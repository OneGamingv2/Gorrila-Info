// THIS IS IN VERY EARLY PRE-ALPHA PROTOTYPE CODE, EXPECT BREAKAGES AND INCONSISTENCIES WORLD SCALE RESOLUTION IS A HARD PROBLEM AND THIS IS MY FIRST APPROACH TO IT. PROCEED WITH CAUTION AND EXPECT TO REWRITE THIS SEVERAL TIMES.
using ExitGames.Client.Photon;
using Photon.Realtime;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using Steamworks;

public static class WorldScaleResolver
{
    private static readonly string[] ScaleMemberNames =
    {
        "worldScale", "worldscale", "WorldScale", "scaleFactor", "ScaleFactor", "playerScale", "PlayerScale", "sizeMultiplier", "SizeMultiplier", "avatarScale", "AvatarScale", "scale"
    };

    private static readonly string[] ScalePropertyKeys =
    {
        "worldScale", "worldscale", "WorldScale", "scale", "Scale", "playerScale", "PlayerScale", "size", "Size"
    };

    private static readonly Dictionary<Type, List<MemberInfo>> MemberCache = new Dictionary<Type, List<MemberInfo>>();
    private static readonly Dictionary<string, float> SmoothedScaleByUser = new Dictionary<string, float>(64);
    private static readonly Dictionary<string, float> LastUpdateTimeByUser = new Dictionary<string, float>(64);

    private const float MinScale = 0.45f;
    private const float MaxScale = 2.4f;
    private const float MaxSampleJump = 0.45f;
    private const float DefaultScale = 1f;
    private const float HandToHeadReference = 0.58f;
    private const float ShoulderWidthReference = 0.40f;

    public static float GetWorldScale(VRRig rig)
    {
        if (rig == null)
            return DefaultScale;

        string userKey = BuildPerUserKey(rig);
        float rawScale = ResolveRawWorldScale(rig);

        if (string.IsNullOrEmpty(userKey))
            return rawScale;

        return SmoothPerUserScale(userKey, rawScale);
    }

    /// <summary>
    /// Clears the cached smoothed scale for a specific player so the next call
    /// returns a fresh reading instead of carrying over a previous player's value.
    /// Call this whenever the selected/locked target changes.
    /// </summary>
    public static void ForceRefreshForRig(VRRig rig)
    {
        if (rig == null) return;
        string key = BuildPerUserKey(rig);
        if (!string.IsNullOrEmpty(key))
        {
            SmoothedScaleByUser.Remove(key);
            LastUpdateTimeByUser.Remove(key);
        }
    }

    /// <summary>
    /// Attempts to retrieve the Steam persona name for the player owning the given rig
    /// using the Steamworks SDK. Returns false if Steam is unavailable, the user has no
    /// Steam ID, or the SDK hasn't received the name yet (call again next frame).
    /// </summary>
    public static bool TryGetSteamPersonaName(VRRig rig, out string steamName)
    {
        steamName = null;
        if (rig == null || !SteamManager.Initialized)
            return false;

        string userId = rig.Creator?.GetPlayerRef()?.UserId;
        if (string.IsNullOrEmpty(userId))
            return false;

        int idx = userId.IndexOf("steam_", StringComparison.OrdinalIgnoreCase);
        string candidate = idx >= 0 ? userId[(idx + 6)..] : userId;

        if (!ulong.TryParse(candidate, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong steam64) || steam64 == 0)
            return false;

        try
        {
            CSteamID steamId = new CSteamID(steam64);
            if (!steamId.IsValid())
                return false;

            // Request the Steam user info so the SDK caches it asynchronously;
            // subsequent calls (next few frames) will return the real name.
            SteamFriends.RequestUserInformation(steamId, true);

            string name = SteamFriends.GetFriendPersonaName(steamId);
            if (string.IsNullOrEmpty(name) || name.Equals("[unknown]", StringComparison.OrdinalIgnoreCase))
                return false;

            steamName = name;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Tries to read a world-scale value from Steam Rich Presence for this player.
    /// Gorilla Tag publishes state via Steam RPC; if worldScale is present it's the
    /// most authoritative source (cannot be faked via Photon alone).
    /// </summary>
    private static bool TryGetSteamRichPresenceScale(VRRig rig, out float scale)
    {
        scale = DefaultScale;
        if (rig == null || !SteamManager.Initialized)
            return false;

        string userId = rig.Creator?.GetPlayerRef()?.UserId;
        if (string.IsNullOrEmpty(userId))
            return false;

        int idx = userId.IndexOf("steam_", StringComparison.OrdinalIgnoreCase);
        string candidate = idx >= 0 ? userId[(idx + 6)..] : userId;

        if (!ulong.TryParse(candidate, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong steam64) || steam64 == 0)
            return false;

        try
        {
            CSteamID steamId = new CSteamID(steam64);
            if (!steamId.IsValid())
                return false;

            for (int i = 0; i < ScalePropertyKeys.Length; i++)
            {
                string rpVal = SteamFriends.GetFriendRichPresence(steamId, ScalePropertyKeys[i]);
                if (!string.IsNullOrEmpty(rpVal) && TryConvertScaleValue(rpVal, out float parsed))
                {
                    scale = parsed;
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static float ResolveRawWorldScale(VRRig rig)
    {
        if (rig == null)
            return DefaultScale;

        // Steam Rich Presence is verified server-side and cannot be spoofed via Photon
        if (TryGetSteamRichPresenceScale(rig, out float steamScale))
            return steamScale;

        if (TryGetScaleFromObject(rig, out float rigScale))
            return rigScale;

        if (TryGetScaleFromCreator(rig, out float creatorScale))
            return creatorScale;

        if (TryGetScaleFromCustomProperties(rig?.Creator?.GetPlayerRef(), out float customPropScale))
            return customPropScale;

        float fallbackScale = GetMathFallback(rig);
        if (!IsValidScale(fallbackScale))
            return DefaultScale;

        return fallbackScale;
    }

    private static float SmoothPerUserScale(string key, float raw)
    {
        raw = Mathf.Clamp(raw, MinScale, MaxScale);
        float now = Time.time;

        if (!SmoothedScaleByUser.TryGetValue(key, out float current))
        {
            SmoothedScaleByUser[key] = raw;
            LastUpdateTimeByUser[key] = now;
            return raw;
        }

        float dt = now - (LastUpdateTimeByUser.TryGetValue(key, out float lastTs) ? lastTs : now);
        LastUpdateTimeByUser[key] = now;

        if (Mathf.Abs(raw - current) > MaxSampleJump)
            raw = Mathf.Lerp(current, raw, 0.2f);

        // Use faster alpha (10× dt) so Steam users' scale updates within ~2 refreshes
        float alpha = Mathf.Clamp01(dt * 10f);
        float smoothed = Mathf.Lerp(current, raw, alpha);
        SmoothedScaleByUser[key] = smoothed;
        return smoothed;
    }

    private static string BuildPerUserKey(VRRig rig)
    {
        Player player = rig?.Creator?.GetPlayerRef();
        if (player == null)
            return null;

        string userId = player.UserId;
        if (TryBuildSteamUserKey(userId, out string steamKey))
            return steamKey;

        if (!string.IsNullOrEmpty(userId))
            return "uid:" + userId;

        int actor = player.ActorNumber;
        if (actor > 0)
            return "actor:" + actor.ToString(CultureInfo.InvariantCulture);

        return null;
    }

    private static bool TryBuildSteamUserKey(string userId, out string key)
    {
        key = null;
        if (string.IsNullOrEmpty(userId))
            return false;

        int idx = userId.IndexOf("steam_", StringComparison.OrdinalIgnoreCase);
        string candidate = idx >= 0 ? userId[(idx + 6)..] : userId;

        if (!ulong.TryParse(candidate, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong steam64) || steam64 == 0)
            return false;

        try
        {
            CSteamID steamId = new CSteamID(steam64);
            if (!steamId.IsValid())
                return false;

            if (SteamManager.Initialized)
            {
                try { _ = SteamFriends.GetFriendPersonaName(steamId); } catch { }
            }

            key = "steam:" + steam64.ToString(CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static float GetMathFallback(VRRig rig)
    {
        float transformScale = GetTransformFallback(rig);
        float bodyScale = EstimateBodyScaleFromRigMath(rig);

        bool hasTransform = IsValidScale(transformScale);
        bool hasBody = IsValidScale(bodyScale);

        float result;

        if (hasTransform && hasBody)
            result = Mathf.Clamp((transformScale * 0.6f) + (bodyScale * 0.4f), MinScale, MaxScale);
        else if (hasTransform)
            result = transformScale;
        else if (hasBody)
            result = bodyScale;
        else
            return DefaultScale;

        // Snap near-default values to exactly 100% so unmodified players
        // don't show e.g. "98%" or "103%" due to heuristic noise.
        if (result >= 0.92f && result <= 1.08f)
            return DefaultScale;

        return result;
    }

    private static float EstimateBodyScaleFromRigMath(VRRig rig)
    {
        if (rig == null)
            return DefaultScale;

        Transform root = rig.transform;
        Transform head = FindByNameHint(root, "head", "Head", "headMesh");
        Transform leftHand = FindByNameHint(root, "leftHand", "LHand", "LeftHand");
        Transform rightHand = FindByNameHint(root, "rightHand", "RHand", "RightHand");
        Transform leftShoulder = FindByNameHint(root, "leftShoulder", "LShoulder", "shoulder_l");
        Transform rightShoulder = FindByNameHint(root, "rightShoulder", "RShoulder", "shoulder_r");

        float sum = 0f;
        int count = 0;

        if (head != null && leftHand != null && rightHand != null)
        {
            float handToHead = (Vector3.Distance(head.position, leftHand.position) + Vector3.Distance(head.position, rightHand.position)) * 0.5f;
            if (handToHead > 0.01f)
            {
                sum += handToHead / HandToHeadReference;
                count++;
            }
        }

        if (leftShoulder != null && rightShoulder != null)
        {
            float shoulderWidth = Vector3.Distance(leftShoulder.position, rightShoulder.position);
            if (shoulderWidth > 0.01f)
            {
                sum += shoulderWidth / ShoulderWidthReference;
                count++;
            }
        }

        if (count == 0)
            return DefaultScale;

        float value = sum / count;
        return Mathf.Clamp(value, MinScale, MaxScale);
    }

    private static Transform FindByNameHint(Transform root, params string[] hints)
    {
        if (root == null || hints == null)
            return null;

        Queue<Transform> queue = new Queue<Transform>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            Transform current = queue.Dequeue();
            string name = current.name;

            for (int i = 0; i < hints.Length; i++)
            {
                if (name.IndexOf(hints[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return current;
            }

            for (int c = 0; c < current.childCount; c++)
                queue.Enqueue(current.GetChild(c));
        }

        return null;
    }

    private static bool IsValidScale(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value >= MinScale && value <= MaxScale;
    }

    private static bool TryGetScaleFromCreator(VRRig rig, out float scale)
    {
        scale = 1f;

        if (rig == null || rig.Creator == null)
            return false;

        if (TryGetScaleFromObject(rig.Creator, out scale))
            return true;

        Player playerRef = rig.Creator.GetPlayerRef();
        if (playerRef != null && TryGetScaleFromObject(playerRef, out scale))
            return true;

        return false;
    }

    private static bool TryGetScaleFromCustomProperties(Player player, out float scale)
    {
        scale = 1f;
        if (player?.CustomProperties == null || player.CustomProperties.Count == 0)
            return false;

        Hashtable props = player.CustomProperties;
        for (int i = 0; i < ScalePropertyKeys.Length; i++)
        {
            if (!props.TryGetValue(ScalePropertyKeys[i], out object value))
                continue;

            if (TryConvertScaleValue(value, out float parsed))
            {
                scale = parsed;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetScaleFromObject(object obj, out float scale)
    {
        scale = 1f;

        if (obj == null)
            return false;

        List<MemberInfo> members = GetScaleMembers(obj.GetType());
        for (int i = 0; i < members.Count; i++)
        {
            object raw = ReadMemberValue(obj, members[i]);
            if (raw == null)
                continue;

            if (TryConvertScaleValue(raw, out float parsed))
            {
                scale = parsed;
                return true;
            }
        }

        return false;
    }

    private static List<MemberInfo> GetScaleMembers(Type type)
    {
        if (type == null)
            return new List<MemberInfo>(0);

        if (MemberCache.TryGetValue(type, out List<MemberInfo> cached))
            return cached;

        var list = new List<MemberInfo>(8);
        BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        FieldInfo[] fields = type.GetFields(flags);
        for (int i = 0; i < fields.Length; i++)
        {
            if (LooksLikeScaleMember(fields[i].Name) && IsSupportedType(fields[i].FieldType))
                list.Add(fields[i]);
        }

        PropertyInfo[] properties = type.GetProperties(flags);
        for (int i = 0; i < properties.Length; i++)
        {
            PropertyInfo p = properties[i];
            if (p.GetIndexParameters().Length != 0)
                continue;

            if (!LooksLikeScaleMember(p.Name) || !IsSupportedType(p.PropertyType))
                continue;

            MethodInfo getter = p.GetGetMethod(true);
            if (getter != null)
                list.Add(p);
        }

        MemberCache[type] = list;
        return list;
    }

    private static bool LooksLikeScaleMember(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        for (int i = 0; i < ScaleMemberNames.Length; i++)
        {
            if (name.IndexOf(ScaleMemberNames[i], StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    private static bool IsSupportedType(Type t)
    {
        return t == typeof(float) ||
               t == typeof(double) ||
               t == typeof(int) ||
               t == typeof(long) ||
               t == typeof(short) ||
               t == typeof(decimal) ||
               t == typeof(string) ||
               t == typeof(Vector3) ||
               t == typeof(Vector2);
    }

    private static object ReadMemberValue(object obj, MemberInfo member)
    {
        try
        {
            if (member is FieldInfo field)
                return field.GetValue(obj);

            if (member is PropertyInfo prop)
                return prop.GetValue(obj, null);
        }
        catch
        {
        }

        return null;
    }

    private static bool TryConvertScaleValue(object value, out float scale)
    {
        scale = 1f;
        if (value == null)
            return false;

        float raw;

        if (value is float f)
            raw = f;
        else if (value is double d)
            raw = (float)d;
        else if (value is int i)
            raw = i;
        else if (value is long l)
            raw = l;
        else if (value is short s)
            raw = s;
        else if (value is decimal m)
            raw = (float)m;
        else if (value is Vector3 v3)
            raw = (Mathf.Abs(v3.x) + Mathf.Abs(v3.y) + Mathf.Abs(v3.z)) / 3f;
        else if (value is Vector2 v2)
            raw = (Mathf.Abs(v2.x) + Mathf.Abs(v2.y)) / 2f;
        else if (value is string str)
        {
            str = str.Trim().Replace("x", string.Empty).Replace("X", string.Empty).Replace("%", string.Empty);
            if (!float.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out raw) &&
                !float.TryParse(str, NumberStyles.Float, CultureInfo.CurrentCulture, out raw))
                return false;
        }
        else
        {
            return false;
        }

        if (float.IsNaN(raw) || float.IsInfinity(raw))
            return false;

        raw = Mathf.Abs(raw);

        if (raw > 10f && raw <= 300f)
            raw /= 100f;

        if (raw < 0.2f || raw > 3f)
            return false;

        scale = raw;
        return true;
    }

    private static float GetTransformFallback(VRRig rig)
    {
        if (rig == null)
            return DefaultScale;

        Vector3 scale = rig.transform.lossyScale;
        float averaged = (Mathf.Abs(scale.x) + Mathf.Abs(scale.y) + Mathf.Abs(scale.z)) / 3f;

        if (averaged < MinScale || averaged > MaxScale)
            return DefaultScale;

        return averaged;
    }
}