using Checker;
using GorillaInfo;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using GorillaNetworking;

public class MainHandler
{
    private VRRig _lastRig;
    private string _cachedName;
    private string _cachedPlatform;
    private string _cachedFps;
    private string _cachedColor;
    private bool _creationDateRequested;
    private float _fpsTimer;
    private bool _scanRunning;

    private const string NoPlayer = "No player selected";
    private const string Dash = "-";
    private const string NoMods = "No mods detected";
    private const string FpsSuffix = " FPS";
    private const string WorldScalePrefix = "World Scale: ";

    public void UpdateMainPage()
    {
        var info = GorillaInfoMain.Instance;
        var misc = info.misc;
        var rig = info.gunLib.lockedTarget;

        if (rig == null)
        {
            if (_lastRig != null)
            {
                misc.txtName.text = NoPlayer;
                misc.txtPlatform.text = Dash;
                misc.txtFps.text = Dash;
                misc.txtColor.text = Dash;
                misc.txtPing.text = Dash;
                misc.txtCreationDate.text = Dash;
                misc.SetMods(new List<string>());

                ClearCache();
            }
            return;
        }

        var netPlayer = rig.OwningNetPlayer;
        if (netPlayer == null) return;

        bool targetChanged = rig != _lastRig;
        _lastRig = rig;

        if (targetChanged)
        {
            _cachedName = netPlayer.NickName;
            misc.txtName.text = _cachedName;

            if (misc.txtSelectedPlayer != null)
                misc.txtSelectedPlayer.text = $"Selected Player: {_cachedName}";
        }
        if (targetChanged)
        {
            _cachedPlatform = rig.GetPlatform().ParsePlatform();
            misc.txtPlatform.text = _cachedPlatform;
        }

        string fps = string.Concat(rig.GetFPS().ToString(), FpsSuffix);

        if (targetChanged || fps != _cachedFps)
        {
            _cachedFps = fps;
            misc.txtFps.text = fps;
        }

        string color = rig.GetColor().ParseColor();
        if (color != _cachedColor)
        {
            _cachedColor = color;
            misc.txtColor.text = color;
        }

        float worldScale = WorldScaleResolver.GetWorldScale(rig) * 100f;
        if (misc.txtPing != null)
            misc.txtPing.text = $"{WorldScalePrefix}{worldScale:F0}%";

        if (targetChanged)
        {
            _creationDateRequested = false;
        }

        if (!_creationDateRequested)
        {
            _creationDateRequested = true;
            string userId = netPlayer.UserId;

            misc.txtCreationDate.text =
                Extensions.GetCreationDate(
                    userId,
                    (date) =>
                    {
                        if (misc.txtCreationDate != null &&
                            info.gunLib.lockedTarget == rig)
                        {
                            misc.txtCreationDate.text = date;
                        }
                    });
        }

        if (targetChanged)
        {
            List<string> mods = info.utilities.DetectAllMods(rig);
            misc.SetMods(mods);
        }
    }

    private void ClearCache()
    {
        _lastRig = null;
        _cachedName = null;
        _cachedPlatform = null;
        _cachedFps = null;
        _cachedColor = null;
        _creationDateRequested = false;
    }

    public void ScanAllPlayers()
    {
        if (_scanRunning)
        {
            GorillaInfoMain.Instance.misc.Notify("<color=#FFFF00>Scan already running</color>");
            return;
        }

        if (!GorillaInfoMain.Instance.notificationManager.notificationsEnabled)
        {
            GorillaInfoMain.Instance.misc.Notify("<color=#FF0000>Notifications are Disabled</color>");
            return;
        }

        GorillaInfoMain.Instance.StartCoroutine(ScanPlayersCoroutine());
    }

    public void LobbyHop()
    {
        GorillaNetworkJoinTrigger trigger = PhotonNetworkController.Instance.currentJoinTrigger ?? GorillaComputer.instance.GetJoinTriggerForZone("forest");
        PhotonNetworkController.Instance.AttemptToJoinPublicRoom(trigger);
    }

    public void Disconnect()
    {
        NetworkSystem.Instance.ReturnToSinglePlayer();
    }

    public void JoinPrivate()
    {
        string roomName = NetworkSystem.Instance.GetMyNickName().ToUpper();

        if (roomName.Length > 6)
            roomName = roomName[..6];

        roomName += UnityEngine.Random.Range(0, 9999).ToString().PadLeft(4);

        PhotonNetworkController.Instance.AttemptToJoinSpecificRoom(roomName, JoinType.Solo);
    }
    private IEnumerator ScanPlayersCoroutine()
    {
        _scanRunning = true;
        try
        {
            yield return new WaitForSeconds(0.3f);

            VRRig[] allRigs = Object.FindObjectsOfType<VRRig>();

            if (allRigs == null || allRigs.Length == 0)
            {
                GorillaInfoMain.Instance.misc.Notify("<color=#FFFF00>No players found in lobby</color>");
                yield break;
            }

            GorillaInfoMain.Instance.misc.Notify($"<color=#00FFFF>Scanning {allRigs.Length} players...</color>");
            yield return new WaitForSeconds(0.5f);

            foreach (var rig in allRigs)
            {
                if (rig == null || rig.OwningNetPlayer == null)
                    continue;

                string playerName = rig.OwningNetPlayer.NickName;
                List<string> mods = GorillaInfoMain.Instance.utilities.DetectAllMods(rig);

                if (mods != null && mods.Count > 0)
                {
                    foreach (var mod in mods)
                    {
                        if (!string.IsNullOrEmpty(mod))
                        {
                            GorillaInfoMain.Instance.misc.Notify($"<color=#FFD700>{playerName}</color> has <color=#00FF00>{mod}</color>");
                            yield return new WaitForSeconds(0.2f);
                        }
                    }
                }
                else
                {
                    GorillaInfoMain.Instance.misc.Notify($"<color=#FFD700>{playerName}</color> - <color=#808080>No mods</color>");
                    yield return new WaitForSeconds(0.2f);
                }
            }

            GorillaInfoMain.Instance.misc.Notify("<color=#00FF00>Scan Complete</color>");
        }
        finally
        {
            _scanRunning = false;
        }
    }
}
