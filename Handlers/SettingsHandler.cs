using UnityEngine;
using GorillaInfo;

public class SettingsHandler
{
    private static readonly string[] GunStyles = { "Purple", "Red", "Green", "Yellow" };
    private TextMesh _lockOnText, _nametagsText, _gunStyleText, _passThroughText;
    private bool _lockOnEnabled, _nametagsEnabled, _passThroughEnabled;
    private int _gunStyleIndex;

    public void InitializeSettings()
    {
        Transform settings = GorillaInfoMain.Instance.menuLoader.settingsPanel?.transform;
        if (settings == null) return;

        _lockOnText = FindButtonLabel(settings, "LockOn");
        _nametagsText = FindButtonLabel(settings, "Nametags");
        _gunStyleText = FindButtonLabel(settings, "GunStyle");
        _passThroughText = FindButtonLabel(settings, "PassThroughGun");

        var gunLib = GorillaInfoMain.Instance.gunLib;
        if (gunLib != null)
        {
            _lockOnEnabled = gunLib.autoLockEnabled;
            _nametagsEnabled = gunLib.nametagsEnabled;
            _passThroughEnabled = gunLib.passThroughEnabled;
        }

        UpdateAllTexts();
    }

    private TextMesh FindButtonLabel(Transform root, string buttonName)
    {
        Transform btn = FindDeepChild(root, buttonName);
        if (btn == null)
            return null;

        TextMesh tm = btn.GetComponent<TextMesh>();
        if (tm != null)
            return tm;

        return btn.GetComponentInChildren<TextMesh>(true);
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

    public void ToggleLockOn()
    {
        _lockOnEnabled = !_lockOnEnabled;
        GorillaInfoMain.Instance.gunLib.autoLockEnabled = _lockOnEnabled;
        if (_lockOnText != null)
            _lockOnText.text = _lockOnEnabled ? "LockOn: ON" : "LockOn: OFF";

        GorillaInfoMain.Instance.updMain?.UpdateMainPage();
    }

    public void ToggleNametags()
    {
        _nametagsEnabled = !_nametagsEnabled;
        GorillaInfoMain.Instance.gunLib.nametagsEnabled = _nametagsEnabled;
        if (_nametagsText != null)
            _nametagsText.text = _nametagsEnabled ? "Nametags: ON" : "Nametags: OFF";
    }

    public void CycleGunStyle()
    {
        _gunStyleIndex = (_gunStyleIndex + 1) % 4;
        GorillaInfoMain.Instance.gunLib.SetGunStyle(_gunStyleIndex);
        if (_gunStyleText != null)
            _gunStyleText.text = $"GunStyle: {GunStyles[_gunStyleIndex]}";
    }

    public void TogglePassThroughGun()
    {
        _passThroughEnabled = !_passThroughEnabled;
        GorillaInfoMain.Instance.gunLib.passThroughEnabled = _passThroughEnabled;
        if (_passThroughText != null)
            _passThroughText.text = _passThroughEnabled ? "PassThrough: ON" : "PassThrough: OFF";
    }

    private void UpdateAllTexts()
    {
        if (_lockOnText != null) _lockOnText.text = _lockOnEnabled ? "LockOn: ON" : "LockOn: OFF";
        if (_nametagsText != null) _nametagsText.text = _nametagsEnabled ? "Nametags: ON" : "Nametags: OFF";
        if (_gunStyleText != null) _gunStyleText.text = $"GunStyle: {GunStyles[_gunStyleIndex]}";
        if (_passThroughText != null) _passThroughText.text = _passThroughEnabled ? "PassThrough: ON" : "PassThrough: OFF";
    }
}
