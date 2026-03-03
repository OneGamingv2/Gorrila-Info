using UnityEngine;
using GorillaInfo.LAB;

namespace GorillaInfo
{
    public class MenuLoader
    {
        public GameObject menuInstance, mainPanel, miscPanel, settingsPanel, actionsPanel, lobbyPanel, musicPanel;
        private Transform handAnchor;
        private const float PositionX = 0.315f;
        private const float PositionY = 0.055f;
        private const float PositionZ = -0.035f;
        private const string VersionLabel = "v1.0.2 FREE";
        private static readonly Color PanelColor = new Color(0.10f, 0.10f, 0.12f);
        private static readonly Color RowColor = new Color(0.16f, 0.16f, 0.18f);
        private static readonly Color ButtonColor = new Color(0.22f, 0.22f, 0.26f);
        private static readonly Color SideButtonColor = new Color(0.18f, 0.18f, 0.22f);

        public void loadmenu()
        {
            if (menuInstance != null)
                return;

            GameObject prefab = GorillaInfo.LAB.Assets.LoadAsset<GameObject>("gorillainfopad");
            if (prefab == null)
            {
                GorillaInfoMain.Instance?.notificationManager?.Notify(" [GorillaInfo] gorillainfopad prefab is NULL ");
                return;
            }

            menuInstance = Object.Instantiate(prefab);
            Transform hand = GorillaTagger.Instance.leftHandTransform;
            if (hand == null)
            {
                GorillaInfoMain.Instance?.notificationManager?.Notify(" [GorillaInfo] Left hand transform is NULL ");
                Object.Destroy(menuInstance);
                menuInstance = null;
                return;
            }

            GameObject anchorObj = new GameObject("GorillaInfoHandAnchor");
            handAnchor = anchorObj.transform;
            handAnchor.SetParent(hand, false);
            handAnchor.localPosition = new Vector3(PositionX, PositionY, PositionZ);
            handAnchor.localRotation = Quaternion.Euler(0f, 90f, 90f);
            handAnchor.localScale = Vector3.one;

            menuInstance.transform.SetParent(handAnchor, false);
            menuInstance.transform.localPosition = Vector3.zero;
            menuInstance.transform.localRotation = Quaternion.identity;
            menuInstance.transform.localScale = Vector3.zero;

            if (!BindMenuPanels(menuInstance.transform))
            {
                RebuildCustomGui(menuInstance.transform);
                BindMenuPanels(menuInstance.transform);
            }

            ApplyVersionLabel(menuInstance.transform);

            GorillaInfoMain main = GorillaInfoMain.Instance;
            main.buttonClick = menuInstance.AddComponent<ButtonClick>();
            main.buttonClick.ball();
            main.misc.initmaintexts();
            main.misc.initmisctexts();
            main.misc.EnableMain();
            main.settingsHandler?.InitializeSettings();
            main.moreInfoHandler?.Initialize();
            main.musicHandler?.Initialize();
        }

        private void RebuildCustomGui(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
                DestroyObject(root.GetChild(i).gameObject);

            CreateSections(root);
            CreateMainTab(root);
            CreateMiscTab(root);
            CreateSettingsTab(root);
            CreateActionsTab(root);
            CreateLobbyTab(root);
            CreateMusicTab(root);
            CreateMoreInfo(root);
        }

        private bool BindMenuPanels(Transform root)
        {
            mainPanel = FindDeepChild(root, "MainTab")?.gameObject;
            miscPanel = FindDeepChild(root, "MiscTab")?.gameObject;
            settingsPanel = FindDeepChild(root, "SettingsTab")?.gameObject;
            actionsPanel = FindDeepChild(root, "ActionsTab")?.gameObject;
            lobbyPanel = FindDeepChild(root, "LobbyTab")?.gameObject;
            musicPanel = FindDeepChild(root, "MusicTab")?.gameObject;

            return mainPanel != null &&
                   miscPanel != null &&
                   settingsPanel != null &&
                   actionsPanel != null &&
                   lobbyPanel != null &&
                   musicPanel != null;
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

        private void CreateSections(Transform root)
        {
            Transform sections = CreateContainer(root, "Sections", new Vector3(-0.34f, 0f, 0f));
            string[] names = { "HomeButton", "MiscButton", "SettingsButton", "ActionsButton", "LobbyButton", "MusicButton" };
            string[] labels = { "HOME", "MISC", "SETTINGS", "ACTIONS", "LOBBY", "MUSIC" };

            for (int i = 0; i < names.Length; i++)
            {
                CreateButton(sections, names[i], labels[i], new Vector3(0f, 0.16f - (i * 0.065f), 0f), new Vector3(0.16f, 0.048f, 0.012f), SideButtonColor);
            }
        }

        private void CreateMainTab(Transform root)
        {
            Transform tab = CreatePanel(root, "MainTab", new Vector3(0f, 0f, 0f), new Vector3(0.52f, 0.38f, 0.012f), PanelColor);
            CreateText(tab, "MainHeader", $"GORILLAINFO {VersionLabel}", new Vector3(0f, 0.17f, -0.01f), 0.022f, TextAnchor.MiddleCenter);
            CreateText(tab, "Name", "No player selected", new Vector3(-0.22f, 0.145f, -0.01f), 0.03f, TextAnchor.MiddleLeft);
            CreateText(tab, "Platform", "-", new Vector3(-0.22f, 0.095f, -0.01f), 0.03f, TextAnchor.MiddleLeft);
            CreateText(tab, "Fps", "-", new Vector3(-0.22f, 0.045f, -0.01f), 0.03f, TextAnchor.MiddleLeft);
            CreateText(tab, "Ping", "-", new Vector3(-0.22f, -0.005f, -0.01f), 0.03f, TextAnchor.MiddleLeft);
            CreateText(tab, "Color Code", "-", new Vector3(-0.22f, -0.055f, -0.01f), 0.03f, TextAnchor.MiddleLeft);
            CreateText(tab, "Creation Date", "-", new Vector3(-0.22f, -0.105f, -0.01f), 0.03f, TextAnchor.MiddleLeft);
        }

        private void ApplyVersionLabel(Transform root)
        {
            if (root == null)
                return;

            TextMesh[] texts = root.GetComponentsInChildren<TextMesh>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TextMesh text = texts[i];
                if (text == null || string.IsNullOrEmpty(text.text))
                    continue;

                if (text.text == "Gorilla Info Paid v.1.0.1" || text.text == "GORILLAINFO FREE" || text.text == "Gorilla Info Paid v.1.0.0")
                {
                    text.text = $"Gorilla Info {VersionLabel}";
                    continue;
                }

                if (text.text.Contains("Paid v.1.0.1"))
                {
                    text.text = text.text.Replace("Paid v.1.0.1", VersionLabel);
                    continue;
                }

                if (text.text.Contains("| FREE"))
                    text.text = text.text.Replace("| FREE", $"| {VersionLabel}");
            }
        }

        private void CreateMiscTab(Transform root)
        {
            Transform tab = CreatePanel(root, "MiscTab", new Vector3(0f, 0f, 0f), new Vector3(0.52f, 0.38f, 0.012f), PanelColor);
            CreateText(tab, "MiscHeader", "MOD SCANNER", new Vector3(0f, 0.17f, -0.01f), 0.022f, TextAnchor.MiddleCenter);
            CreateText(tab, "UserHas", "User has 0 Mods", new Vector3(-0.22f, 0.145f, -0.01f), 0.03f, TextAnchor.MiddleLeft);

            float startY = 0.09f;
            for (int i = 0; i < 8; i++)
            {
                Transform row = CreatePanel(tab, $"ModShowThing{i + 1}", new Vector3(-0.03f, startY - (i * 0.034f), 0f), new Vector3(0.40f, 0.028f, 0.008f), RowColor);
                CreateText(row, $"Mod{i + 1}", "-", new Vector3(-0.185f, 0f, -0.01f), 0.022f, TextAnchor.MiddleLeft);
            }

            CreateButton(tab, "PrevButton", "<", new Vector3(0.18f, -0.145f, 0f), new Vector3(0.05f, 0.04f, 0.012f), ButtonColor);
            CreateButton(tab, "NextButton", ">", new Vector3(0.24f, -0.145f, 0f), new Vector3(0.05f, 0.04f, 0.012f), ButtonColor);
        }

        private void CreateSettingsTab(Transform root)
        {
            Transform tab = CreatePanel(root, "SettingsTab", new Vector3(0f, 0f, 0f), new Vector3(0.52f, 0.38f, 0.012f), PanelColor);
            CreateText(tab, "SettingsHeader", "SETTINGS", new Vector3(0f, 0.17f, -0.01f), 0.022f, TextAnchor.MiddleCenter);
            CreateButton(tab, "Notifications", "Notifications: ON", new Vector3(0f, 0.12f, 0f), new Vector3(0.35f, 0.05f, 0.012f), ButtonColor);
            CreateButton(tab, "LockOn", "LockOn: OFF", new Vector3(0f, 0.05f, 0f), new Vector3(0.35f, 0.05f, 0.012f), ButtonColor);
            CreateButton(tab, "Nametags", "Nametags: OFF", new Vector3(0f, -0.02f, 0f), new Vector3(0.35f, 0.05f, 0.012f), ButtonColor);
            CreateButton(tab, "GunStyle", "GunStyle: Purple", new Vector3(0f, -0.09f, 0f), new Vector3(0.35f, 0.05f, 0.012f), ButtonColor);
            CreateButton(tab, "PassThroughGun", "PassThrough: OFF", new Vector3(0f, -0.16f, 0f), new Vector3(0.35f, 0.05f, 0.012f), ButtonColor);
        }

        private void CreateActionsTab(Transform root)
        {
            Transform tab = CreatePanel(root, "ActionsTab", new Vector3(0f, 0f, 0f), new Vector3(0.52f, 0.38f, 0.012f), PanelColor);
            CreateText(tab, "ActionsHeader", "ACTIONS", new Vector3(0f, 0.17f, -0.01f), 0.022f, TextAnchor.MiddleCenter);
            CreateText(tab, "SelectedPlayer", "Selected Player: None | FREE", new Vector3(-0.22f, 0.15f, -0.01f), 0.027f, TextAnchor.MiddleLeft);
            CreateButton(tab, "Scan Players", "Scan Players", new Vector3(0f, 0.08f, 0f), new Vector3(0.34f, 0.048f, 0.012f), ButtonColor);
            CreateButton(tab, "LobbyHop", "Lobby Hop", new Vector3(0f, 0.02f, 0f), new Vector3(0.34f, 0.048f, 0.012f), ButtonColor);
            CreateButton(tab, "JoinPrivate", "Join Private", new Vector3(0f, -0.04f, 0f), new Vector3(0.34f, 0.048f, 0.012f), ButtonColor);
            CreateButton(tab, "Disconnect", "Disconnect", new Vector3(0f, -0.10f, 0f), new Vector3(0.34f, 0.048f, 0.012f), ButtonColor);
            CreateButton(tab, "MoreInfoButton", "More Info", new Vector3(0f, -0.16f, 0f), new Vector3(0.34f, 0.048f, 0.012f), ButtonColor);
        }

        private void CreateLobbyTab(Transform root)
        {
            Transform tab = CreatePanel(root, "LobbyTab", new Vector3(0f, 0f, 0f), new Vector3(0.52f, 0.38f, 0.012f), PanelColor);
            CreateText(tab, "LobbyHeader", "LOBBY", new Vector3(0f, 0.17f, -0.01f), 0.022f, TextAnchor.MiddleCenter);
            CreateText(tab, "Code", "Code: -", new Vector3(-0.22f, 0.15f, -0.01f), 0.027f, TextAnchor.MiddleLeft);

            for (int i = 0; i < 10; i++)
            {
                float y = 0.115f - (i * 0.03f);
                CreateText(tab, $"PlayerName{i}", "-", new Vector3(-0.22f, y, -0.01f), 0.022f, TextAnchor.MiddleLeft);
                CreateButton(tab, $"SelectPlayer{i}", "Select", new Vector3(0.20f, y, 0f), new Vector3(0.09f, 0.024f, 0.01f), ButtonColor);
            }
        }

        private void CreateMusicTab(Transform root)
        {
            Transform tab = CreatePanel(root, "MusicTab", new Vector3(0f, 0f, 0f), new Vector3(0.52f, 0.38f, 0.012f), PanelColor);
            CreateText(tab, "MusicHeader", "MUSIC", new Vector3(0f, 0.17f, -0.01f), 0.022f, TextAnchor.MiddleCenter);
            CreateText(tab, "SongTitle", "No media detected", new Vector3(-0.22f, 0.14f, -0.01f), 0.03f, TextAnchor.MiddleLeft);
            CreateText(tab, "SongArtist", "-", new Vector3(-0.22f, 0.09f, -0.01f), 0.024f, TextAnchor.MiddleLeft);

            CreateButton(tab, "Previous", "Prev", new Vector3(-0.17f, -0.12f, 0f), new Vector3(0.10f, 0.05f, 0.012f), ButtonColor);
            CreateButton(tab, "PauseButton", "Play/Pause", new Vector3(-0.03f, -0.12f, 0f), new Vector3(0.14f, 0.05f, 0.012f), ButtonColor);
            CreateButton(tab, "Next", "Next", new Vector3(0.13f, -0.12f, 0f), new Vector3(0.10f, 0.05f, 0.012f), ButtonColor);

            CreateButton(tab, "SpotifyButton", "Spotify", new Vector3(-0.17f, -0.05f, 0f), new Vector3(0.10f, 0.045f, 0.012f), ButtonColor);
            CreateButton(tab, "YouTubeButton", "YouTube", new Vector3(-0.04f, -0.05f, 0f), new Vector3(0.10f, 0.045f, 0.012f), ButtonColor);
            CreateButton(tab, "OpenBrowser", "Open", new Vector3(0.09f, -0.05f, 0f), new Vector3(0.08f, 0.045f, 0.012f), ButtonColor);
            CreateButton(tab, "RefreshButton", "Refresh", new Vector3(0.20f, -0.05f, 0f), new Vector3(0.09f, 0.045f, 0.012f), ButtonColor);
        }

        private void CreateMoreInfo(Transform root)
        {
            Transform panel = CreatePanel(root, "MoreInfo", new Vector3(0.58f, 0f, 0f), new Vector3(0.28f, 0.22f, 0.012f), new Color(0.12f, 0.12f, 0.14f));
            CreateText(panel, "Name", "Unknown", new Vector3(0f, 0.07f, -0.01f), 0.026f, TextAnchor.MiddleCenter);
            CreateText(panel, "Speed", "Speed: 0.00 m/s", new Vector3(0f, 0.02f, -0.01f), 0.022f, TextAnchor.MiddleCenter);

            Transform model = CreateContainer(panel, "GIModel", new Vector3(0f, -0.055f, 0f));
            Transform object14 = CreatePanel(model, "Object_14", Vector3.zero, new Vector3(0.06f, 0.06f, 0.06f), Color.white);
            object14.localScale = new Vector3(0.06f, 0.06f, 0.06f);

            panel.gameObject.SetActive(false);
        }

        private Transform CreateContainer(Transform parent, string name, Vector3 localPosition)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = localPosition;
            obj.transform.localRotation = Quaternion.identity;
            obj.transform.localScale = Vector3.one;
            return obj.transform;
        }

        private Transform CreatePanel(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color)
        {
            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = name;
            panel.transform.SetParent(parent, false);
            panel.transform.localPosition = localPosition;
            panel.transform.localRotation = Quaternion.identity;
            panel.transform.localScale = localScale;

            Renderer renderer = panel.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
                renderer.material = new Material(shader);
                renderer.material.color = color;
            }

            Collider col = panel.GetComponent<Collider>();
            if (col != null)
                DestroyObject(col);

            return panel.transform;
        }

        private void CreateButton(Transform parent, string name, string label, Vector3 localPosition, Vector3 localScale, Color color)
        {
            GameObject btn = GameObject.CreatePrimitive(PrimitiveType.Cube);
            btn.name = name;
            btn.tag = "Untagged";
            btn.transform.SetParent(parent, false);
            btn.transform.localPosition = localPosition;
            btn.transform.localRotation = Quaternion.identity;
            btn.transform.localScale = localScale;

            Renderer renderer = btn.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
                renderer.material = new Material(shader);
                renderer.material.color = color;
            }

            GameObject textObj = new GameObject($"{name}_Label");
            textObj.transform.SetParent(btn.transform, false);
            textObj.transform.localPosition = new Vector3(0f, 0f, -localScale.z * 0.55f);
            textObj.transform.localRotation = Quaternion.identity;
            textObj.transform.localScale = Vector3.one;

            TextMesh text = textObj.AddComponent<TextMesh>();
            text.text = label;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.010f;
            text.color = Color.white;
        }

        private void CreateText(Transform parent, string name, string value, Vector3 localPosition, float size, TextAnchor anchor)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent, false);
            textObj.transform.localPosition = localPosition;
            textObj.transform.localRotation = Quaternion.identity;
            textObj.transform.localScale = Vector3.one;

            TextMesh text = textObj.AddComponent<TextMesh>();
            text.text = value;
            text.anchor = anchor;
            text.alignment = anchor == TextAnchor.MiddleLeft ? TextAlignment.Left : TextAlignment.Center;
            text.characterSize = size * 0.42f;
            text.color = Color.white;
        }

        private void DestroyObject(UnityEngine.Object obj)
        {
            if (obj == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(obj);
            else
                Object.DestroyImmediate(obj);
        }
    }
}
