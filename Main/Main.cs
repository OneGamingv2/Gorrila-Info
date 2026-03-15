using BepInEx;
using UnityEngine;
using GorillaInfo.LAB;
using HarmonyLib;
using System;
using System.Reflection;

namespace GorillaInfo
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
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
        public AntiCheatHandler antiCheatHandler;

        public enum MenuState : byte { Closed, Opening, Open, Closing }
        public MenuState menuState = MenuState.Closed;
        public bool spawned;

        private bool _buttonWasPressed;
        private float _nextMainPageUpdate;
        private float _nextLobbyUpdate;
        private const float MainPageInterval = 0.25f;
        private const float LobbyInterval = 0.45f;
        private bool _gunDestroyed;
        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            buttonClick = gameObject.AddComponent<ButtonClick>();
            InitializeModules();
            ApplyCompatibilityPatches();
        }

        private void Update()
        {
            if (GorillaTagger.Instance == null) return;

            if (spawned && (menuLoader == null || menuLoader.menuInstance == null))
                spawned = false;

            if (!spawned)
            {
                menuLoader.loadmenu();
                spawned = menuLoader != null && menuLoader.menuInstance != null;
                if (!spawned)
                    return;
            }

            HandleInputs();

            try
            {
                if (menuState == MenuState.Opening || menuState == MenuState.Closing)
                    menuAnimations.animshandler();

                moreInfoHandler?.UpdateAnimation();
                moreInfoHandler?.UpdatePlayerInfo();
                gunLib?.UpdateNametags();
                antiCheatHandler?.Update();
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
            }

            if (menuState == MenuState.Closed && !_gunDestroyed && spawned)
            {
                gunLib.OnMenuClosed();
                // do NOT reset lock-on here — it should persist between opens
                _gunDestroyed = true;
            }

            if (menuState == MenuState.Open)
                _gunDestroyed = false;

            buttonClick?.ballvisibility();

            if (menuState != MenuState.Open) return;

            try
            {
                buttonClick.uptadeball();
                gunLib.rearmgun();
                gunLib.gunray();
                musicHandler?.UpdateMusicData();
                misc.UpdateModDisplay();
                button.checkbuttons();
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
            }

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
            antiCheatHandler = new AntiCheatHandler();
        }

        private void ApplyCompatibilityPatches()
        {
            if (_harmony != null)
                return;

            _harmony = new Harmony(PluginInfo.GUID + ".compat");

            Type builderPieceType = AccessTools.TypeByName("BuilderPiece");
            MethodInfo awake = builderPieceType != null ? AccessTools.Method(builderPieceType, "Awake") : null;
            MethodInfo finalizer = AccessTools.Method(typeof(GorillaInfoMain), nameof(BuilderPieceAwakeFinalizer));

            if (awake != null && finalizer != null)
            {
                _harmony.Patch(awake, finalizer: new HarmonyMethod(finalizer));
                Debug.Log("[GorillaInfo] Applied BuilderPiece Awake compatibility patch");
            }
        }

        private static Exception BuilderPieceAwakeFinalizer(Exception __exception)
        {
            if (__exception is NullReferenceException)
                return null;

            return __exception;
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
