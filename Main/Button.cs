using UnityEngine;
using System.Collections.Generic;
using GorillaInfo;

public class Button
{
    private static readonly Dictionary<Transform, bool> _buttonTouchStates = new(16);
    private float _nextAllowedClickTime;
    private const float ClickCooldown = 0.2f;

    public void checkbuttons()
    {
        if (Time.time < _nextAllowedClickTime) return;

        var menu = GorillaInfoMain.Instance?.menuLoader?.menuInstance;
        var sphere = GorillaInfoMain.Instance?.buttonClick?.fingerSphere;

        if (menu == null || sphere == null || GorillaInfoMain.Instance.menuState != GorillaInfoMain.MenuState.Open)
            return;

        Transform sections = FindDeepChild(menu.transform, "Sections");
        if (sections == null) return;

        Vector3 spherePos = sphere.transform.position;

        if (TryPressButton(FindAnyDeepChild(sections, "HomeButton", "Home"), GorillaInfoMain.Instance.misc.EnableMain, spherePos)) return;
        if (TryPressButton(FindAnyDeepChild(sections, "MiscButton", "Misc"), GorillaInfoMain.Instance.misc.EnableMisc, spherePos)) return;
        if (TryPressButton(FindAnyDeepChild(sections, "SettingsButton", "Settings"), GorillaInfoMain.Instance.misc.EnableSettings, spherePos)) return;
        if (TryPressButton(FindAnyDeepChild(sections, "ActionsButton", "Actions"), GorillaInfoMain.Instance.misc.Enableactions, spherePos)) return;
        if (TryPressButton(FindAnyDeepChild(sections, "LobbyButton", "Lobby"), GorillaInfoMain.Instance.misc.EnableLobby, spherePos)) return;
        if (TryPressButton(FindAnyDeepChild(sections, "MusicButton", "Music"), GorillaInfoMain.Instance.misc.EnableMusic, spherePos)) return;

        var settingsPanel = GorillaInfoMain.Instance.menuLoader.settingsPanel;
        if (settingsPanel != null)
        {
            if (TryPressButton(FindAnyDeepChild(settingsPanel.transform, "Notifications", "Notification"), GorillaInfoMain.Instance.settingsHandler.ToggleNotifications, spherePos)) return;
            if (TryPressButton(FindAnyDeepChild(settingsPanel.transform, "LockOn", "Lock On", "Lockon", "LockOnButton"), GorillaInfoMain.Instance.settingsHandler.ToggleLockOn, spherePos)) return;
            if (TryPressButton(FindAnyDeepChild(settingsPanel.transform, "Nametags", "NameTags", "Nametag"), GorillaInfoMain.Instance.settingsHandler.ToggleNametags, spherePos)) return;
            if (TryPressButton(FindAnyDeepChild(settingsPanel.transform, "GunStyle", "Gun Style"), GorillaInfoMain.Instance.settingsHandler.CycleGunStyle, spherePos)) return;
            if (TryPressButton(FindAnyDeepChild(settingsPanel.transform, "PassThroughGun", "PassThrough", "Pass Through"), GorillaInfoMain.Instance.settingsHandler.TogglePassThroughGun, spherePos)) return;
            if (TryPressButton(FindAnyDeepChild(settingsPanel.transform, "GunSize", "Gun Size"), GorillaInfoMain.Instance.settingsHandler.CycleGunSize, spherePos)) return;
            if (TryPressButton(FindAnyDeepChild(settingsPanel.transform, "LockPointer", "Pointer"), GorillaInfoMain.Instance.settingsHandler.ToggleLockPointer, spherePos)) return;
            if (TryPressButton(FindAnyDeepChild(settingsPanel.transform, "TargetSphere", "Sphere"), GorillaInfoMain.Instance.settingsHandler.ToggleTargetSphere, spherePos)) return;
            if (TryPressButton(FindAnyDeepChild(settingsPanel.transform, "GunRay", "Ray"), GorillaInfoMain.Instance.settingsHandler.ToggleGunRay, spherePos)) return;
            if (TryPressButton(FindAnyDeepChild(settingsPanel.transform, "ResetSettings", "Reset"), GorillaInfoMain.Instance.settingsHandler.ResetDefaults, spherePos)) return;
        }

        var actionsPanel = GorillaInfoMain.Instance.menuLoader.actionsPanel;
        if (actionsPanel != null)
        {
            if (TryPressButton(FindAnyDeepChild(actionsPanel.transform, "Scan Players", "ScanPlayers", "ScanPlayer", "Scan"), GorillaInfoMain.Instance.updMain.ScanAllPlayers, spherePos)) return;
            if (TryPressButton(FindDeepChild(actionsPanel.transform, "LobbyHop"), GorillaInfoMain.Instance.updMain.LobbyHop, spherePos)) return;
            if (TryPressButton(FindDeepChild(actionsPanel.transform, "JoinPrivate"), GorillaInfoMain.Instance.updMain.JoinPrivate, spherePos)) return;
            if (TryPressButton(FindDeepChild(actionsPanel.transform, "Disconnect"), GorillaInfoMain.Instance.updMain.Disconnect, spherePos)) return;
            if (TryPressButton(FindAnyDeepChild(actionsPanel.transform, "ClearSelection", "Clear Selection", "ClearTarget"), () =>
            {
                GorillaInfoMain.Instance.gunLib?.ClearSelection();
                GorillaInfoMain.Instance.updMain?.UpdateMainPage();
            }, spherePos)) return;
            if (TryPressButton(FindAnyDeepChild(actionsPanel.transform, "MoreInfoButton", "MoreInfo", "More Info"), () => GorillaInfoMain.Instance.moreInfoHandler?.ToggleMoreInfo(), spherePos)) return;
        }

        var musicPanel = GorillaInfoMain.Instance.menuLoader.musicPanel;
        if (musicPanel != null)
        {
            if (GorillaInfoMain.Instance.musicHandler != null)
            {
                if (TryPressButton(FindDeepChild(musicPanel.transform, "Previous"), GorillaInfoMain.Instance.musicHandler.PreviousTrack, spherePos)) return;
                if (TryPressButton(FindDeepChild(musicPanel.transform, "PauseButton"), GorillaInfoMain.Instance.musicHandler.PlayPauseMusic, spherePos)) return;
                if (TryPressButton(FindDeepChild(musicPanel.transform, "Next"), GorillaInfoMain.Instance.musicHandler.NextTrack, spherePos)) return;
                if (TryPressButton(FindDeepChild(musicPanel.transform, "SpotifyButton"), GorillaInfoMain.Instance.musicHandler.OpenSpotify, spherePos)) return;
                if (TryPressButton(FindDeepChild(musicPanel.transform, "Spotify"), GorillaInfoMain.Instance.musicHandler.OpenSpotify, spherePos)) return;
                if (TryPressButton(FindDeepChild(musicPanel.transform, "YouTubeButton"), GorillaInfoMain.Instance.musicHandler.OpenYouTube, spherePos)) return;
                if (TryPressButton(FindDeepChild(musicPanel.transform, "YouTube"), GorillaInfoMain.Instance.musicHandler.OpenYouTube, spherePos)) return;
                if (TryPressButton(FindDeepChild(musicPanel.transform, "OpenBrowser"), GorillaInfoMain.Instance.musicHandler.OpenCurrentInBrowser, spherePos)) return;
                if (TryPressButton(FindDeepChild(musicPanel.transform, "RefreshButton"), GorillaInfoMain.Instance.musicHandler.RefreshNowPlaying, spherePos)) return;
            }
        }

        var lobbyPanel = GorillaInfoMain.Instance.menuLoader.lobbyPanel;
        if (lobbyPanel != null)
        {
            for (int i = 0; i < 10; i++)
            {
                Transform selectBtn = FindDeepChild(lobbyPanel.transform, $"SelectPlayer{i}");
                if (selectBtn != null)
                {
                    int playerIdx = i;
                    if (TryPressButton(selectBtn, () => GorillaInfoMain.Instance.lobbyHandler?.SelectPlayer(playerIdx), spherePos)) return;
                }
            }
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

    private Transform FindAnyDeepChild(Transform parent, params string[] names)
    {
        if (parent == null || names == null)
            return null;

        for (int i = 0; i < names.Length; i++)
        {
            Transform found = FindDeepChild(parent, names[i]);
            if (found != null)
                return found;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            string normalizedChildName = NormalizeName(child.name);

            for (int n = 0; n < names.Length; n++)
            {
                string normalizedTarget = NormalizeName(names[n]);
                if (normalizedChildName.Contains(normalizedTarget) || normalizedTarget.Contains(normalizedChildName))
                    return child;
            }

            Transform recursive = FindAnyDeepChild(child, names);
            if (recursive != null)
                return recursive;
        }

        return null;
    }

    private string NormalizeName(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Replace(" ", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
    }

    private bool TryPressButton(Transform btn, System.Action onPress, Vector3 spherePos)
    {
        if (btn == null) return false;

        Collider col = btn.GetComponent<Collider>() ?? btn.GetComponentInChildren<Collider>();
        if (col == null) return false;

        if (!_buttonTouchStates.TryGetValue(btn, out bool wasTouching))
            wasTouching = false;

        bool touching = col.bounds.Contains(spherePos);

        if (touching && !wasTouching)
        {
            AudioHelper.PlaySound("CreamyClick.wav");
            onPress?.Invoke();
            _nextAllowedClickTime = Time.time + ClickCooldown;
            _buttonTouchStates[btn] = true;
            return true;
        }

        _buttonTouchStates[btn] = touching;
        return false;
    }
}
