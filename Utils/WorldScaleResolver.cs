using ExitGames.Client.Photon;
using Photon.Realtime;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

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

    public static float GetWorldScale(VRRig rig)
    {
        if (rig == null)
            return 1f;

        if (TryGetScaleFromObject(rig, out float rigScale))
            return rigScale;

        if (TryGetScaleFromCreator(rig, out float creatorScale))
            return creatorScale;

        if (TryGetScaleFromCustomProperties(rig?.Creator?.GetPlayerRef(), out float customPropScale))
            return customPropScale;

        return GetTransformFallback(rig);
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
            return 1f;

        Vector3 scale = rig.transform.lossyScale;
        float averaged = (Mathf.Abs(scale.x) + Mathf.Abs(scale.y) + Mathf.Abs(scale.z)) / 3f;

        if (averaged < 0.2f || averaged > 3f)
            return 1f;

        return averaged;
    }
}