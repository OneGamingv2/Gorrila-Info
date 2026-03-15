using UnityEngine;
using System.Collections.Generic;
using GorillaInfo;

public class Button
{
    private static readonly Dictionary<Transform, bool> _buttonTouchStates = new(16);
    private float _nextAllowedClickTime;
    private float _interactionRadius = 0.005f;
    private bool _touchLatchActive;
    private bool _anyTouchThisFrame;
    private Collider _latchedCollider;
    private const float ClickCooldown = 0.2f;
    private const float OpenClickGuardSeconds = 0.28f;
    private const float MaxButtonColliderExtent = 0.22f;
    private bool _wasMenuOpen;
    private readonly Dictionary<string, Transform> _buttonCache = new Dictionary<string, Transform>(64);
    private bool _buttonCacheBuilt;

    public void checkbuttons()
    {
        var menu = GorillaInfoMain.Instance?.menuLoader?.menuInstance;
        var sphere = GorillaInfoMain.Instance?.buttonClick?.fingerSphere;

        if (menu == null || sphere == null || GorillaInfoMain.Instance.menuState != GorillaInfoMain.MenuState.Open)
        {
            _wasMenuOpen = false;
            _touchLatchActive = false;
            _latchedCollider = null;
            _buttonTouchStates.Clear();
            _buttonCacheBuilt = false;
            return;
        }

        if (!_buttonCacheBuilt)
            BuildButtonCache(menu.transform);

        if (!_wasMenuOpen)
        {
            _wasMenuOpen = true;
            _nextAllowedClickTime = Time.time + OpenClickGuardSeconds;
            _touchLatchActive = true;
            _latchedCollider = null;
            _buttonTouchStates.Clear();
        }

        _anyTouchThisFrame = false;

        Vector3 spherePos = sphere.transform.position;
        SphereCollider sphereCollider = sphere.GetComponent<SphereCollider>();
        if (sphereCollider != null)
        {
            Vector3 lossy = sphere.transform.lossyScale;
            float scaleMax = Mathf.Max(lossy.x, Mathf.Max(lossy.y, lossy.z));
            _interactionRadius = Mathf.Clamp(sphereCollider.radius * scaleMax, 0.0035f, 0.016f);
        }

        // Release the open-guard latch as soon as the cooldown window expires.
        // Without this, if the finger is near ANY button, _anyTouchThisFrame stays
        // true every frame, _latchedCollider stays null, and the latch never clears.
        if (_touchLatchActive && _latchedCollider == null && Time.time >= _nextAllowedClickTime)
            _touchLatchActive = false;

        var inst = GorillaInfoMain.Instance;

        if (TryPressButton(GetCachedButton("HomeButton"), inst.misc.EnableMain, spherePos)) return;
        if (TryPressButton(GetCachedButton("MiscButton"), inst.misc.EnableMisc, spherePos)) return;
        if (TryPressButton(GetCachedButton("SettingsButton"), inst.misc.EnableSettings, spherePos)) return;
        if (TryPressButton(GetCachedButton("ActionsButton"), inst.misc.Enableactions, spherePos)) return;
        if (TryPressButton(GetCachedButton("LobbyButton"), inst.misc.EnableLobby, spherePos)) return;
        if (TryPressButton(GetCachedButton("MusicButton"), inst.misc.EnableMusic, spherePos)) return;

        if (inst.menuLoader.settingsPanel != null && inst.menuLoader.settingsPanel.activeInHierarchy)
        {
            if (TryPressButton(GetCachedButton("Notifications"), inst.settingsHandler.ToggleNotifications, spherePos)) return;
            if (TryPressButton(GetCachedButton("LockOn"), inst.settingsHandler.ToggleLockOn, spherePos)) return;
            if (TryPressButton(GetCachedButton("Nametags"), inst.settingsHandler.ToggleNametags, spherePos)) return;
            if (TryPressButton(GetCachedButton("GunStyle"), inst.settingsHandler.CycleGunStyle, spherePos)) return;
            if (TryPressButton(GetCachedButton("PassThroughGun"), inst.settingsHandler.TogglePassThroughGun, spherePos)) return;
            if (TryPressButton(GetCachedButton("GunSize"), inst.settingsHandler.CycleGunSize, spherePos)) return;
            if (TryPressButton(GetCachedButton("LockPointer"), inst.settingsHandler.ToggleLockPointer, spherePos)) return;
            if (TryPressButton(GetCachedButton("TargetSphere"), inst.settingsHandler.ToggleTargetSphere, spherePos)) return;
            if (TryPressButton(GetCachedButton("GunRay"), inst.settingsHandler.ToggleGunRay, spherePos)) return;
            if (TryPressButton(GetCachedButton("ResetSettings"), inst.settingsHandler.ResetDefaults, spherePos)) return;
        }

        if (inst.menuLoader.actionsPanel != null && inst.menuLoader.actionsPanel.activeInHierarchy)
        {
            if (TryPressButton(GetCachedButton("Scan Players"), inst.updMain.ScanAllPlayers, spherePos)) return;
            if (TryPressButton(GetCachedButton("LobbyHop"), inst.updMain.LobbyHop, spherePos)) return;
            if (TryPressButton(GetCachedButton("JoinPrivate"), inst.updMain.JoinPrivate, spherePos)) return;
            if (TryPressButton(GetCachedButton("Disconnect"), inst.updMain.Disconnect, spherePos)) return;
            if (TryPressButton(GetCachedButton("ClearSelection"), () =>
            {
                inst.gunLib?.ClearSelection();
                inst.updMain?.UpdateMainPage();
            }, spherePos)) return;
            if (TryPressButton(GetCachedButton("MoreInfoButton"), () => inst.moreInfoHandler?.ToggleMoreInfo(), spherePos)) return;
        }

        if (inst.menuLoader.musicPanel != null && inst.menuLoader.musicPanel.activeInHierarchy && inst.musicHandler != null)
        {
            if (TryPressButton(GetCachedButton("Previous"), inst.musicHandler.PreviousTrack, spherePos)) return;
            if (TryPressButton(GetCachedButton("PauseButton"), inst.musicHandler.PlayPauseMusic, spherePos)) return;
            if (TryPressButton(GetCachedButton("Next"), inst.musicHandler.NextTrack, spherePos)) return;
            if (TryPressButton(GetCachedButton("VolDown"), inst.musicHandler.VolumeDown, spherePos)) return;
            if (TryPressButton(GetCachedButton("Mute"), inst.musicHandler.ToggleMute, spherePos)) return;
            if (TryPressButton(GetCachedButton("VolUp"), inst.musicHandler.VolumeUp, spherePos)) return;
            if (TryPressButton(GetCachedButton("SpotifyButton"), inst.musicHandler.OpenSpotify, spherePos)) return;
            if (TryPressButton(GetCachedButton("Spotify"), inst.musicHandler.OpenSpotify, spherePos)) return;
            if (TryPressButton(GetCachedButton("YouTubeButton"), inst.musicHandler.OpenYouTube, spherePos)) return;
            if (TryPressButton(GetCachedButton("YouTube"), inst.musicHandler.OpenYouTube, spherePos)) return;
            if (TryPressButton(GetCachedButton("OpenBrowser"), inst.musicHandler.OpenCurrentInBrowser, spherePos)) return;
            if (TryPressButton(GetCachedButton("RefreshButton"), inst.musicHandler.RefreshNowPlaying, spherePos)) return;
        }

        if (inst.menuLoader.lobbyPanel != null && inst.menuLoader.lobbyPanel.activeInHierarchy)
        {
            for (int i = 0; i < 10; i++)
            {
                int playerIdx = i;
                if (TryPressButton(GetCachedButton($"SelectPlayer{i}"), () => inst.lobbyHandler?.SelectPlayer(playerIdx), spherePos)) return;
            }
        }

        if (_latchedCollider != null && !IsTouchingCollider(_latchedCollider, spherePos))
        {
            _touchLatchActive = false;
            _latchedCollider = null;
        }

        if (!_anyTouchThisFrame)
        {
            _touchLatchActive = false;
            _latchedCollider = null;
        }
    }

    private void BuildButtonCache(Transform menuRoot)
    {
        _buttonCache.Clear();

        Transform sections = FindDeepChild(menuRoot, "Sections");
        if (sections != null)
        {
            CacheButton(sections, "HomeButton", "Home");
            CacheButton(sections, "MiscButton", "Misc");
            CacheButton(sections, "SettingsButton", "Settings");
            CacheButton(sections, "ActionsButton", "Actions");
            CacheButton(sections, "LobbyButton", "Lobby");
            CacheButton(sections, "MusicButton", "Music");
        }

        var loader = GorillaInfoMain.Instance?.menuLoader;

        if (loader?.settingsPanel != null)
        {
            Transform sp = loader.settingsPanel.transform;
            CacheButton(sp, "Notifications", "Notification");
            CacheButton(sp, "LockOn", "Lock On", "Lockon", "LockOnButton");
            CacheButton(sp, "Nametags", "NameTags", "Nametag");
            CacheButton(sp, "GunStyle", "Gun Style");
            CacheButton(sp, "PassThroughGun", "PassThrough", "Pass Through");
            CacheButton(sp, "GunSize", "Gun Size");
            CacheButton(sp, "LockPointer", "Pointer");
            CacheButton(sp, "TargetSphere", "Sphere");
            CacheButton(sp, "GunRay", "Ray");
            CacheButton(sp, "ResetSettings", "Reset");
        }

        if (loader?.actionsPanel != null)
        {
            Transform ap = loader.actionsPanel.transform;
            CacheButton(ap, "Scan Players", "ScanPlayers", "ScanPlayer", "Scan");
            CacheButton(ap, "LobbyHop");
            CacheButton(ap, "JoinPrivate");
            CacheButton(ap, "Disconnect");
            CacheButton(ap, "ClearSelection", "Clear Selection", "ClearTarget");
            CacheButton(ap, "MoreInfoButton", "MoreInfo", "More Info");
        }

        if (loader?.musicPanel != null)
        {
            Transform mp = loader.musicPanel.transform;
            CacheButton(mp, "Previous", "Prev");
            CacheButton(mp, "PauseButton", "PlayPause", "Pause");
            CacheButton(mp, "Next", "NextButton");
            CacheButton(mp, "VolDown", "VolumeDown", "Vol -");
            CacheButton(mp, "Mute", "VolumeMute");
            CacheButton(mp, "VolUp", "VolumeUp", "Vol +");
            CacheButton(mp, "SpotifyButton");
            CacheButton(mp, "Spotify");
            CacheButton(mp, "YouTubeButton");
            CacheButton(mp, "YouTube");
            CacheButton(mp, "OpenBrowser");
            CacheButton(mp, "RefreshButton");
        }

        if (loader?.lobbyPanel != null)
        {
            Transform lp = loader.lobbyPanel.transform;
            for (int i = 0; i < 10; i++)
                CacheButton(lp, $"SelectPlayer{i}");
        }

        _buttonCacheBuilt = true;
    }

    private void CacheButton(Transform parent, params string[] names)
    {
        if (names == null || names.Length == 0) return;
        Transform resolved = FindAnyDeepChild(parent, names);
        if (resolved != null)
            _buttonCache[names[0]] = resolved;
    }

    private Transform GetCachedButton(string key)
    {
        _buttonCache.TryGetValue(key, out Transform t);
        return t;
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

    private Transform FindAnyDeepChild(Transform parent, params string[] names)
    {
        if (parent == null || names == null)
            return null;

        for (int i = 0; i < names.Length; i++)
        {
            Transform found = FindDeepChild(parent, names[i]);
            if (found != null)
                return ResolveButtonTransform(found);
        }

        for (int i = 0; i < names.Length; i++)
        {
            string normalizedTarget = NormalizeName(names[i]);
            Transform found = FindByNormalizedName(parent, normalizedTarget);
            if (found != null)
                return ResolveButtonTransform(found);
        }

        return null;
    }

    private Transform FindByNormalizedName(Transform parent, string normalizedName)
    {
        if (parent == null || string.IsNullOrEmpty(normalizedName))
            return null;

        if (NormalizeName(parent.name) == normalizedName)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindByNormalizedName(parent.GetChild(i), normalizedName);
            if (found != null)
                return found;
        }

        return null;
    }

    private Transform ResolveButtonTransform(Transform candidate)
    {
        if (candidate == null)
            return null;

        Collider candidateCollider = candidate.GetComponent<Collider>();
        if (IsLikelyButtonCollider(candidateCollider))
            return candidate;

        Transform parent = candidate.parent;
        if (parent != null && IsLikelyButtonCollider(parent.GetComponent<Collider>()))
            return parent;

        return null;
    }

    private bool IsLikelyButtonCollider(Collider col)
    {
        if (col == null)
            return false;

        Bounds b = col.bounds;
        if (b.extents.x > MaxButtonColliderExtent ||
            b.extents.y > MaxButtonColliderExtent ||
            b.extents.z > MaxButtonColliderExtent)
        {
            return false;
        }

        return true;
    }

    private bool IsTouchingCollider(Collider col, Vector3 spherePos)
    {
        if (col == null)
            return false;

        Vector3 closest = col.ClosestPoint(spherePos);
        return (spherePos - closest).sqrMagnitude <= (_interactionRadius * _interactionRadius);
    }

    private string NormalizeName(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Replace(" ", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
    }

    private bool TryPressButton(Transform btn, System.Action onPress, Vector3 spherePos)
    {
        Transform target = ResolveButtonTransform(btn);
        if (target == null) return false;

        Collider col = target.GetComponent<Collider>();
        if (col == null) return false;

        if (!_buttonTouchStates.TryGetValue(target, out bool wasTouching))
            wasTouching = false;

        bool touching = IsTouchingCollider(col, spherePos);
        if (touching)
            _anyTouchThisFrame = true;

        if (_touchLatchActive && _latchedCollider != null && col != _latchedCollider)
        {
            _buttonTouchStates[target] = touching;
            return false;
        }

        if (touching && !wasTouching && !_touchLatchActive && Time.time >= _nextAllowedClickTime)
        {
            // commit latch BEFORE invoking so an exception in onPress can't cause rapid re-fire
            _nextAllowedClickTime = Time.time + ClickCooldown;
            _touchLatchActive = true;
            _latchedCollider = col;
            _buttonTouchStates[target] = true;
            AudioHelper.PlaySound("CreamyClick.wav");
            try { onPress?.Invoke(); }
            catch (System.Exception ex) { UnityEngine.Debug.LogException(ex); }
            return true;
        }

        _buttonTouchStates[target] = touching;
        return false;
    }
}
