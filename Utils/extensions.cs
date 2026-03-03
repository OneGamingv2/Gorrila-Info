using HarmonyLib;
using PlayFab;
using PlayFab.ClientModels;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Checker
{
    public enum Platform : byte
    {
        Steam,
        PC,
        Standalone,
        Unknown
    }

    public static class Extensions
    {
        public static readonly HashSet<VRRig> PlayersWithCosmetics = new HashSet<VRRig>();
        public static readonly Dictionary<VRRig, Platform> PlayerPlatforms = new Dictionary<VRRig, Platform>();
        public static readonly Dictionary<VRRig, string[]> PlayerMods = new Dictionary<VRRig, string[]>();

        public static Dictionary<string, string> KnownCheats = new Dictionary<string, string>();
        public static Dictionary<string, string> KnownMods = new Dictionary<string, string>();
        public static string[] KnownCheaters = Array.Empty<string>();

        private static readonly FieldInfo _rawCosmeticStringField =
            AccessTools.Field(typeof(VRRig), "rawCosmeticString");
        private static readonly FieldInfo _fpsField =
            AccessTools.Field(typeof(VRRig), "fps");
        private static readonly FieldInfo _playerColorField =
            AccessTools.Field(typeof(VRRig), "playerColor");


        private static FieldInfo _customPropertiesField;
        private static bool _customPropertiesFieldResolved;

        private static readonly List<string> _propertyBuffer = new List<string>(16);

        private static readonly string[] SpooferResult = { "Spoofer" };

        public static readonly Dictionary<string, string> SpecialModsList = new Dictionary<string, string>(112, StringComparer.OrdinalIgnoreCase)
        {
            { "GFaces", "gFACES" },

            { "drowsiiiGorillaInfoBoard", "gInfo Board" },
            { "MonkeCosmetics::Material", "Monke Cosmetics" },
            { "DeeTags", "DEE TAGS" },
            { "GorillaNametags", "GORILLA NAMETAGS" },
            { "Boy Do I Love Information", "BDIL-INFO" },
            { "NametagsPlusPlus", "NAMETAGS++" },
            { "kingbingus.oculusreportmenu", "OCULUS REPORT MENU" },
            { "github.com/maroon-shadow/SimpleBoards", "SIMPLEBOARDS" },
            { "cody likes burritos", "ShutUpMonkeys" },
            { "ObsidianMC", "OBSIDIAN" },
            { "frhiugvhrejughejuruij_fhbuijerbfjkvbrehjbv/uithbuyreghuyhvferwu\\_jrjegjuu.ireji/girenhguerhn", "RESURGENCE" },
            { "GTrials", "gTRIALS" },
            { "usinggphys", "gPHYS" },
            { "github.com/ZlothY29IQ/GorillaMediaDisplay", "gMEDIA DISPLAY" },
            { "github.com/ZlothY29IQ/TooMuchInfo", "TOOMUCHINFO" },
            { "github.com/ZlothY29IQ/RoomUtils-IW", "ROOMUTILS-IW" },
            { "github.com/ZlothY29IQ/MonkeClick", "MONKECLICK" },
            { "github.com/ZlothY29IQ/MonkeClick-CI", "MONKECLICK-CI" },
            { "github.com/ZlothY29IQ/MonkeRealism", "MONKEREALISM" },
            { "WalkSimulator", "WALKSIM ZLOTHY" },
            { "FPS-Nametags for Zlothy", "FPS-TAGS ZLOTHY" },
            { "Dingus", "DINGUS" },
            { "https://github.com/arielthemonke/GorillaCraftAutoBuilder", "gCRAFT AUTO BUILD" },
            { "MediaPad", "MEDIAPAD" },
            { "GorillaCinema", "gCINEMA" },
            { "ChainedTogetherActive", "CHAINEDTOGETHER" },
            { "GPronouns", "gPRONOUNS" },
            { "CSVersion", "CustomSkin" },
            { "github.com/ZlothY29IQ/Zloth-RecRoomRig", "ZLOTHYBodyEst" },
            { "ShirtProperties", "SHIRTS-OLD" },
            { "GorillaShirts", "SHIRTS" },
            { "GS", "OLD SHIRTS" },
            { "genesis", "GENESIS" },
            { "ØƦƁƖƬ", "ORBIT" },
            { "Untitled", "UNTITLED" },
            { "EmoteWheel", "EMOTE" },
            { "MistUser", "MIST" },
            { "ElixirMenu", "ELIXIR" },
            { "Elixir", "ELIXIR" },
            { "elux", "ELUX" },
            { "VioletFreeUser", "VIOLETFREE" },
            { "Hidden Menu", "HIDDEN" },
            { "HP_Left", "HOLDABLEPAD" },
            { "GrateVersion", "GRATE" },
            { "void", "VOID" },
            { "BananaOS", "BANANAOS" },
            { "GC", "GORILLACRAFT" },
            { "CarName", "VEHICLES" },
            { "6XpyykmrCthKhFeUfkYGxv7xnXpoe2", "CCMV2" },
            { "cronos", "CRONOS" },
            { "ORBIT", "ORBIT" },
            { "Violet On Top", "VIOLET" },
            { "VioletPaidUser", "VIOLETPAID" },
            { "MonkePhone", "MONKEPHONE" },
            { "Body Tracking", "BODYTRACKING" },
            { "Graze Heath System", "HEALTH SYSTEM" },
            { "Body Estimation", "HANBodyEst" },
            { "GorillaTorsoEstimator", "TORSOEst" },
            { "com.duv14.gorillatag.gorillainfo", "GORILLAINFO" },
            { "gorillainfo", "GORILLAINFO" },
            { "Gorilla Track", "gTRACK OLD" },
            { "Gorilla Track Packed", "gTRACK-PACK" },
            { "Gorilla Track 2.3.0", "gTRACK" },
            { "GorillaWatch", "GORILLAWATCH" },
            { "InfoWatch", "INFOWATCH" },
            { "BananaPhone", "BANANAPHONE" },
            { "Vivid", "VIVID" },
            { "CustomMaterial", "CUSTOMCOSMETICS" },
            { "cheese is gouda", "WHOISTHATMONKE" },
            { "WhoIsThatMonke Version", "WHOISTHATMONKE" },
            { "I like cheese", "RECROOMRIG" },
            { "pmversion", "PLAYERMODELS" },
            { "msp", "MONKESMARTPHONE" },
            { "gorillastats", "GORILLASTATS" },
            { "using gorilladrift", "GORILLADRIFT" },
            { "monkehavocversion", "MONKEHAVOC" },
            { "tictactoe", "TICTACTOE" },
            { "ccolor", "INDEX" },
            { "imposter", "GORILLAAMONGUS" },
            { "spectapeversion", "SPECTAPE" },
            { "cats", "CATS" },
            { "made by biotest05 :3", "DOGS" },
            { "fys cool magic mod", "FYSMAGICMOD" },
            { "chainedtogether", "CHAINED TOGETHER" },
            { "goofywalkversion", "GOOFYWALK" },
            { "void_menu_open", "VOID" },
            { "dark", "SHIBAGT DARK" },
            { "oblivionuser", "OBLIVION" },
            { "eyerock reborn", "EYEROCK" },
            { "asteroidlite", "ASTEROID LITE" },
            { "cokecosmetics", "COKE COSMETX" },
            { "silliness", "SILLINESS" },
            { "CoolCustomProperty", "Axo's Custom Property" },
            { "SimpleInfo", "duv.lucy" },
            { "colour", "CUSTOMCOSMETICS" },
            { "shirtversion", "GORILLASHIRTS" },
            { "OculusReportMenu", "ReportMenu" },
            { "CastingShouldBeFree", "FreeCastingMod" },
            { "ThareonsChecker", "ThareonsChecker" },
            { "MonkeyNotificationLib", "JarvisMod (Possibility)" },
            { "RexonPAID", "RexonPaid" },
            { "ui", "ZelixModChecker (M)" },
            { "", "Thareons UI" },
            { "FemBoyChecker", "Sentinel Menu" },
        };


        public static bool HasCosmetics(this VRRig rig)
        {
            return rig != null && PlayersWithCosmetics.Contains(rig);
        }

        public static Platform GetPlatform(this VRRig rig)
        {
            if (rig == null || _rawCosmeticStringField == null)
                return Platform.Unknown;

            string raw = _rawCosmeticStringField.GetValue(rig) as string;
            if (raw == null)
                return Platform.Unknown;

            if (raw.Contains("S. FIRST LOGIN"))
                return Platform.Steam;

            if (raw.Contains("FIRST LOGIN"))
                return Platform.PC;

            if (rig.Creator != null)
            {
                var playerRef = rig.Creator.GetPlayerRef();
                if (playerRef != null && playerRef.CustomProperties != null && playerRef.CustomProperties.Count >= 2)
                    return Platform.PC;
            }

            return Platform.Standalone;
        }

        public static string ParsePlatform(this Platform p)
        {
            switch (p)
            {
                case Platform.Steam: return "<color=#0091F7>Steam</color>";
                case Platform.PC: return "<color=#FF69B4>PC</color>";
                case Platform.Standalone: return "<color=#663399>Quest</color>";
                default: return "<color=#663399>Quest</color>";
            }
        }

        public static string[] GetPlayerMods(this VRRig rig)
        {
            if (rig == null)
                return Array.Empty<string>();

            if (PlayerMods.TryGetValue(rig, out var mods))
                return (mods != null && mods.Length > 10) ? SpooferResult : mods;

            return Array.Empty<string>();
        }


        public static int GetFPS(this VRRig rig)
        {
            if (rig == null || _fpsField == null)
                return -1;

            try
            {
                object value = _fpsField.GetValue(rig);
                return value != null ? (int)value : -1;
            }
            catch
            {
                return -1;
            }
        }

        public static Color GetColor(this VRRig rig)
        {
            if (rig == null || _playerColorField == null)
                return Color.white;

            try
            {
                object value = _playerColorField.GetValue(rig);
                return value != null ? (Color)value : Color.white;
            }
            catch
            {
                return Color.white;
            }
        }

        public static string ParseColor(this Color color)
        {
            int r = Mathf.RoundToInt(color.r * 9f);
            int g = Mathf.RoundToInt(color.g * 9f);
            int b = Mathf.RoundToInt(color.b * 9f);
            return $"{r} {g} {b}";
        }


        public static string[] GetCustomProperties(this VRRig rig)
        {
            if (rig == null || rig.OwningNetPlayer == null)
                return Array.Empty<string>();

            try
            {
                if (!_customPropertiesFieldResolved)
                {
                    _customPropertiesField = AccessTools.Field(rig.OwningNetPlayer.GetType(), "customProperties");
                    _customPropertiesFieldResolved = true;
                }

                if (_customPropertiesField == null)
                    return Array.Empty<string>();

                var customProperties = _customPropertiesField.GetValue(rig.OwningNetPlayer)
                    as ExitGames.Client.Photon.Hashtable;

                if (customProperties == null || customProperties.Count == 0)
                    return Array.Empty<string>();

                _propertyBuffer.Clear();

                foreach (System.Collections.DictionaryEntry entry in customProperties)
                {
                    if (SpecialModsList.TryGetValue(entry.Key.ToString(), out string modName))
                        _propertyBuffer.Add(modName);
                }

                return _propertyBuffer.Count > 0 ? _propertyBuffer.ToArray() : Array.Empty<string>();
            }
            catch (Exception e)
            {
                Debug.LogError("[InfoPad] Error getting custom properties: " + e.Message);
                return Array.Empty<string>();
            }
        }

      /*  public static string[] DetectModsFromCosmetics(this VRRig rig)
        {
            if (rig == null || rig.cosmeticSet == null)
                return Array.Empty<string>();

            try
            {
                string allowed = rig.concatStringOfCosmeticsAllowed ?? "";

                if (string.IsNullOrEmpty(allowed))
                    return Array.Empty<string>();

                foreach (var cosmetic in rig.cosmeticSet.items)
                {
                    if (!cosmetic.isNullItem && allowed.IndexOf(cosmetic.itemName, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        return new string[] { "<color=red>COSMETX</color>" };
                    }
                }

                return Array.Empty<string>();
            }
            catch (Exception e)
            {
                Debug.LogError("[InfoPad] Error detecting cosmetic mods: " + e.Message);
                return Array.Empty<string>();
            }
        } */


        private static readonly Dictionary<string, float> _waitingForCreationDate = new Dictionary<string, float>();
        private static readonly Dictionary<string, string> _creationDateCache = new Dictionary<string, string>();

        public static IReadOnlyDictionary<string, string> CreationDateCache => _creationDateCache;

        public static string GetCreationDate(string userId, Action<string> onResult = null, string format = "MM/dd/yyyy")
        {
            if (_creationDateCache.ContainsKey(userId))
                return _creationDateCache[userId];

            float currentTime = Time.time;

            if (_waitingForCreationDate.TryGetValue(userId, out float nextTime) && currentTime < nextTime)
                return "Loading...";

            _waitingForCreationDate[userId] = currentTime + 10f;


            FetchCreationDate(userId, result =>
            {
                _creationDateCache[userId] = result;
                onResult?.Invoke(result);
            }, format);

            return "Loading...";
        }


        private static void FetchCreationDate(string userId, Action<string> onResult, string format)
        {
            PlayFabClientAPI.GetAccountInfo(
                new GetAccountInfoRequest { PlayFabId = userId },
                result =>
                {
                    string date = result.AccountInfo.Created.ToString(format);
                    _creationDateCache[userId] = date;
                    _waitingForCreationDate.Remove(userId);
                    onResult?.Invoke(date);
                },
                error =>
                {
                    _creationDateCache[userId] = "Null";
                    _waitingForCreationDate.Remove(userId);
                    onResult?.Invoke("Null");
                });
        }
    }
}
