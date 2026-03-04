using ExitGames.Client.Photon;
using Photon.Realtime;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

public static class ArmLengthResolver
{
    private static readonly string[] ArmMemberNameHints =
    {
        "armLength", "ArmLength", "armScale", "ArmScale", "longArms", "LongArms", "armMultiplier", "ArmMultiplier", "leftArm", "rightArm"
    };

    private static readonly string[] ArmPropertyKeys =
    {
        "armLength", "ArmLength", "armScale", "ArmScale", "longArms", "LongArms", "leftArmLength", "rightArmLength", "longarm", "long_arms"
    };

    private static readonly Dictionary<Type, List<MemberInfo>> MemberCache = new Dictionary<Type, List<MemberInfo>>();

    public static float GetArmLengthScale(VRRig rig)
    {
        if (rig == null)
            return 1f;

        if (TryGetArmScaleFromObject(rig, out float rigScale))
            return rigScale;

        if (rig.Creator != null)
        {
            if (TryGetArmScaleFromObject(rig.Creator, out float creatorScale))
                return creatorScale;

            Player playerRef = rig.Creator.GetPlayerRef();
            if (playerRef != null)
            {
                if (TryGetArmScaleFromObject(playerRef, out float playerScale))
                    return playerScale;

                if (TryGetArmScaleFromCustomProperties(playerRef, out float customScale))
                    return customScale;
            }
        }

        return 1f;
    }

    private static bool TryGetArmScaleFromCustomProperties(Player player, out float scale)
    {
        scale = 1f;
        Hashtable props = player?.CustomProperties;
        if (props == null || props.Count == 0)
            return false;

        for (int i = 0; i < ArmPropertyKeys.Length; i++)
        {
            if (!props.TryGetValue(ArmPropertyKeys[i], out object value))
                continue;

            if (TryConvertArmValue(value, out float parsed))
            {
                scale = parsed;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetArmScaleFromObject(object obj, out float scale)
    {
        scale = 1f;
        if (obj == null)
            return false;

        List<MemberInfo> members = GetArmMembers(obj.GetType());
        for (int i = 0; i < members.Count; i++)
        {
            object raw = ReadMemberValue(obj, members[i]);
            if (raw == null)
                continue;

            if (TryConvertArmValue(raw, out float parsed))
            {
                scale = parsed;
                return true;
            }
        }

        return false;
    }

    private static List<MemberInfo> GetArmMembers(Type type)
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
            if (LooksLikeArmMember(fields[i].Name) && IsSupportedType(fields[i].FieldType))
                list.Add(fields[i]);
        }

        PropertyInfo[] properties = type.GetProperties(flags);
        for (int i = 0; i < properties.Length; i++)
        {
            PropertyInfo p = properties[i];
            if (p.GetIndexParameters().Length != 0)
                continue;

            if (!LooksLikeArmMember(p.Name) || !IsSupportedType(p.PropertyType))
                continue;

            MethodInfo getter = p.GetGetMethod(true);
            if (getter != null)
                list.Add(p);
        }

        MemberCache[type] = list;
        return list;
    }

    private static bool LooksLikeArmMember(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        for (int i = 0; i < ArmMemberNameHints.Length; i++)
        {
            if (name.IndexOf(ArmMemberNameHints[i], StringComparison.OrdinalIgnoreCase) >= 0)
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
               t == typeof(Vector2) ||
               t == typeof(Vector3) ||
               t == typeof(bool);
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

    private static bool TryConvertArmValue(object value, out float scale)
    {
        scale = 1f;
        if (value == null)
            return false;

        float raw;

        if (value is bool b)
            raw = b ? 1.25f : 1f;
        else if (value is float f)
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
        else if (value is Vector2 v2)
            raw = (Mathf.Abs(v2.x) + Mathf.Abs(v2.y)) * 0.5f;
        else if (value is Vector3 v3)
            raw = (Mathf.Abs(v3.x) + Mathf.Abs(v3.y) + Mathf.Abs(v3.z)) / 3f;
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

        if (raw > 10f && raw <= 400f)
            raw /= 100f;

        if (raw < 0.6f || raw > 3f)
            return false;

        scale = raw;
        return true;
    }
}