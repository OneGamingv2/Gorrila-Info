using UnityEngine;
using GorillaInfo;
using System.Collections.Generic;
using Checker;
using Photon.Pun;
using Steamworks;

public class MoreInfoHandler
{
    private GameObject _moreInfoPanel;
    private TextMesh _nameText;
    private TextMesh _steamNameText;
    private TextMesh _speedText;
    private TextMesh _platformText;
    private TextMesh _pingText;
    private TextMesh _worldScaleText;
    private Renderer _object14Renderer;
    private bool _isOpen;
    private Vector3 _scaleVelocity;
    private Vector3 _panelOpenScale = new Vector3(0.28f, 0.22f, 0.012f);
    private VRRig _targetRig;
    private VRRig _speedRig;
    private Vector3 _lastSpeedPosition;
    private float _lastSpeedTime;
    private float _nextInfoRefreshTime;
    private const float ScaleSmoothTime = 0.08f;
    private const float OpenSnapThresholdSqr = 0.000001f;
    private const float CloseSnapThresholdSqr = 0.00001f;
    private const float InfoRefreshInterval = 0.1f;

    // Traffic-light colours used across all info fields
    private static readonly Color ColGood = new Color(0.20f, 1.00f, 0.45f);   // green
    private static readonly Color ColOk   = new Color(1.00f, 0.88f, 0.12f);   // yellow
    private static readonly Color ColBad  = new Color(1.00f, 0.25f, 0.25f);   // red
    private static readonly Color ColInfo = Color.white;

    private Dictionary<string, Material> _furMaterials = new Dictionary<string, Material>();
    private Color[] _materialColors = new Color[]
    {
        new Color(0f, 0f, 1f),        // bluefur
        new Color(0f, 1f, 1f),        // cyanfur
        new Color(0f, 1f, 0f),        // greenfur
        new Color(1f, 0.27f, 0f),     // lavafur (lava/orange-red)
        new Color(1f, 0.192f, 0.203f),// pinkfur
        new Color(1f, 1f, 1f),        // lightfur (white)
        new Color(1f, 0f, 0f),        // redfur
        new Color(1f, 1f, 0f)         // yellowfur
    };

    private string[] _materialNames = new string[]
    {
        "bluefur", "cyanfur", "greenfur", "lavafur", "pinkfur", "lightfur", "redfur", "yellowfur"
    };

    public void Initialize()
    {
        var menuInstance = GorillaInfoMain.Instance?.menuLoader?.menuInstance;
        if (menuInstance == null) return;

        _moreInfoPanel = FindDeepChild(menuInstance.transform, "MoreInfo")?.gameObject;
        if (_moreInfoPanel == null) return;

        // Clear any bad rotation baked into the prefab panel
        _moreInfoPanel.transform.localRotation = Quaternion.identity;

        _nameText = _moreInfoPanel.transform.Find("Name")?.GetComponent<TextMesh>();
        _speedText = _moreInfoPanel.transform.Find("Speed")?.GetComponent<TextMesh>();
        _platformText = _moreInfoPanel.transform.Find("Platform")?.GetComponent<TextMesh>();
        _pingText = _moreInfoPanel.transform.Find("Ping")?.GetComponent<TextMesh>();
        _worldScaleText = _moreInfoPanel.transform.Find("WorldScale")?.GetComponent<TextMesh>();

        // Steam name row — sits between player name and speed
        _steamNameText = _moreInfoPanel.transform.Find("SteamName")?.GetComponent<TextMesh>();
        if (_steamNameText == null)
            _steamNameText = CreateInfoText("SteamName", new Vector3(0f, 0.044f, -0.01f));

        if (_platformText == null)
            _platformText = CreateInfoText("Platform", new Vector3(0f, -0.014f, -0.01f));
        if (_pingText == null)
            _pingText = CreateInfoText("Ping", new Vector3(0f, -0.042f, -0.01f));
        if (_worldScaleText == null)
            _worldScaleText = CreateInfoText("WorldScale", new Vector3(0f, -0.070f, -0.01f));

        NormalizeInfoFields();
        DisableFpsArtifacts();

        Transform giModel = _moreInfoPanel.transform.Find("GIModel");
        if (giModel != null)
        {
            Transform object14 = giModel.Find("Object_14");
            if (object14 != null)
                _object14Renderer = object14.GetComponent<Renderer>();
        }

        LoadMaterials();

        _moreInfoPanel.SetActive(false);
        Vector3 capturedScale = _moreInfoPanel.transform.localScale;
        if (capturedScale.sqrMagnitude > 0.0001f)
            _panelOpenScale = capturedScale;
        _moreInfoPanel.transform.localScale = Vector3.zero;
        _scaleVelocity = Vector3.zero;
        _isOpen = false;
    }

    private void NormalizeInfoFields()
    {
        ConfigureInfoField(_nameText,      0.015f);
        ConfigureInfoField(_steamNameText, 0.012f);
        ConfigureInfoField(_speedText,     0.0125f);
        ConfigureInfoField(_platformText,  0.0125f);
        ConfigureInfoField(_pingText,      0.0125f);
        ConfigureInfoField(_worldScaleText,0.0125f);
    }

    private void ConfigureInfoField(TextMesh text, float characterSize)
    {
        if (text == null)
            return;

        // Reset localRotation on the text node AND every ancestor up to the panel.
        // Prefab may have baked rotations on intermediate containers that cause sideways text.
        Transform t = text.transform;
        while (t != null && t.gameObject != _moreInfoPanel)
        {
            t.localRotation = Quaternion.identity;
            t = t.parent;
        }

        text.transform.localScale = Vector3.one;
        text.characterSize = characterSize;
        if (text.font != null && text.font.dynamic)
            text.fontStyle = FontStyle.Bold;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
    }

    private void DisableFpsArtifacts()
    {
        if (_moreInfoPanel == null)
            return;

        TextMesh[] texts = _moreInfoPanel.GetComponentsInChildren<TextMesh>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TextMesh tm = texts[i];
            if (tm == null)
                continue;

            bool isKnownInfoField = tm == _nameText || tm == _steamNameText || tm == _speedText ||
                                     tm == _platformText || tm == _pingText || tm == _worldScaleText;
            if (isKnownInfoField)
            {
                // Ensure known fields face forward (no sideways rotation from prefab)
                tm.transform.localRotation = Quaternion.identity;
                continue;
            }

            // Hide ALL unknown text — old prefab labels, debug text, etc.
            tm.gameObject.SetActive(false);
        }
    }

    private void LoadMaterials()
    {
        _furMaterials.Clear();

        foreach (string matName in _materialNames)
        {
            Material mat = Resources.Load<Material>($"Materials/{matName}");

            if (mat == null)
            {
                foreach (AssetBundle bundle in AssetBundle.GetAllLoadedAssetBundles())
                {
                    mat = bundle.LoadAsset<Material>(matName);
                    if (mat != null) break;
                }
            }

            if (mat != null)
                _furMaterials[matName] = mat;
        }
    }

    public void ToggleMoreInfo()
    {
        if (_moreInfoPanel == null) return;

        _isOpen = !_isOpen;
        _scaleVelocity = Vector3.zero;

        if (_isOpen)
        {
            _moreInfoPanel.SetActive(true);
            _moreInfoPanel.transform.localScale = Vector3.zero;
            AudioHelper.PlaySound("open.wav");
        }
        else
        {
            AudioHelper.PlaySound("close.wav");
        }
    }

    public void UpdateAnimation()
    {
        if (_moreInfoPanel == null) return;
        if (!_isOpen && !_moreInfoPanel.activeSelf) return;

        Vector3 target = _isOpen ? _panelOpenScale : Vector3.zero;
        Transform t = _moreInfoPanel.transform;

        t.localScale = Vector3.SmoothDamp(t.localScale, target, ref _scaleVelocity, ScaleSmoothTime);

        if (_isOpen)
        {
            if ((t.localScale - _panelOpenScale).sqrMagnitude < OpenSnapThresholdSqr)
                t.localScale = _panelOpenScale;
        }
        else
        {
            if (t.localScale.sqrMagnitude < CloseSnapThresholdSqr)
            {
                t.localScale = Vector3.zero;
                _scaleVelocity = Vector3.zero;
                _moreInfoPanel.SetActive(false);
            }
        }
    }

    public void UpdatePlayerInfo()
    {
        if (_moreInfoPanel == null || !_moreInfoPanel.activeSelf) return;

        if (Time.time < _nextInfoRefreshTime)
            return;

        _nextInfoRefreshTime = Time.time + InfoRefreshInterval;

        VRRig target = GorillaInfoMain.Instance?.gunLib?.lockedTarget;
        if (target == null) return;

        // Flush cached scale when locked target changes
        if (target != _targetRig)
        {
            _targetRig = target;
            WorldScaleResolver.ForceRefreshForRig(target);
        }

        string photonName = target.Creator?.GetPlayerRef()?.NickName ?? "Unknown";

        // ── Name row (always white) ──────────────────────────────────────────
        if (_nameText != null)
        {
            _nameText.text  = photonName;
            _nameText.color = ColInfo;
        }

        // ── Steam name row (colour = spoof status) ───────────────────────────
        if (_steamNameText != null)
        {
            bool hasSteam = WorldScaleResolver.TryGetSteamPersonaName(target, out string steamName);
            if (!hasSteam)
            {
                _steamNameText.text  = "Steam: N/A";
                _steamNameText.color = ColOk;   // yellow — no Steam data available
            }
            else if (steamName.Equals(photonName, System.StringComparison.OrdinalIgnoreCase))
            {
                _steamNameText.text  = $"Steam: {steamName}";
                _steamNameText.color = ColGood;  // green — name matches, legit
            }
            else
            {
                _steamNameText.text  = $"Steam: {steamName}";
                _steamNameText.color = ColBad;   // red — Photon name differs from Steam name (spoofed)
            }
        }

        // ── Speed row ────────────────────────────────────────────────────────
        if (_speedText != null)
        {
            float speed = GetPlayerSpeed(target);
            _speedText.text  = $"Speed: {speed:F2} m/s";
            _speedText.color = SpeedToColor(speed);
        }

        // ── Platform row ─────────────────────────────────────────────────────
        if (_platformText != null)
        {
            Platform plat = target.GetPlatform();
            _platformText.text  = $"Platform: {ParsePlatform(plat)}";
            _platformText.color = PlatformToColor(plat);
        }

        // ── Ping row ─────────────────────────────────────────────────────────
        if (_pingText != null)
        {
            int ping = GetPlayerPing(target);
            _pingText.text  = $"Ping: {ping}ms";
            _pingText.color = PingToColor(ping);
        }

        // ── World Scale row ──────────────────────────────────────────────────
        if (_worldScaleText != null)
        {
            float ws = WorldScaleResolver.GetWorldScale(target);
            int wsPercent = Mathf.RoundToInt(ws * 100f);
            _worldScaleText.text  = $"World Scale: {wsPercent}%";
            _worldScaleText.color = ScaleToColor(ws);
        }

        UpdateModelMaterial(target);
    }

    // ── Colour helpers ──────────────────────────────────────────────────────

    private static Color ScaleToColor(float scale)
    {
        // 100% (±5%) = green, 80–95% or 105–125% = yellow, outside = red
        if (scale >= 0.95f && scale <= 1.05f) return ColGood;
        if (scale >= 0.80f && scale <= 1.25f) return ColOk;
        return ColBad;
    }

    private static Color SpeedToColor(float metersPerSec)
    {
        if (metersPerSec < 5f)  return ColGood;
        if (metersPerSec < 12f) return ColOk;
        return ColBad;
    }

    private static Color PingToColor(int ms)
    {
        if (ms < 90)  return ColGood;
        if (ms < 200) return ColOk;
        return ColBad;
    }

    private static Color PlatformToColor(Platform plat)
    {
        switch (plat)
        {
            case Platform.Steam:      return ColGood;               // green — verified Steam PC
            case Platform.PC:         return ColOk;                 // yellow — PC but not Steam-verified
            case Platform.Standalone: return new Color(0.4f, 0.8f, 1f); // cyan — Quest
            default:                  return ColInfo;
        }
    }

    private TextMesh CreateInfoText(string name, Vector3 localPosition)
    {
        if (_moreInfoPanel == null)
            return null;

        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(_moreInfoPanel.transform, false);
        textObj.transform.localPosition = localPosition;
        textObj.transform.localRotation = Quaternion.identity;
        textObj.transform.localScale = Vector3.one;

        TextMesh text = textObj.AddComponent<TextMesh>();
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.characterSize = 0.016f;
        if (text.font != null && text.font.dynamic)
            text.fontStyle = FontStyle.Bold;
        text.color = Color.white;
        return text;
    }

    private string ParsePlatform(Platform platform)
    {
        switch (platform)
        {
            case Platform.Steam: return "Steam";
            case Platform.PC: return "PC";
            case Platform.Standalone: return "Quest";
            default: return "Unknown";
        }
    }

    private int GetPlayerPing(VRRig rig)
    {
        var playerRef = rig?.Creator?.GetPlayerRef();
        if (playerRef?.CustomProperties != null)
        {
            object value;
            if (playerRef.CustomProperties.TryGetValue("ping", out value) ||
                playerRef.CustomProperties.TryGetValue("Ping", out value))
            {
                if (value is int i)
                    return i;
                if (value != null && int.TryParse(value.ToString(), out int parsed))
                    return parsed;
            }
        }

        return PhotonNetwork.GetPing();
    }

    private void UpdateModelMaterial(VRRig rig)
    {
        if (_object14Renderer == null || _furMaterials.Count == 0) return;

        Color playerColor = rig.GetColor();
        string closestMaterial = FindClosestMaterial(playerColor);

        if (!string.IsNullOrEmpty(closestMaterial) && _furMaterials.TryGetValue(closestMaterial, out Material mat))
        {
            _object14Renderer.material = mat;
        }
    }

    private string FindClosestMaterial(Color playerColor)
    {
        float minDistance = float.MaxValue;
        string closestMat = "";

        for (int i = 0; i < _materialColors.Length; i++)
        {
            float distance = Vector3.Distance(
                new Vector3(playerColor.r, playerColor.g, playerColor.b),
                new Vector3(_materialColors[i].r, _materialColors[i].g, _materialColors[i].b)
            );

            if (distance < minDistance)
            {
                minDistance = distance;
                closestMat = _materialNames[i];
            }
        }

        return closestMat;
    }

    private float GetPlayerSpeed(VRRig rig)
    {
        if (rig == null)
            return 0f;

        float now = Time.time;
        Vector3 current = rig.transform.position;

        if (_speedRig != rig || _lastSpeedTime <= 0f)
        {
            _speedRig = rig;
            _lastSpeedPosition = current;
            _lastSpeedTime = now;
            return 0f;
        }

        float dt = now - _lastSpeedTime;
        if (dt <= 0f)
            return 0f;

        float speed = Vector3.Distance(current, _lastSpeedPosition) / dt;
        _lastSpeedPosition = current;
        _lastSpeedTime = now;
        return speed;
    }

    private static Transform FindDeepChild(Transform parent, string name)
    {
        if (parent == null) return null;
        Transform direct = parent.Find(name);
        if (direct != null) return direct;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeepChild(parent.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }
}
