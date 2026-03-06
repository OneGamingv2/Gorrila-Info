using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace GorillaInfo
{
    public class MusicHandler
    {
        public TextMesh txtSongTitle, txtSongArtist;
        private GameObject musicTab;
        private Camera _previewCamera;
        private RenderTexture _previewTexture;
        private Renderer _previewRenderer;
        private float _nextMetadataRefreshTime;
        private float _nextPreviewRefreshTime;
        private float _nextProcessFallbackScanTime;
        private const float MetadataRefreshIntervalSeconds = 0.8f;
        private const float PreviewRefreshIntervalSeconds = 1f / 8f;
        private const float ProcessFallbackScanIntervalSeconds = 2f;
        private const int PreviewWidth = 480;
        private const int PreviewHeight = 270;

        private string currentTitle = "No media detected";
        private string currentArtist = "-";
        private string currentProvider = "None";
        private string currentState = "Idle";
        private string currentWindowTitle = "-";
        private string _lastRenderedTitle;
        private string _lastRenderedArtist;
        private string _lastRenderedProvider;
        private string _lastRenderedState;
        private static readonly string[] CandidateProcessNames = { "Spotify", "chrome", "msedge", "firefox", "brave", "opera" };
        private static readonly string[] IgnoredWindowTitleFragments =
        {
            "connect", "settings", "discord", "steam", "gorilla tag", "visual studio", "file explorer"
        };

        private const string SpotifyUrl = "https://open.spotify.com/";
        private const string YouTubeUrl = "https://www.youtube.com/";

        internal enum VirtualKeyCodes : uint
        {
            NEXT_TRACK = 0xB0,
            PREVIOUS_TRACK = 0xB1,
            PLAY_PAUSE = 0xB3,
            VOLUME_MUTE = 0xAD,
            VOLUME_DOWN = 0xAE,
            VOLUME_UP = 0xAF,
        }

        private const uint KeyEventKeyUp = 0x0002;

        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        internal static extern void keybd_event(uint bVk, uint bScan, uint dwFlags, uint dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        internal static void SendKey(VirtualKeyCodes virtualKeyCode)
        {
            keybd_event((uint)virtualKeyCode, 0, 0, 0);
            keybd_event((uint)virtualKeyCode, 0, KeyEventKeyUp, 0);
        }

        public void Initialize()
        {
            if (GorillaInfoMain.Instance == null || GorillaInfoMain.Instance.menuLoader == null || GorillaInfoMain.Instance.menuLoader.menuInstance == null)
                return;

            Transform menu = GorillaInfoMain.Instance.menuLoader.menuInstance.transform;
            if (menu == null) return;

            Transform musicTabTrans = FindDeepChild(menu, "MusicTab");
            if (musicTabTrans == null) return;

            musicTab = musicTabTrans.gameObject;

            Transform titleTrans = FindDeepChild(musicTabTrans, "SongTitle");
            Transform artistTrans = FindDeepChild(musicTabTrans, "SongArtist");

            if (titleTrans != null)
                txtSongTitle = titleTrans.GetComponent<TextMesh>();
            if (artistTrans != null)
                txtSongArtist = artistTrans.GetComponent<TextMesh>();

            NormalizeMusicHeader(menu, musicTabTrans);
            EnsureMusicTextSizing();
            EnsurePreviewSurface(musicTabTrans);

            currentState = "Ready";
            _nextMetadataRefreshTime = 0f;
            _nextPreviewRefreshTime = 0f;
            _nextProcessFallbackScanTime = 0f;

            UpdateDisplay();
        }

        public void ToggleMusicTab(bool enabled)
        {
            if (musicTab != null)
                musicTab.SetActive(enabled);
        }

        public void PlayPauseMusic()
        {
            SendKey(VirtualKeyCodes.PLAY_PAUSE);
            currentState = "Play/Pause sent";
            _nextMetadataRefreshTime = 0f;
            UpdateMusicData();
        }

        public void NextTrack()
        {
            SendKey(VirtualKeyCodes.NEXT_TRACK);
            currentState = "Next sent";
            _nextMetadataRefreshTime = Time.time + 0.2f;
        }

        public void PreviousTrack()
        {
            SendKey(VirtualKeyCodes.PREVIOUS_TRACK);
            currentState = "Previous sent";
            _nextMetadataRefreshTime = Time.time + 0.2f;
        }

        public void OpenSpotify()
        {
            OpenUrl(SpotifyUrl, "Spotify", "Opened Spotify in browser");
        }

        public void OpenYouTube()
        {
            OpenUrl(YouTubeUrl, "YouTube", "Opened YouTube in browser");
        }

        public void OpenCurrentInBrowser()
        {
            if (currentProvider.Equals("Spotify", StringComparison.OrdinalIgnoreCase))
            {
                OpenSpotify();
                return;
            }

            if (currentProvider.Equals("YouTube", StringComparison.OrdinalIgnoreCase))
            {
                OpenYouTube();
                return;
            }

            OpenUrl("https://www.google.com/search?q=" + Uri.EscapeDataString(currentTitle), "Browser", "Opened current title search");
        }

        public void RefreshNowPlaying()
        {
            _nextMetadataRefreshTime = 0f;
            _nextPreviewRefreshTime = 0f;
            UpdateMusicData();
        }

        public void VolumeUp()
        {
            SendKey(VirtualKeyCodes.VOLUME_UP);
            currentState = "Volume +";
            UpdateDisplay();
        }

        public void VolumeDown()
        {
            SendKey(VirtualKeyCodes.VOLUME_DOWN);
            currentState = "Volume -";
            UpdateDisplay();
        }

        public void ToggleMute()
        {
            SendKey(VirtualKeyCodes.VOLUME_MUTE);
            currentState = "Mute toggled";
            UpdateDisplay();
        }

        public void UpdateMusicData()
        {
            if (musicTab == null || !musicTab.activeInHierarchy)
                return;

            if (Time.time >= _nextMetadataRefreshTime)
            {
                _nextMetadataRefreshTime = Time.time + MetadataRefreshIntervalSeconds;
                UpdateNowPlayingInfo();
            }

            if (Time.time >= _nextPreviewRefreshTime)
            {
                _nextPreviewRefreshTime = Time.time + PreviewRefreshIntervalSeconds;
                UpdatePreviewFrame();
            }

            UpdateDisplay();
        }

        private void UpdateNowPlayingInfo()
        {
            if (TryGetForegroundWindowTitle(out string title))
            {
                currentWindowTitle = title;
                if (!IsBadWindowTitle(title) && TryParseTrackFromWindowTitle(title, out string provider, out string parsedTitle, out string parsedArtist))
                {
                    currentProvider = provider;
                    currentTitle = Limit(parsedTitle, 56);
                    currentArtist = Limit(parsedArtist, 34);
                    currentState = "Detected";
                    return;
                }
            }

            if (Time.time >= _nextProcessFallbackScanTime && TryDetectFromKnownProcesses(out string fallbackProvider, out string fallbackTitle, out string fallbackArtist))
            {
                _nextProcessFallbackScanTime = Time.time + ProcessFallbackScanIntervalSeconds;
                currentProvider = fallbackProvider;
                currentTitle = Limit(fallbackTitle, 56);
                currentArtist = Limit(fallbackArtist, 34);
                currentState = "Detected (fallback)";
                return;
            }

            if (Time.time >= _nextProcessFallbackScanTime)
                _nextProcessFallbackScanTime = Time.time + ProcessFallbackScanIntervalSeconds;

            currentProvider = "System";
            currentState = "No media found";
            currentTitle = "No active Spotify/YouTube track";
            currentArtist = "-";
        }

        private bool TryDetectFromKnownProcesses(out string provider, out string title, out string artist)
        {
            provider = string.Empty;
            title = string.Empty;
            artist = string.Empty;

            for (int i = 0; i < CandidateProcessNames.Length; i++)
            {
                Process[] processes;
                try
                {
                    processes = Process.GetProcessesByName(CandidateProcessNames[i]);
                }
                catch
                {
                    continue;
                }

                for (int p = 0; p < processes.Length; p++)
                {
                    string windowTitle;
                    try
                    {
                        windowTitle = processes[p].MainWindowTitle;
                    }
                    catch
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(windowTitle))
                        continue;

                    if (IsBadWindowTitle(windowTitle))
                        continue;

                    if (TryParseTrackFromWindowTitle(windowTitle, out provider, out title, out artist))
                        return true;
                }
            }

            return false;
        }

        private void OpenUrl(string url, string provider, string state)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
                currentProvider = provider;
                currentState = state;
                _nextMetadataRefreshTime = 0f;
                UpdateDisplay();
            }
            catch (Exception ex)
            {
                currentState = "Open failed";
                currentWindowTitle = ex.GetType().Name;
                UpdateDisplay();
            }
        }

        private bool TryParseTrackFromWindowTitle(string title, out string provider, out string parsedTitle, out string parsedArtist)
        {
            provider = string.Empty;
            parsedTitle = string.Empty;
            parsedArtist = string.Empty;

            if (string.IsNullOrWhiteSpace(title))
                return false;

            if (title.IndexOf("YouTube", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                provider = "YouTube";
                ParseYouTubeTitle(title, out parsedTitle, out parsedArtist);
                return true;
            }

            if (title.IndexOf("Spotify", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                provider = "Spotify";
                ParseSpotifyTitle(title, out parsedTitle, out parsedArtist);
                return true;
            }

            return false;
        }

        private void ParseYouTubeTitle(string title, out string parsedTitle, out string parsedArtist)
        {
            string cleaned = title;
            int youtubeMarker = cleaned.IndexOf(" - YouTube", StringComparison.OrdinalIgnoreCase);
            if (youtubeMarker >= 0)
                cleaned = cleaned.Substring(0, youtubeMarker);

            parsedTitle = cleaned;
            parsedArtist = "YouTube";
        }

        private void ParseSpotifyTitle(string title, out string parsedTitle, out string parsedArtist)
        {
            string cleaned = title;
            int spotifyMarker = cleaned.IndexOf(" - Spotify", StringComparison.OrdinalIgnoreCase);
            if (spotifyMarker >= 0)
                cleaned = cleaned.Substring(0, spotifyMarker);

            string[] parts = cleaned.Split(new[] { " - " }, StringSplitOptions.None);
            if (parts.Length >= 2)
            {
                parsedTitle = parts[0].Trim();
                parsedArtist = parts[1].Trim();
            }
            else
            {
                parsedTitle = cleaned;
                parsedArtist = "Spotify";
            }
        }

        private void UpdatePreviewFrame()
        {
            if (_previewRenderer == null)
                return;

            Camera source = Camera.main;
            if (source == null)
            {
                currentState = "No camera";
                return;
            }

            EnsurePreviewResources();
            if (_previewTexture == null || _previewCamera == null)
                return;

            _previewCamera.transform.position = source.transform.position;
            _previewCamera.transform.rotation = source.transform.rotation;
            _previewCamera.fieldOfView = source.fieldOfView;
            _previewCamera.nearClipPlane = source.nearClipPlane;
            _previewCamera.farClipPlane = source.farClipPlane;
            _previewCamera.clearFlags = source.clearFlags;
            _previewCamera.backgroundColor = source.backgroundColor;
            _previewCamera.cullingMask = source.cullingMask;
            _previewCamera.targetTexture = _previewTexture;
            _previewCamera.Render();
        }

        private void EnsurePreviewSurface(Transform musicTabTrans)
        {
            if (musicTabTrans == null)
                return;

            Transform preview = FindDeepChild(musicTabTrans, "ScreenPreview");
            if (preview == null)
            {
                GameObject previewObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                previewObj.name = "ScreenPreview";
                previewObj.transform.SetParent(musicTabTrans, false);
                previewObj.transform.localPosition = new Vector3(0.06f, 0.045f, -0.008f);
                previewObj.transform.localRotation = Quaternion.identity;
                previewObj.transform.localScale = new Vector3(0.26f, 0.15f, 1f);

                Collider col = previewObj.GetComponent<Collider>();
                if (col != null)
                    UnityEngine.Object.Destroy(col);

                preview = previewObj.transform;
            }

            _previewRenderer = preview.GetComponent<Renderer>();
            EnsurePreviewResources();
        }

        private void EnsurePreviewResources()
        {
            if (_previewTexture == null)
            {
                _previewTexture = new RenderTexture(PreviewWidth, PreviewHeight, 16, RenderTextureFormat.ARGB32)
                {
                    antiAliasing = 1,
                    useMipMap = false,
                    autoGenerateMips = false,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
            }

            if (_previewRenderer != null)
            {
                Shader shader = Shader.Find("Unlit/Texture") ?? Shader.Find("Standard");
                if (_previewRenderer.material == null || _previewRenderer.material.shader != shader)
                    _previewRenderer.material = new Material(shader);

                _previewRenderer.material.mainTexture = _previewTexture;
            }

            if (_previewCamera == null)
            {
                GameObject cameraObj = new GameObject("GorillaInfoPreviewCamera");
                cameraObj.hideFlags = HideFlags.HideAndDontSave;
                _previewCamera = cameraObj.AddComponent<Camera>();
                _previewCamera.enabled = false;
            }
        }

        private void NormalizeMusicHeader(Transform musicTabTrans)
        {
            if (musicTabTrans == null)
                return;

            TextMesh[] texts = musicTabTrans.GetComponentsInChildren<TextMesh>(true);
            if (texts == null || texts.Length == 0)
                return;

            TextMesh primary = null;
            for (int i = 0; i < texts.Length; i++)
            {
                TextMesh tm = texts[i];
                if (tm == null || string.IsNullOrWhiteSpace(tm.text))
                    continue;

                if (tm.text.Trim().Equals("MUSIC", StringComparison.OrdinalIgnoreCase))
                {
                    if (primary == null)
                    {
                        primary = tm;
                    }
                    else
                    {
                        tm.gameObject.SetActive(false);
                    }
                }
            }
        }

        private void NormalizeMusicHeader(Transform menuRoot, Transform musicTabTrans)
        {
            if (musicTabTrans == null)
                return;

            TextMesh tabHeader = FindDeepChild(musicTabTrans, "MusicHeader")?.GetComponent<TextMesh>();
            if (tabHeader != null)
            {
                tabHeader.text = string.Empty;
                tabHeader.gameObject.SetActive(false);
            }

            if (menuRoot == null)
                return;

            TextMesh[] allTexts = menuRoot.GetComponentsInChildren<TextMesh>(true);
            for (int i = 0; i < allTexts.Length; i++)
            {
                TextMesh tm = allTexts[i];
                if (tm == null || string.IsNullOrWhiteSpace(tm.text))
                    continue;

                string trimmed = tm.text.Trim();
                if (!trimmed.Equals("MUSIC", StringComparison.OrdinalIgnoreCase) &&
                    !trimmed.Equals("Music", StringComparison.OrdinalIgnoreCase))
                    continue;

                Transform tr = tm.transform;
                bool isInMusicTab = tr.IsChildOf(musicTabTrans);
                bool isSidebarButton = FindAncestorByName(tr, "MusicButton") != null;
                bool isSongInfo = tr == txtSongTitle?.transform || tr == txtSongArtist?.transform;

                if (isInMusicTab && !isSongInfo)
                {
                    tm.gameObject.SetActive(false);
                    continue;
                }

                if (!isInMusicTab && !isSidebarButton)
                    tm.gameObject.SetActive(false);
            }
        }

        private void EnsureMusicTextSizing()
        {
            if (txtSongTitle != null)
            {
                txtSongTitle.characterSize = Mathf.Max(txtSongTitle.characterSize, 0.018f);
                txtSongTitle.fontStyle = FontStyle.Bold;
            }

            if (txtSongArtist != null)
            {
                txtSongArtist.characterSize = Mathf.Max(txtSongArtist.characterSize, 0.015f);
                txtSongArtist.fontStyle = FontStyle.Bold;
                txtSongArtist.lineSpacing = 0.9f;
            }
        }

        private Transform FindDeepChild(Transform parent, string name)
        {
            if (parent == null)
                return null;

            Transform direct = parent.Find(name);
            if (direct != null)
                return direct;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindDeepChild(parent.GetChild(i), name);
                if (found != null)
                    return found;
            }

            return null;
        }

        private Transform FindAncestorByName(Transform node, string name)
        {
            Transform cur = node;
            while (cur != null)
            {
                if (cur.name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return cur;
                cur = cur.parent;
            }
            return null;
        }

        private bool IsBadWindowTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return true;

            string lowered = title.Trim().ToLowerInvariant();
            if (lowered.Length < 5)
                return true;

            for (int i = 0; i < IgnoredWindowTitleFragments.Length; i++)
            {
                if (lowered.Contains(IgnoredWindowTitleFragments[i]))
                    return true;
            }

            return false;
        }

        private bool TryGetForegroundWindowTitle(out string title)
        {
            title = string.Empty;
            IntPtr handle = GetForegroundWindow();
            if (handle == IntPtr.Zero)
                return false;

            StringBuilder builder = new StringBuilder(512);
            int length = GetWindowText(handle, builder, builder.Capacity);
            if (length <= 0)
                return false;

            title = builder.ToString();
            return !string.IsNullOrWhiteSpace(title);
        }

        private static string Limit(string value, int max)
        {
            if (string.IsNullOrEmpty(value))
                return "-";

            return value.Length > max ? value.Substring(0, max) : value;
        }

        private void UpdateDisplay()
        {
            if (_lastRenderedTitle == currentTitle &&
                _lastRenderedArtist == currentArtist &&
                _lastRenderedProvider == currentProvider &&
                _lastRenderedState == currentState)
            {
                return;
            }

            if (txtSongTitle != null)
                txtSongTitle.text = currentTitle;

            if (txtSongArtist != null)
                txtSongArtist.text = $"{currentArtist}\n{currentProvider} | {currentState}";

            _lastRenderedTitle = currentTitle;
            _lastRenderedArtist = currentArtist;
            _lastRenderedProvider = currentProvider;
            _lastRenderedState = currentState;
        }
    }
}
