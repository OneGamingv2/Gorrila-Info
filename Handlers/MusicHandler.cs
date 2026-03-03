using System;
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
        private float nextRefreshTime;
        private const float RefreshIntervalSeconds = 1.2f;

        private string currentTitle = "No media detected";
        private string currentArtist = "-";
        private string currentProvider = "None";
        private string currentState = "Idle";
        private string currentWindowTitle = "-";

        private const string SpotifyUrl = "https://open.spotify.com/";
        private const string YouTubeUrl = "https://www.youtube.com/";

        internal enum VirtualKeyCodes : uint
        {
            NEXT_TRACK = 0xB0,
            PREVIOUS_TRACK = 0xB1,
            PLAY_PAUSE = 0xB3,
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

            Transform musicTabTrans = menu.Find("MusicTab");
            if (musicTabTrans == null) return;

            musicTab = musicTabTrans.gameObject;

            // Initialize text components based on hierarchy
            Transform titleTrans = musicTabTrans.Find("SongTitle");
            Transform artistTrans = musicTabTrans.Find("SongArtist");

            if (titleTrans != null)
                txtSongTitle = titleTrans.GetComponent<TextMesh>();
            if (artistTrans != null)
                txtSongArtist = artistTrans.GetComponent<TextMesh>();

            currentState = "Ready";
            nextRefreshTime = 0f;

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
            nextRefreshTime = 0f;
            UpdateMusicData();
        }

        public void NextTrack()
        {
            SendKey(VirtualKeyCodes.NEXT_TRACK);
            currentState = "Next sent";
            nextRefreshTime = Time.time + 0.2f;
        }

        public void PreviousTrack()
        {
            SendKey(VirtualKeyCodes.PREVIOUS_TRACK);
            currentState = "Previous sent";
            nextRefreshTime = Time.time + 0.2f;
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
            nextRefreshTime = 0f;
            UpdateMusicData();
        }

        public void UpdateMusicData()
        {
            if (Time.time < nextRefreshTime)
                return;

            nextRefreshTime = Time.time + RefreshIntervalSeconds;
            UpdateFromForegroundWindow();
            UpdateDisplay();
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
                nextRefreshTime = 0f;
                UpdateDisplay();
            }
            catch (Exception ex)
            {
                currentState = "Open failed";
                currentWindowTitle = ex.GetType().Name;
                UpdateDisplay();
            }
        }

        private void UpdateFromForegroundWindow()
        {
            if (!TryGetForegroundWindowTitle(out string title))
            {
                currentState = "No active window";
                return;
            }

            currentWindowTitle = title;

            if (title.IndexOf("YouTube", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                currentProvider = "YouTube";
                currentState = "Detected";
                ParseYouTubeTitle(title);
                return;
            }

            if (title.IndexOf("Spotify", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                currentProvider = "Spotify";
                currentState = "Detected";
                ParseSpotifyTitle(title);
                return;
            }

            currentProvider = "System";
            currentState = "No Spotify/YouTube focus";
            currentTitle = "Switch to Spotify/YouTube to view track";
            currentArtist = "-";
        }

        private void ParseYouTubeTitle(string title)
        {
            string cleaned = title;
            int youtubeMarker = cleaned.IndexOf(" - YouTube", StringComparison.OrdinalIgnoreCase);
            if (youtubeMarker >= 0)
                cleaned = cleaned.Substring(0, youtubeMarker);

            currentTitle = Limit(cleaned, 42);
            currentArtist = "YouTube";
        }

        private void ParseSpotifyTitle(string title)
        {
            string cleaned = title;
            int spotifyMarker = cleaned.IndexOf(" - Spotify", StringComparison.OrdinalIgnoreCase);
            if (spotifyMarker >= 0)
                cleaned = cleaned.Substring(0, spotifyMarker);

            string[] parts = cleaned.Split(new[] { " - " }, StringSplitOptions.None);
            if (parts.Length >= 2)
            {
                currentTitle = Limit(parts[0].Trim(), 42);
                currentArtist = Limit(parts[1].Trim(), 28);
            }
            else
            {
                currentTitle = Limit(cleaned, 42);
                currentArtist = "Spotify";
            }
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
            if (txtSongTitle != null)
                txtSongTitle.text = currentTitle;

            if (txtSongArtist != null)
                txtSongArtist.text = $"{currentArtist}\n{currentProvider} | {currentState}";
        }
    }
}
