using UnityEngine;
using GorillaInfo;
using BepInEx.Configuration;

public class SettingsHandler
{
    private static readonly string[] GunStyles = { "Purple", "Red", "Green", "Yellow", "Cyan", "Orange", "Pink", "White", "Blue", "Black" };
    private static readonly string[] GunSizes = { "Small", "Normal", "Large", "XL" };
    private TextMesh _notificationsText;
    private TextMesh _lockOnText;
    private TextMesh _nametagsText;
    private TextMesh _gunStyleText;
    private TextMesh _passThroughText;
    private TextMesh _gunSizeText;
    private TextMesh _lockPointerText;
    private TextMesh _targetSphereText;
    private TextMesh _gunRayText;
    private bool _notificationsEnabled;
    private bool _lockOnEnabled;
    private bool _nametagsEnabled;
    private bool _passThroughEnabled;
    private bool _lockPointerEnabled;
    private bool _targetSphereEnabled;
    private bool _gunRayEnabled;
    private int _gunStyleIndex;
    private int _gunSizeIndex;
    private bool _configInitialized;
    private ConfigEntry<bool> _notificationsConfig;
    private ConfigEntry<bool> _lockOnConfig;
    private ConfigEntry<bool> _nametagsConfig;
    private ConfigEntry<bool> _passThroughConfig;
    private ConfigEntry<int> _gunStyleConfig;
    private ConfigEntry<int> _gunSizeConfig;
    private ConfigEntry<bool> _lockPointerConfig;
    private ConfigEntry<bool> _targetSphereConfig;
    private ConfigEntry<bool> _gunRayConfig;

    public void InitializeSettings()
    {
        EnsureConfigBindings();

        Transform settings = GorillaInfoMain.Instance.menuLoader.settingsPanel?.transform;
        if (settings == null) return;

        _notificationsText = FindButtonLabel(settings, "Notifications");
        _lockOnText = FindButtonLabel(settings, "LockOn");
        _nametagsText = FindButtonLabel(settings, "Nametags");
        _gunStyleText = FindButtonLabel(settings, "GunStyle");
        _passThroughText = FindButtonLabel(settings, "PassThroughGun");
        _gunSizeText = FindButtonLabel(settings, "GunSize");
        _lockPointerText = FindButtonLabel(settings, "LockPointer");
        _targetSphereText = FindButtonLabel(settings, "TargetSphere");
        _gunRayText = FindButtonLabel(settings, "GunRay");

        _notificationsEnabled = _notificationsConfig.Value;

        var gunLib = GorillaInfoMain.Instance.gunLib;
        if (gunLib != null)
        {
            _lockOnEnabled = _lockOnConfig.Value;
            _nametagsEnabled = _nametagsConfig.Value;
            _passThroughEnabled = _passThroughConfig.Value;
            _lockPointerEnabled = _lockPointerConfig.Value;
            _targetSphereEnabled = _targetSphereConfig.Value;
            _gunRayEnabled = _gunRayConfig.Value;

            _gunStyleIndex = Mathf.Clamp(_gunStyleConfig.Value, 0, gunLib.GetGunStyleCount() - 1);
            _gunSizeIndex = Mathf.Clamp(_gunSizeConfig.Value, 0, gunLib.GetGunSizePresetCount() - 1);

            gunLib.autoLockEnabled = _lockOnEnabled;
            gunLib.nametagsEnabled = _nametagsEnabled;
            gunLib.passThroughEnabled = _passThroughEnabled;
            gunLib.lockPointerEnabled = _lockPointerEnabled;
            gunLib.SetTargetSphereEnabled(_targetSphereEnabled);
            gunLib.SetGunRayEnabled(_gunRayEnabled);
            gunLib.SetGunSizePreset(_gunSizeIndex);
            gunLib.SetGunStyle(_gunStyleIndex);
        }

        if (GorillaInfoMain.Instance.notificationManager != null)
            GorillaInfoMain.Instance.notificationManager.notificationsEnabled = _notificationsEnabled;

        UpdateAllTexts();
        GorillaInfoMain.Instance.updMain?.UpdateMainPage();
    }

    private void EnsureConfigBindings()
    {
        if (_configInitialized)
            return;

        var cfg = GorillaInfoMain.Instance.Config;
        _notificationsConfig = cfg.Bind("CheckerSettings", "NotificationsEnabled", true, "Enable notifications.");
        _lockOnConfig = cfg.Bind("CheckerSettings", "LockOnEnabled", false, "Enable lock-on mode.");
        _nametagsConfig = cfg.Bind("CheckerSettings", "NametagsEnabled", false, "Enable nametags.");
        _passThroughConfig = cfg.Bind("CheckerSettings", "PassThroughEnabled", false, "Allow pass-through target detection.");
        _gunStyleConfig = cfg.Bind("CheckerSettings", "GunStyleIndex", 0, "Current gun style index.");
        _gunSizeConfig = cfg.Bind("CheckerSettings", "GunSizeIndex", 1, "Current gun size preset index.");
        _lockPointerConfig = cfg.Bind("CheckerSettings", "LockPointerEnabled", true, "Show pointer line when lock-on is ON.");
        _targetSphereConfig = cfg.Bind("CheckerSettings", "TargetSphereEnabled", true, "Show target sphere around selected player.");
        _gunRayConfig = cfg.Bind("CheckerSettings", "GunRayEnabled", true, "Enable gun ray rendering.");
        _configInitialized = true;
    }

    public void ToggleNotifications()
    {
        _notificationsEnabled = !_notificationsEnabled;
        if (GorillaInfoMain.Instance.notificationManager != null)
            GorillaInfoMain.Instance.notificationManager.notificationsEnabled = _notificationsEnabled;
        _notificationsConfig.Value = _notificationsEnabled;
        if (_notificationsText != null)
            _notificationsText.text = _notificationsEnabled ? "Notify: ON" : "Notify: OFF";
        GorillaInfoMain.Instance.Config.Save();
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
        _lockOnConfig.Value = _lockOnEnabled;
        if (_lockOnText != null)
            _lockOnText.text = _lockOnEnabled ? "LockOn: ON" : "LockOn: OFF";
        GorillaInfoMain.Instance.Config.Save();
        GorillaInfoMain.Instance.updMain?.UpdateMainPage();
    }

    public void ToggleNametags()
    {
        _nametagsEnabled = !_nametagsEnabled;
        GorillaInfoMain.Instance.gunLib.nametagsEnabled = _nametagsEnabled;
        _nametagsConfig.Value = _nametagsEnabled;
        if (_nametagsText != null)
            _nametagsText.text = _nametagsEnabled ? "Nametags: ON" : "Nametags: OFF";
        GorillaInfoMain.Instance.Config.Save();
    }

    public void CycleGunStyle()
    {
        int styleCount = GorillaInfoMain.Instance.gunLib.GetGunStyleCount();
        _gunStyleIndex = (_gunStyleIndex + 1) % styleCount;
        _gunStyleConfig.Value = _gunStyleIndex;
        GorillaInfoMain.Instance.gunLib.SetGunStyle(_gunStyleIndex);
        if (_gunStyleText != null)
            _gunStyleText.text = $"GunStyle: {GunStyles[Mathf.Clamp(_gunStyleIndex, 0, GunStyles.Length - 1)]}";
        GorillaInfoMain.Instance.Config.Save();
    }

    public void TogglePassThroughGun()
    {
        _passThroughEnabled = !_passThroughEnabled;
        GorillaInfoMain.Instance.gunLib.passThroughEnabled = _passThroughEnabled;
        _passThroughConfig.Value = _passThroughEnabled;
        if (_passThroughText != null)
            _passThroughText.text = _passThroughEnabled ? "PassThrough: ON" : "PassThrough: OFF";
        GorillaInfoMain.Instance.Config.Save();
    }

    public void CycleGunSize()
    {
        int count = GorillaInfoMain.Instance.gunLib.GetGunSizePresetCount();
        _gunSizeIndex = (_gunSizeIndex + 1) % count;
        GorillaInfoMain.Instance.gunLib.SetGunSizePreset(_gunSizeIndex);
        _gunSizeConfig.Value = _gunSizeIndex;
        if (_gunSizeText != null)
            _gunSizeText.text = $"GunSize: {GunSizes[Mathf.Clamp(_gunSizeIndex, 0, GunSizes.Length - 1)]}";
        GorillaInfoMain.Instance.Config.Save();
    }

    public void ToggleLockPointer()
    {
        _lockPointerEnabled = !_lockPointerEnabled;
        GorillaInfoMain.Instance.gunLib.lockPointerEnabled = _lockPointerEnabled;
        _lockPointerConfig.Value = _lockPointerEnabled;
        if (_lockPointerText != null)
            _lockPointerText.text = _lockPointerEnabled ? "Pointer: ON" : "Pointer: OFF";
        GorillaInfoMain.Instance.Config.Save();
    }

    public void ToggleTargetSphere()
    {
        _targetSphereEnabled = !_targetSphereEnabled;
        GorillaInfoMain.Instance.gunLib.SetTargetSphereEnabled(_targetSphereEnabled);
        _targetSphereConfig.Value = _targetSphereEnabled;
        if (_targetSphereText != null)
            _targetSphereText.text = _targetSphereEnabled ? "TargetSphere: ON" : "TargetSphere: OFF";
        GorillaInfoMain.Instance.Config.Save();
    }

    public void ToggleGunRay()
    {
        _gunRayEnabled = !_gunRayEnabled;
        GorillaInfoMain.Instance.gunLib.SetGunRayEnabled(_gunRayEnabled);
        _gunRayConfig.Value = _gunRayEnabled;
        if (_gunRayText != null)
            _gunRayText.text = _gunRayEnabled ? "GunRay: ON" : "GunRay: OFF";
        GorillaInfoMain.Instance.Config.Save();
    }

    public void ResetDefaults()
    {
        _notificationsEnabled = true;
        _lockOnEnabled = false;
        _nametagsEnabled = false;
        _passThroughEnabled = false;
        _gunStyleIndex = 0;
        _gunSizeIndex = 1;
        _lockPointerEnabled = true;
        _targetSphereEnabled = true;
        _gunRayEnabled = true;

        _notificationsConfig.Value = _notificationsEnabled;
        _lockOnConfig.Value = _lockOnEnabled;
        _nametagsConfig.Value = _nametagsEnabled;
        _passThroughConfig.Value = _passThroughEnabled;
        _gunStyleConfig.Value = _gunStyleIndex;
        _gunSizeConfig.Value = _gunSizeIndex;
        _lockPointerConfig.Value = _lockPointerEnabled;
        _targetSphereConfig.Value = _targetSphereEnabled;
        _gunRayConfig.Value = _gunRayEnabled;

        if (GorillaInfoMain.Instance.notificationManager != null)
            GorillaInfoMain.Instance.notificationManager.notificationsEnabled = _notificationsEnabled;

        var gunLib = GorillaInfoMain.Instance.gunLib;
        gunLib.autoLockEnabled = _lockOnEnabled;
        gunLib.nametagsEnabled = _nametagsEnabled;
        gunLib.passThroughEnabled = _passThroughEnabled;
        gunLib.lockPointerEnabled = _lockPointerEnabled;
        gunLib.SetTargetSphereEnabled(_targetSphereEnabled);
        gunLib.SetGunRayEnabled(_gunRayEnabled);
        gunLib.SetGunSizePreset(_gunSizeIndex);
        gunLib.SetGunStyle(_gunStyleIndex);

        UpdateAllTexts();
        GorillaInfoMain.Instance.Config.Save();
        GorillaInfoMain.Instance.updMain?.UpdateMainPage();
    }

    private void UpdateAllTexts()
    {
        if (_notificationsText != null) _notificationsText.text = _notificationsEnabled ? "Notify: ON" : "Notify: OFF";
        if (_lockOnText != null) _lockOnText.text = _lockOnEnabled ? "LockOn: ON" : "LockOn: OFF";
        if (_nametagsText != null) _nametagsText.text = _nametagsEnabled ? "Nametags: ON" : "Nametags: OFF";
        if (_gunStyleText != null) _gunStyleText.text = $"GunStyle: {GunStyles[Mathf.Clamp(_gunStyleIndex, 0, GunStyles.Length - 1)]}";
        if (_gunSizeText != null) _gunSizeText.text = $"GunSize: {GunSizes[Mathf.Clamp(_gunSizeIndex, 0, GunSizes.Length - 1)]}";
        if (_passThroughText != null) _passThroughText.text = _passThroughEnabled ? "PassThrough: ON" : "PassThrough: OFF";
        if (_lockPointerText != null) _lockPointerText.text = _lockPointerEnabled ? "Pointer: ON" : "Pointer: OFF";
        if (_targetSphereText != null) _targetSphereText.text = _targetSphereEnabled ? "TargetSphere: ON" : "TargetSphere: OFF";
        if (_gunRayText != null) _gunRayText.text = _gunRayEnabled ? "GunRay: ON" : "GunRay: OFF";
    }
}
