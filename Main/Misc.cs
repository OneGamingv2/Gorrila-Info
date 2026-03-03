using System.Collections.Generic;
using UnityEngine;

namespace GorillaInfo
{
    public class Misc
    {
        public TextMesh txtName, txtPlatform, txtFps, txtPing, txtColor, txtCreationDate, txtLockOn, txtSelectedPlayer;
        public TextMesh[] playerNames = new TextMesh[10];
        public TextMesh txtLobbyCode;
        public ModDisplay modDisplay;

        public void initmaintexts()
        {
            Transform main = GorillaInfoMain.Instance.menuLoader.mainPanel?.transform;
            if (main == null) return;

            txtName = main.Find("Name")?.GetComponent<TextMesh>();
            txtPlatform = main.Find("Platform")?.GetComponent<TextMesh>();
            txtFps = main.Find("Fps")?.GetComponent<TextMesh>();
            txtPing = main.Find("Ping")?.GetComponent<TextMesh>();
            txtColor = main.Find("Color Code")?.GetComponent<TextMesh>();
            txtCreationDate = main.Find("Creation Date")?.GetComponent<TextMesh>();
        }

        public void initmisctexts()
        {
            Transform misc = GorillaInfoMain.Instance.menuLoader.miscPanel?.transform;
            if (misc == null) return;

            modDisplay = new ModDisplay();
            modDisplay.Initialize(GorillaInfoMain.Instance.menuLoader.miscPanel);
        }

        public void InitactionsTexts()
        {
            Transform actions = GorillaInfoMain.Instance.menuLoader.actionsPanel?.transform;
            if (actions == null) return;

            txtSelectedPlayer = actions.Find("SelectedPlayer")?.GetComponent<TextMesh>();
            if (txtSelectedPlayer != null)
                txtSelectedPlayer.text = "Selected Player: None";
        }

        public void InitLobbyTexts()
        {
            Transform lobby = GorillaInfoMain.Instance.menuLoader.lobbyPanel?.transform;
            if (lobby == null) return;

            txtLobbyCode = lobby.Find("Code")?.GetComponent<TextMesh>();
            if (txtLobbyCode != null)
                txtLobbyCode.text = "Code: -";

            for (int i = 0; i < 10; i++)
            {
                playerNames[i] = lobby.Find($"PlayerName{i}")?.GetComponent<TextMesh>();
                if (playerNames[i] != null)
                    playerNames[i].text = "-";
            }
        }

        private void SetPanel(bool m, bool mi, bool s, bool mu, bool l, bool music)
        {
            if (GorillaInfoMain.Instance.menuLoader.mainPanel != null)
                GorillaInfoMain.Instance.menuLoader.mainPanel.SetActive(m);
            if (GorillaInfoMain.Instance.menuLoader.miscPanel != null)
                GorillaInfoMain.Instance.menuLoader.miscPanel.SetActive(mi);
            if (GorillaInfoMain.Instance.menuLoader.settingsPanel != null)
                GorillaInfoMain.Instance.menuLoader.settingsPanel.SetActive(s);
            if (GorillaInfoMain.Instance.menuLoader.actionsPanel != null)
                GorillaInfoMain.Instance.menuLoader.actionsPanel.SetActive(mu);
            if (GorillaInfoMain.Instance.menuLoader.lobbyPanel != null)
                GorillaInfoMain.Instance.menuLoader.lobbyPanel.SetActive(l);
            if (GorillaInfoMain.Instance.menuLoader.musicPanel != null)
                GorillaInfoMain.Instance.menuLoader.musicPanel.SetActive(music);
        }

        public void EnableMain() => SetPanel(true, false, false, false, false, false);
        public void EnableMisc() => SetPanel(false, true, false, false, false, false);
        public void EnableSettings() => SetPanel(false, false, true, false, false, false);
        public void Enableactions() => SetPanel(false, false, false, true, false, false);
        public void EnableLobby() => SetPanel(false, false, false, false, true, false);
        public void EnableMusic() => SetPanel(false, false, false, false, false, true);

        public void SetMods(List<string> mods) => modDisplay?.SetMods(mods);
        public void UpdateModDisplay() => modDisplay?.Update();

        public void Notify(string message)
        {
            if (GorillaInfoMain.Instance?.notificationManager != null)
                GorillaInfoMain.Instance.notificationManager.Notify(message);
        }
    }
}
