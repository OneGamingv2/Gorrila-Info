using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Checker;
using GorillaInfo;

public class LobbyHandler
{
    private List<VRRig> _currentPlayers = new List<VRRig>();
    private string _cachedCode = "";
    private GameObject[] _playerSlotObjects = new GameObject[10];
    private GameObject[] _selectButtonObjects = new GameObject[10];
    private bool _initialized;
    private const int MaxPlayerSlots = 10;

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
        VRRig[] allRigs = Object.FindObjectsOfType<VRRig>();

        foreach (var rig in allRigs)
        {
            if (rig != null && rig.OwningNetPlayer != null)
                _currentPlayers.Add(rig);
        }

        for (int i = 0; i < MaxPlayerSlots; i++)
        {
            bool hasPlayer = i < _currentPlayers.Count;

            if (_playerSlotObjects[i] != null)
            {
                _playerSlotObjects[i].SetActive(hasPlayer);
                foreach (var component in _playerSlotObjects[i].GetComponentsInChildren<Behaviour>(true))
                    component.enabled = hasPlayer;
            }

            if (_selectButtonObjects[i] != null)
            {
                _selectButtonObjects[i].SetActive(hasPlayer);
                foreach (var component in _selectButtonObjects[i].GetComponentsInChildren<Behaviour>(true))
                    component.enabled = hasPlayer;
            }

            if (misc.playerNames[i] != null)
            {
                if (hasPlayer)
                {
                    string playerName = _currentPlayers[i].OwningNetPlayer.NickName;
                    if (string.IsNullOrEmpty(playerName))
                        playerName = "Player";
                    misc.playerNames[i].text = playerName;
                }
                else
                {
                    misc.playerNames[i].text = "-";
                }
            }
        }

        string roomCode = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.Name : "UNKNOWN";
        misc.txtLobbyCode.text = $"Code: {roomCode}";
    }

    public void SelectPlayer(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= _currentPlayers.Count)
            return;

        var selectedRig = _currentPlayers[playerIndex];
        if (selectedRig == null || selectedRig.OwningNetPlayer == null)
            return;

        GorillaInfoMain.Instance.gunLib.lockedTarget = selectedRig;

        var misc = GorillaInfoMain.Instance.misc;
        if (misc == null)
            return;

        if (misc.txtSelectedPlayer != null)
        {
            misc.txtSelectedPlayer.text = $"Selected Player: {selectedRig.OwningNetPlayer.NickName}";
        }

        misc.EnableMain();
    }
}
