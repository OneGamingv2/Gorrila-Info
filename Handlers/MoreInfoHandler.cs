using UnityEngine;
using GorillaInfo;
using System.Collections.Generic;
using Checker;
using Photon.Pun;

public class MoreInfoHandler
{
    private GameObject _moreInfoPanel;
    private TextMesh _nameText;
    private TextMesh _speedText;
    private TextMesh _platformText;
    private TextMesh _pingText;
    private TextMesh _worldScaleText;
    private Renderer _object14Renderer;
    private bool _isOpen;
    private Vector3 _scaleVelocity;
    private VRRig _speedRig;
    private Vector3 _lastSpeedPosition;
    private float _lastSpeedTime;
    private float _nextInfoRefreshTime;
    private const float ScaleSmoothTime = 0.08f;
    private const float OpenSnapThresholdSqr = 0.000001f;
    private const float CloseSnapThresholdSqr = 0.00001f;
    private const float InfoRefreshInterval = 0.1f;

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

        _moreInfoPanel = menuInstance.transform.Find("MoreInfo")?.gameObject;
        if (_moreInfoPanel == null) return;

        _nameText = _moreInfoPanel.transform.Find("Name")?.GetComponent<TextMesh>();
        _speedText = _moreInfoPanel.transform.Find("Speed")?.GetComponent<TextMesh>();
        _platformText = _moreInfoPanel.transform.Find("Platform")?.GetComponent<TextMesh>();
        _pingText = _moreInfoPanel.transform.Find("Ping")?.GetComponent<TextMesh>();
        _worldScaleText = _moreInfoPanel.transform.Find("WorldScale")?.GetComponent<TextMesh>();

        if (_platformText == null)
            _platformText = CreateInfoText("Platform", new Vector3(0f, -0.02f, -0.01f));
        if (_pingText == null)
            _pingText = CreateInfoText("Ping", new Vector3(0f, -0.05f, -0.01f));
        if (_worldScaleText == null)
            _worldScaleText = CreateInfoText("WorldScale", new Vector3(0f, -0.08f, -0.01f));

        Transform giModel = _moreInfoPanel.transform.Find("GIModel");
        if (giModel != null)
        {
            Transform object14 = giModel.Find("Object_14");
            if (object14 != null)
                _object14Renderer = object14.GetComponent<Renderer>();
        }

        LoadMaterials();

        _moreInfoPanel.SetActive(false);
        _moreInfoPanel.transform.localScale = Vector3.zero;
        _scaleVelocity = Vector3.zero;
        _isOpen = false;
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

        Vector3 target = _isOpen ? Vector3.one : Vector3.zero;
        Transform t = _moreInfoPanel.transform;

        t.localScale = Vector3.SmoothDamp(t.localScale, target, ref _scaleVelocity, ScaleSmoothTime);

        if (_isOpen)
        {
            if ((t.localScale - Vector3.one).sqrMagnitude < OpenSnapThresholdSqr)
                t.localScale = Vector3.one;
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

        if (_nameText != null)
        {
            string playerName = target.OwningNetPlayer?.NickName ?? "Unknown";
            _nameText.text = playerName;
        }

        if (_speedText != null)
        {
            float speed = GetPlayerSpeed(target);
            _speedText.text = $"Speed: {speed:F2} m/s\nPlatform: {ParsePlatform(target.GetPlatform())}\nPing: {GetPlayerPing(target)}ms\nWorld Scale: {WorldScaleResolver.GetWorldScale(target) * 100f:F0}%\nArm Length: {ArmLengthResolver.GetArmLengthScale(target) * 100f:F0}%";
        }

        if (_platformText != null)
            _platformText.text = $"Platform: {ParsePlatform(target.GetPlatform())}";

        if (_pingText != null)
            _pingText.text = $"Ping: {GetPlayerPing(target)}ms";

        if (_worldScaleText != null)
            _worldScaleText.text = $"World/Arms: {WorldScaleResolver.GetWorldScale(target) * 100f:F0}% / {ArmLengthResolver.GetArmLengthScale(target) * 100f:F0}%";

        UpdateModelMaterial(target);
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
}
