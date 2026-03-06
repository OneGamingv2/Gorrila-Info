using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Checker;
using GorillaInfo;
using System;

public class LobbyHandler
{
    private readonly List<VRRig> _currentPlayers = new List<VRRig>(10);
    private readonly VRRig[] _slotToRig = new VRRig[10];
    private readonly string[] _lastDisplayedNames = new string[10];
    private string _cachedCode = "";
    private readonly GameObject[] _playerSlotObjects = new GameObject[10];
    private readonly GameObject[] _selectButtonObjects = new GameObject[10];
    private readonly Behaviour[][] _playerSlotBehaviours = new Behaviour[10][];
    private readonly Behaviour[][] _selectButtonBehaviours = new Behaviour[10][];
    private bool _initialized;
    private const int MaxPlayerSlots = 10;
    private static readonly Comparison<VRRig> PlayerSort = ComparePlayers;

    private bool TryGetLobbyTransform(out Transform lobby)
    {
        lobby = null;

        var main = GorillaInfoMain.Instance;
        if (main == null || main.menuLoader == null)
            return false;

        GameObject lobbyPanel = main.menuLoader.lobbyPanel;
        if (lobbyPanel == null)
            return false;

        lobby = lobbyPanel.transform;
        return lobby != null;
    }

    public bool InitializeLobbySlots()
    {
        if (!TryGetLobbyTransform(out Transform lobby))
            return false;

        for (int i = 0; i < MaxPlayerSlots; i++)
        {
            _playerSlotObjects[i] = lobby.Find($"PlayerName{i}")?.gameObject;
            _selectButtonObjects[i] = lobby.Find($"SelectPlayer{i}")?.gameObject;
            _playerSlotBehaviours[i] = _playerSlotObjects[i] != null ? _playerSlotObjects[i].GetComponentsInChildren<Behaviour>(true) : null;
            _selectButtonBehaviours[i] = _selectButtonObjects[i] != null ? _selectButtonObjects[i].GetComponentsInChildren<Behaviour>(true) : null;
            _lastDisplayedNames[i] = null;
        }

        _initialized = true;
        return true;
    }

    public void UpdateLobby()
    {
        var misc = GorillaInfoMain.Instance.misc;
        if (misc == null)
            return;

        if (!_initialized)
        {
            if (!TryGetLobbyTransform(out _))
                return;

            misc.InitLobbyTexts();
            if (!InitializeLobbySlots())
                return;
        }

        if (misc.playerNames == null || misc.txtLobbyCode == null)
            return;

        _currentPlayers.Clear();
        VRRig[] allRigs = UnityEngine.Object.FindObjectsByType<VRRig>(FindObjectsSortMode.None);

        foreach (var rig in allRigs)
        {
            if (rig == null || rig == GorillaTagger.Instance?.offlineVRRig)
                continue;

            if (rig.Creator?.GetPlayerRef() != null)
                _currentPlayers.Add(rig);
        }

        _currentPlayers.Sort(PlayerSort);

        for (int i = 0; i < MaxPlayerSlots; i++)
        {
            bool hasPlayer = i < _currentPlayers.Count;
            _slotToRig[i] = hasPlayer ? _currentPlayers[i] : null;

            if (_playerSlotObjects[i] != null)
            {
                if (_playerSlotObjects[i].activeSelf != hasPlayer)
                {
                    _playerSlotObjects[i].SetActive(hasPlayer);
                    SetBehavioursEnabled(_playerSlotBehaviours[i], hasPlayer);
                }
            }

            if (_selectButtonObjects[i] != null)
            {
                if (_selectButtonObjects[i].activeSelf != hasPlayer)
                {
                    _selectButtonObjects[i].SetActive(hasPlayer);
                    SetBehavioursEnabled(_selectButtonBehaviours[i], hasPlayer);
                }
            }

            if (misc.playerNames[i] != null)
            {
                if (hasPlayer)
                {
                    string playerName = GetRigName(_currentPlayers[i]);
                    if (string.IsNullOrEmpty(playerName))
                        playerName = "Player";

                    if (!string.Equals(_lastDisplayedNames[i], playerName))
                    {
                        misc.playerNames[i].text = playerName;
                        _lastDisplayedNames[i] = playerName;
                    }
                }
                else
                {
                    if (_lastDisplayedNames[i] != "-")
                    {
                        misc.playerNames[i].text = "-";
                        _lastDisplayedNames[i] = "-";
                    }
                }
            }
        }

        string roomCode = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.Name : "UNKNOWN";
        if (!string.Equals(_cachedCode, roomCode))
        {
            _cachedCode = roomCode;
            misc.txtLobbyCode.text = $"Code: {roomCode}";
        }
    }

    private static void SetBehavioursEnabled(Behaviour[] components, bool enabled)
    {
        if (components == null)
            return;

        for (int i = 0; i < components.Length; i++)
        {
            Behaviour component = components[i];
            if (component != null)
                component.enabled = enabled;
        }
    }

    public void SelectPlayer(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= MaxPlayerSlots)
            return;

        var selectedRig = ResolveRigForSlot(playerIndex);
        if (selectedRig == null)
            return;

        if (!GorillaInfoMain.Instance.gunLib.TrySetLockedTarget(selectedRig, false))
            return;

        var misc = GorillaInfoMain.Instance.misc;
        if (misc == null)
            return;

        GorillaInfoMain.Instance.updMain?.UpdateMainPage();

        misc.EnableMain();
    }

    private VRRig ResolveRigForSlot(int playerIndex)
    {
        VRRig cachedRig = _slotToRig[playerIndex];
        if (cachedRig != null && cachedRig != GorillaTagger.Instance?.offlineVRRig && cachedRig.Creator?.GetPlayerRef() != null)
            return cachedRig;

        List<VRRig> liveRigs = new List<VRRig>(MaxPlayerSlots);
        VRRig[] allRigs = UnityEngine.Object.FindObjectsByType<VRRig>(FindObjectsSortMode.None);
        for (int i = 0; i < allRigs.Length; i++)
        {
            VRRig rig = allRigs[i];
            if (rig == null || rig == GorillaTagger.Instance?.offlineVRRig)
                continue;

            if (rig.Creator?.GetPlayerRef() != null)
                liveRigs.Add(rig);
        }

        liveRigs.Sort(PlayerSort);
        if (playerIndex < 0 || playerIndex >= liveRigs.Count)
            return null;

        return liveRigs[playerIndex];
    }

    private static int ComparePlayers(VRRig a, VRRig b)
    {
        string nameA = GetRigName(a);
        string nameB = GetRigName(b);
        int cmp = string.Compare(nameA, nameB, StringComparison.OrdinalIgnoreCase);
        if (cmp != 0)
            return cmp;

        int actorA = a?.Creator?.GetPlayerRef()?.ActorNumber ?? int.MaxValue;
        int actorB = b?.Creator?.GetPlayerRef()?.ActorNumber ?? int.MaxValue;
        return actorA.CompareTo(actorB);
    }

    private static string GetRigName(VRRig rig)
    {
        return rig?.Creator?.GetPlayerRef()?.NickName ?? "Player";
    }
}
