using BepInEx;
using UnityEngine;
using GorillaInfo.LAB;

namespace GorillaInfo
{
    [BepInPlugin("com.LIPPS.gorillatag.gorillainfo", "gorillainfo", "1.0.2")]
    public class GorillaInfoMain : BaseUnityPlugin
    {
        public static GorillaInfoMain Instance;
        public MenuLoader menuLoader;
        public MenuAnimations menuAnimations;
        public ButtonClick buttonClick;
        public Button button;
        public GunLib gunLib;
        public CheckerUtilities utilities;
        public MainHandler updMain;
        public Misc misc;
        public LobbyHandler lobbyHandler;
        public SettingsHandler settingsHandler;
        public NotificationManager notificationManager;
        public MoreInfoHandler moreInfoHandler;
        public MusicHandler musicHandler;

        public enum MenuState : byte { Closed, Opening, Open, Closing }
        public MenuState menuState = MenuState.Closed;
        public bool spawned;

        private bool _buttonWasPressed;
        private float _nextMainPageUpdate;
        private float _nextLobbyUpdate;
        private const float MainPageInterval = 0.25f;
        private const float LobbyInterval = 0.45f;
        private bool _gunDestroyed;

        private void Awake()
        {
            Instance = this;
            buttonClick = gameObject.AddComponent<ButtonClick>();
            InitializeModules();
        }

        private void Update()
        {
            if (GorillaTagger.Instance == null) return;

            if (!spawned)
            {
                menuLoader.loadmenu();
                spawned = true;
            }

            HandleInputs();

            if (menuState == MenuState.Opening || menuState == MenuState.Closing)
                menuAnimations.animshandler();

            moreInfoHandler?.UpdateAnimation();
            moreInfoHandler?.UpdatePlayerInfo();
            gunLib?.UpdateNametags();

            if (menuState == MenuState.Closed && !_gunDestroyed && spawned)
            {
                gunLib.OnMenuClosed();
                settingsHandler?.SetLockOnStateFromRuntime(false, false);
                _gunDestroyed = true;
            }

            if (menuState == MenuState.Open)
                _gunDestroyed = false;

            if (menuState != MenuState.Open) return;

            buttonClick.ballvisibility();
            buttonClick.uptadeball();
            gunLib.rearmgun();
            gunLib.gunray();
            musicHandler?.UpdateMusicData();
            button.checkbuttons();

            if (Time.time >= _nextMainPageUpdate)
            {
                updMain.UpdateMainPage();
                _nextMainPageUpdate = Time.time + MainPageInterval;
            }

            if (Time.time >= _nextLobbyUpdate)
            {
                lobbyHandler?.UpdateLobby();
                _nextLobbyUpdate = Time.time + LobbyInterval;
            }
        }

        private void InitializeModules()
        {
            menuLoader = new MenuLoader();
            menuAnimations = new MenuAnimations();
            button = new Button();
            gunLib = new GunLib();
            utilities = new CheckerUtilities();
            updMain = new MainHandler();
            misc = new Misc();
            lobbyHandler = new LobbyHandler();
            settingsHandler = new SettingsHandler();
            notificationManager = new NotificationManager();
            moreInfoHandler = new MoreInfoHandler();
            musicHandler = new MusicHandler();
        }

        private void Start()
        {
            GorillaInfo.LAB.Assets.LoadAssetBundle();
            notificationManager?.Notify(" GorillaInfo Loaded ");
            misc.InitactionsTexts();
            misc.InitLobbyTexts();
            lobbyHandler.InitializeLobbySlots();
            settingsHandler.InitializeSettings();
       
        }

        private void HandleInputs()
        {
            bool pressed = SimpleInputs.LeftY;
            if (pressed && !_buttonWasPressed)
            {
                if (menuState == MenuState.Open)
                    menuAnimations.closinganim();
                else if (menuState == MenuState.Closed)
                    menuAnimations.openanim();
            }
            _buttonWasPressed = pressed;
        }
    }
}
