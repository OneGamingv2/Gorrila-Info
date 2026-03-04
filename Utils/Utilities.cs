using ExitGames.Client.Photon;
using Photon.Realtime;
using System;
using System.Collections.Generic;
using Checker;
using UnityEngine;

public class Utilities
{
    private static readonly List<string> _detectedModsBuffer = new List<string>(32);
    private static readonly HashSet<string> _detectedModsSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private const int LowFpsThreshold = 25;
    private const float SuspiciousSpeedThreshold = 7.5f;
    private const float SpeedBoostThreshold = 11f;
    private const float FlyVerticalVelocityThreshold = 6f;
    private const float SteamLongArmsThreshold = 1.12f;
    private const float SteamExtremeLongArmsThreshold = 1.35f;
    private static readonly string[] _signatureKeywords =
    {
        "NOCLIP MOD", "SPEED", "GHOST", "INVIS", "TAGALL", "AUTOTAG", "RIGGUN",
        "PULL", "SPEED BOOST", "CRASH GUN", "LAG GUN", "TELEPORT GUN", "ESP",
        "ANTI-REPORT", "NAME CHANGER", "EXTERNAL MENU", "MODDED CLIENT",
        "Heavy Spoofer", "Spoofer", "SPOOFED PROPS", "EXTERNAL SIGNATURE", "COSMETX",
        "VOID", "WURST", "II STUPID", "PHANTOM", "ECLIPSE", "CRIMSON", "AZURE",
        "SENTINEL", "LUNAR", "SHADOW", "CELESTIAL", "NOVA", "APEX", "VIPER",
        "ZEPHYR", "PRISM", "PULSAR", "OBLIVION", "FROST", "INFERNO", "SPECTRE",
        "VENOM", "HYDRA", "GLITCH", "MIASMA", "COSMOS", "TWILIGHT", "STEREO",
        "FLUX", "GALAX", "NEBULA PAID", "RESURGENCE", "ELIXIR", "MANGO", "PLASMA"
    };

    public List<string> DetectAllMods(VRRig rig)
    {
        _detectedModsBuffer.Clear();
        _detectedModsSet.Clear();

        if (rig == null)
            return _detectedModsBuffer;

        AddModsFromRigCache(rig);
        AddModsFromReflectionProps(rig);
        AddModsFromPhotonCustomProps(rig);
        AddBehavioralSignals(rig);

        return _detectedModsBuffer;
    }

    public List<string> DetectModsFromCustomProps(VRRig rig)
    {
        return DetectAllMods(rig);
    }

    private void AddModsFromRigCache(VRRig rig)
    {
        string[] cachedMods = rig.GetPlayerMods();
        if (cachedMods == null || cachedMods.Length == 0)
            return;

        for (int i = 0; i < cachedMods.Length; i++)
            TryAddMod(cachedMods[i]);
    }

    private void AddModsFromReflectionProps(VRRig rig)
    {
        string[] reflectionMods = rig.GetCustomProperties();
        if (reflectionMods == null || reflectionMods.Length == 0)
            return;

        for (int i = 0; i < reflectionMods.Length; i++)
            TryAddMod(reflectionMods[i]);
    }

    private void AddModsFromPhotonCustomProps(VRRig rig)
    {
        Player p = rig?.Creator?.GetPlayerRef();

        if (p?.CustomProperties == null || p.CustomProperties.Count == 0)
            return;

        if (p.CustomProperties.Count >= 40)
            TryAddMod("Spoofer");

        foreach (var kvp in p.CustomProperties)
        {
            string key = kvp.Key?.ToString();
            if (!string.IsNullOrEmpty(key) && Checker.Extensions.SpecialModsList.TryGetValue(key, out string modName))
            {
                TryAddMod(modName);
            }
            TryAddMatchedSignaturesFromText(key);

            string value = kvp.Value?.ToString();
            if (!string.IsNullOrEmpty(value) && Checker.Extensions.SpecialModsList.TryGetValue(value, out string valueModName))
            {
                TryAddMod(valueModName);
            }
            TryAddMatchedSignaturesFromText(value);
        }
    }

    private void TryAddMatchedSignaturesFromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        for (int i = 0; i < _signatureKeywords.Length; i++)
        {
            string keyword = _signatureKeywords[i];
            if (text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                TryAddMod(keyword);
        }
    }

    private void AddBehavioralSignals(VRRig rig)
    {
        Platform platform = rig.GetPlatform();

        int fps = rig.GetFPS();
        if (fps > 0 && fps <= LowFpsThreshold)
            TryAddMod("LOW FPS");

        float armScale = ArmLengthResolver.GetArmLengthScale(rig);
        if (platform == Platform.Steam)
        {
            if (armScale >= SteamExtremeLongArmsThreshold)
                TryAddMod("EXTREME LONG ARMS");
            else if (armScale >= SteamLongArmsThreshold)
                TryAddMod("LONG ARMS");
        }

        Rigidbody rb = rig.GetComponent<Rigidbody>();
        if (rb == null)
            return;

        Vector3 velocity = rb.linearVelocity;
        float speed = velocity.magnitude;

        if (speed >= SuspiciousSpeedThreshold)
            TryAddMod("SPEED");

        if (speed >= SpeedBoostThreshold)
            TryAddMod("SPEED BOOST");

        if (Mathf.Abs(velocity.y) >= FlyVerticalVelocityThreshold)
            TryAddMod("FLY");
    }

    private void TryAddMod(string modName)
    {
        if (string.IsNullOrWhiteSpace(modName))
            return;

        modName = NormalizeModName(modName);

        if (_detectedModsSet.Add(modName))
            _detectedModsBuffer.Add(modName);
    }

    private static string NormalizeModName(string modName)
    {
        string trimmed = modName.Trim();

        if (trimmed.IndexOf("COSMET", StringComparison.OrdinalIgnoreCase) >= 0)
            return "COSMETX";

        if (trimmed.Equals("Heavy Spoofer", StringComparison.OrdinalIgnoreCase))
            return "SPOOFER";

        return trimmed;
    }

    public string FormatModsTextMultiline(List<string> mods, int maxChars)
    {
        if (mods == null || mods.Count == 0) return "";

        string joined = string.Join(", ", mods);
        return joined.Length > maxChars ? joined.Substring(0, maxChars) : joined;
    }
}
