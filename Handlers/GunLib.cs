using UnityEngine;
using Photon.Pun;
using GorillaInfo;
using Checker;
using System.Collections.Generic;
using Photon.Realtime;

public class GunLib
{
    private sealed class NametagVisual
    {
        public Transform Root;
        public TextMesh MainText;
        public TextMesh ShadowText;
        public Renderer Backdrop;
    }

    public LineRenderer gunRay;
    public GameObject gunSphere;
    public bool gunRayEnabled = true;
    public bool autoLockEnabled;
    public bool gunSuppressedAfterInfoUpdate;
    public bool nametagsEnabled;
    public bool passThroughEnabled;
    public VRRig lockedTarget;

    private bool _lastBPressed;
    private int _currentGunStyleIndex;
    private const float MaxDistance = 50f;
    private const float GunRayWidth = 0.006f;
    private const float GunSphereScale = 0.15f;
    private const float NametagDistance = 18f;
    private const float NametagHeight = 0.95f;
    private const float NametagTextSize = 0.078f;
    private const int MaxModsShownInTag = 2;
    private const float NametagPositionLerp = 18f;
    private const float NametagRotationLerp = 20f;
    private const float RigCacheRefreshInterval = 0.2f;
    private const float ModsRefreshInterval = 1.2f;
    private const float StatsRefreshInterval = 0.35f;
    private const float NametagUpdateInterval = 0.04f;

    private readonly Dictionary<VRRig, NametagVisual> _nametags = new Dictionary<VRRig, NametagVisual>(24);
    private readonly Dictionary<VRRig, string> _modsNametagCache = new Dictionary<VRRig, string>(24);
    private readonly Dictionary<VRRig, float> _modsCacheTimestamp = new Dictionary<VRRig, float>(24);
    private readonly List<VRRig> _rigCache = new List<VRRig>(24);
    private readonly HashSet<VRRig> _activeRigSet = new HashSet<VRRig>();
    private readonly Dictionary<VRRig, string> _tagStatsCache = new Dictionary<VRRig, string>(24);
    private readonly Dictionary<VRRig, float> _tagStatsTimestamp = new Dictionary<VRRig, float>(24);
    private float _nextRigCacheRefreshTime;
    private float _nextNametagUpdateTime;

    private static readonly Color[] GunStyleColors = new Color[]
    {
        new Color(0.7f, 0.2f, 1f),
        new Color(1f, 0.2f, 0.2f),
        new Color(0.2f, 1f, 0.2f),
        new Color(1f, 1f, 0.2f)
    };

    private static readonly Color NametagMainColor = new Color(1f, 1f, 1f, 1f);
    private static readonly Color NametagShadowColor = new Color(0f, 0f, 0f, 0.95f);
    private static readonly Color NametagBackdropColor = new Color(0.05f, 0.05f, 0.07f, 0.72f);

    public void gunray()
    {
        if (!gunRayEnabled || GorillaInfoMain.Instance.menuState != GorillaInfoMain.MenuState.Open)
        {
            destroy();
            return;
        }

        bool gripHeld = ControllerInputPoller.instance.rightControllerGripFloat > 0.8f;
        if (!gripHeld && !autoLockEnabled)
        {
            destroy();
            return;
        }

        if (gunRay == null && gripHeld) makeGun();

        Transform hand = GorillaTagger.Instance.rightHandTransform;
        Vector3 start = hand.position;
        Vector3 dir = hand.forward;

        RaycastHit hit;
        LayerMask mask = passThroughEnabled ? LayerMask.GetMask("Default") : -1;
        bool hitSomething = Physics.Raycast(start, dir, out hit, MaxDistance, mask);
        Vector3 end = hitSomething ? hit.point : start + dir * MaxDistance;

        if (autoLockEnabled && IsRigValid(lockedTarget))
        {
            Vector3 targetPos = lockedTarget.transform.position + Vector3.up * 0.35f;
            if (Vector3.Distance(start, targetPos) <= MaxDistance)
                end = targetPos;
        }

        if (gunRay != null && gunSphere != null)
        {
            gunRay.SetPosition(0, start);
            gunRay.SetPosition(1, end);
            gunSphere.transform.position = end;
        }

        if (!hitSomething && !passThroughEnabled && !autoLockEnabled) return;

        VRRig hitRig = passThroughEnabled ? FindClosestVRRigInDirection(start, dir) : (hitSomething ? hit.collider.GetComponentInParent<VRRig>() : null);

        if (hitRig == null || hitRig == GorillaTagger.Instance.offlineVRRig) return;

        if (SimpleInputs.RightTrigger && lockedTarget != hitRig)
        {
            lockedTarget = hitRig;
            GorillaInfoMain.Instance.updMain.UpdateMainPage();
        }
    }

    private VRRig FindClosestVRRigInDirection(Vector3 start, Vector3 dir)
    {
        VRRig closest = null;
        float closestDist = NametagDistance;
        RefreshRigCache();

        for (int i = 0; i < _rigCache.Count; i++)
        {
            VRRig rig = _rigCache[i];
            if (rig == GorillaTagger.Instance.offlineVRRig) continue;

            Vector3 rigPos = rig.transform.position;
            float dist = Vector3.Distance(start, rigPos);
            if (dist > closestDist) continue;

            Vector3 toRig = (rigPos - start).normalized;
            if (Vector3.Dot(dir, toRig) < 0.7f) continue;

            closestDist = dist;
            closest = rig;
        }
        return closest;
    }

    private void makeGun()
    {
        gunRay = new GameObject("GunRay").AddComponent<LineRenderer>();
        gunRay.startWidth = GunRayWidth;
        gunRay.endWidth = GunRayWidth;
        gunRay.material = new Material(Shader.Find("Sprites/Default"));

        gunSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        gunSphere.transform.localScale = Vector3.one * GunSphereScale;
        Object.Destroy(gunSphere.GetComponent<Collider>());

        gunRay.material.color = GunStyleColors[0];
        gunSphere.GetComponent<Renderer>().material = gunRay.material;
        gunSphere.layer = LayerMask.NameToLayer("Ignore Raycast");
    }

    public void destroy()
    {
        if (gunRay != null) Object.Destroy(gunRay.gameObject);
        if (gunSphere != null) Object.Destroy(gunSphere.gameObject);
        gunRay = null;
        gunSphere = null;
    }

    public void rearmgun()
    {
        bool bNow = ControllerInputPoller.instance.rightControllerSecondaryButton;
        bool bDown = bNow && !_lastBPressed;
        _lastBPressed = bNow;

        if (bDown)
        {
            lockedTarget = null;
            gunSuppressedAfterInfoUpdate = false;
            GorillaInfoMain.Instance.updMain.UpdateMainPage();
        }
    }

    public void SetGunStyle(int styleIndex)
    {
        if (styleIndex < 0 || styleIndex >= GunStyleColors.Length) return;

        _currentGunStyleIndex = styleIndex;

        if (gunRay != null)
        {
            gunRay.material.color = GunStyleColors[styleIndex];
            if (gunSphere != null)
                gunSphere.GetComponent<Renderer>().material.color = GunStyleColors[styleIndex];
        }
    }

    public void UpdateNametags()
    {
        if (!nametagsEnabled)
        {
            ClearNametags();
            return;
        }

        if (Time.time < _nextNametagUpdateTime)
            return;

        _nextNametagUpdateTime = Time.time + NametagUpdateInterval;

        RefreshRigCache();
        _activeRigSet.Clear();

        for (int i = 0; i < _rigCache.Count; i++)
        {
            VRRig rig = _rigCache[i];
            if (rig == null || rig == GorillaTagger.Instance.offlineVRRig)
                continue;

            _activeRigSet.Add(rig);
            UpdateNametagForRig(rig);
        }

        List<VRRig> toRemove = null;
        foreach (var kv in _nametags)
        {
            if (!_activeRigSet.Contains(kv.Key) || kv.Key == null)
            {
                if (toRemove == null)
                    toRemove = new List<VRRig>();
                toRemove.Add(kv.Key);
            }
        }

        if (toRemove != null)
        {
            for (int i = 0; i < toRemove.Count; i++)
            {
                VRRig rig = toRemove[i];
                if (_nametags.TryGetValue(rig, out NametagVisual visual) && visual?.Root != null)
                    Object.Destroy(visual.Root.gameObject);
                _nametags.Remove(rig);
                _modsNametagCache.Remove(rig);
                _modsCacheTimestamp.Remove(rig);
                _tagStatsCache.Remove(rig);
                _tagStatsTimestamp.Remove(rig);
            }
        }
    }

    private void UpdateNametagForRig(VRRig rig)
    {
        if (!_nametags.TryGetValue(rig, out NametagVisual visual) || visual?.Root == null)
        {
            visual = CreateNametagVisual();
            _nametags[rig] = visual;
        }

        Transform tagTransform = visual.Root;
        Vector3 targetPosition = rig.transform.position + Vector3.up * NametagHeight;
        if (tagTransform.position == Vector3.zero)
            tagTransform.position = targetPosition;
        else
            tagTransform.position = Vector3.Lerp(tagTransform.position, targetPosition, Time.deltaTime * NametagPositionLerp);

        Camera camera = Camera.main;
        if (camera != null)
        {
            Quaternion targetRotation = Quaternion.LookRotation(tagTransform.position - camera.transform.position);
            tagTransform.rotation = Quaternion.Slerp(tagTransform.rotation, targetRotation, Time.deltaTime * NametagRotationLerp);
        }

        string playerName = rig.Creator?.GetPlayerRef()?.NickName ?? "Unknown";
        string statsText = GetCachedStatsText(rig);
        string modsText = GetCachedModsNametagText(rig);

        bool visible = IsTagVisibleToCamera(rig, tagTransform.position);
        if (visual.MainText != null)
            visual.MainText.gameObject.SetActive(visible);
        if (visual.ShadowText != null)
            visual.ShadowText.gameObject.SetActive(visible);
        if (visual.Backdrop != null)
            visual.Backdrop.enabled = visible;

        if (!visible)
            return;

        string tagText = string.IsNullOrEmpty(modsText)
            ? $"<color=#FFFFFF>{playerName}</color>\n<color=#A8D8FF>{statsText}</color>"
            : $"<color=#FFFFFF>{playerName}</color>\n<color=#A8D8FF>{statsText}</color>\n<color=#8CFF9B>{modsText}</color>";

        visual.MainText.text = tagText;
        visual.ShadowText.text = tagText;

        float lineCount = string.IsNullOrEmpty(modsText) ? 2f : 3f;
        Vector3 bgScale = new Vector3(0.56f, 0.12f + (lineCount - 2f) * 0.06f, 1f);
        if (visual.Backdrop != null)
            visual.Backdrop.transform.localScale = bgScale;
    }

    private NametagVisual CreateNametagVisual()
    {
        GameObject rootObj = new GameObject("GI_Nametag");
        Transform root = rootObj.transform;

        GameObject bgObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bgObj.name = "BG";
        bgObj.transform.SetParent(root, false);
        bgObj.transform.localPosition = Vector3.zero;
        bgObj.transform.localRotation = Quaternion.identity;
        bgObj.transform.localScale = new Vector3(0.56f, 0.12f, 1f);

        Collider bgCollider = bgObj.GetComponent<Collider>();
        if (bgCollider != null)
            Object.Destroy(bgCollider);

        Renderer bgRenderer = bgObj.GetComponent<Renderer>();
        if (bgRenderer != null)
        {
            Material bgMaterial = new Material(Shader.Find("Sprites/Default"));
            bgMaterial.color = NametagBackdropColor;
            bgRenderer.material = bgMaterial;
        }

        TextMesh shadow = CreateTagText(root, "Shadow", NametagShadowColor, new Vector3(0.0022f, -0.0018f, -0.001f));
        TextMesh main = CreateTagText(root, "Main", NametagMainColor, new Vector3(0f, 0f, -0.002f));

        return new NametagVisual
        {
            Root = root,
            MainText = main,
            ShadowText = shadow,
            Backdrop = bgRenderer
        };
    }

    private TextMesh CreateTagText(Transform parent, string name, Color color, Vector3 offset)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);
        textObj.transform.localPosition = offset;
        textObj.transform.localRotation = Quaternion.identity;

        TextMesh textMesh = textObj.AddComponent<TextMesh>();
        textMesh.characterSize = NametagTextSize;
        textMesh.alignment = TextAlignment.Center;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.color = color;
        textMesh.fontStyle = FontStyle.Bold;
        textMesh.lineSpacing = 0.88f;
        return textMesh;
    }

    private string GetCachedStatsText(VRRig rig)
    {
        if (rig == null)
            return "Platform: Unknown | FPS: - | Ping: -";

        if (_tagStatsCache.TryGetValue(rig, out string cached) &&
            _tagStatsTimestamp.TryGetValue(rig, out float ts) &&
            Time.time - ts < StatsRefreshInterval)
        {
            return cached;
        }

        int fps = rig.GetFPS();
        int ping = GetPlayerPing(rig);
        string platform = ParsePlatformForNametag(rig.GetPlatform());
        string value = $"{platform} | {fps} FPS | {ping}ms";
        _tagStatsCache[rig] = value;
        _tagStatsTimestamp[rig] = Time.time;
        return value;
    }

    private bool IsTagVisibleToCamera(VRRig rig, Vector3 tagPosition)
    {
        Camera camera = Camera.main;
        if (camera == null)
            return true;

        if (!Physics.Linecast(camera.transform.position, tagPosition, out RaycastHit hit, -1, QueryTriggerInteraction.Ignore))
            return true;

        Transform hitTransform = hit.collider != null ? hit.collider.transform : null;
        if (hitTransform == null)
            return true;

        if (rig != null && hitTransform.IsChildOf(rig.transform))
            return true;

        Transform localRig = GorillaTagger.Instance?.offlineVRRig?.transform;
        if (localRig != null && hitTransform.IsChildOf(localRig))
            return true;

        return false;
    }

    private string ParsePlatformForNametag(Platform platform)
    {
        switch (platform)
        {
            case Platform.Steam: return "Steam";
            case Platform.PC: return "PC";
            case Platform.Standalone: return "Quest";
            default: return "Unknown";
        }
    }

    private void RefreshRigCache()
    {
        if (Time.time < _nextRigCacheRefreshTime)
            return;

        _nextRigCacheRefreshTime = Time.time + RigCacheRefreshInterval;
        _rigCache.Clear();

        VRRig[] rigs = Object.FindObjectsByType<VRRig>(FindObjectsSortMode.None);
        for (int i = 0; i < rigs.Length; i++)
        {
            if (rigs[i] != null)
                _rigCache.Add(rigs[i]);
        }
    }

    private bool IsRigValid(VRRig rig)
    {
        return rig != null && rig != GorillaTagger.Instance.offlineVRRig;
    }

    private string GetCachedModsNametagText(VRRig rig)
    {
        if (rig == null)
            return "Mods: None";

        if (_modsNametagCache.TryGetValue(rig, out string cached) &&
            _modsCacheTimestamp.TryGetValue(rig, out float ts) &&
            Time.time - ts < ModsRefreshInterval)
        {
            return cached;
        }

        string newValue = BuildModsNametagText(rig);
        _modsNametagCache[rig] = newValue;
        _modsCacheTimestamp[rig] = Time.time;
        return newValue;
    }

    private string BuildModsNametagText(VRRig rig)
    {
        var utilities = GorillaInfoMain.Instance?.utilities;
        if (utilities == null)
            return string.Empty;

        List<string> detectedMods = utilities.DetectAllMods(rig);
        if (detectedMods == null || detectedMods.Count == 0)
            return "Mods: None";

        int shownCount = Mathf.Min(MaxModsShownInTag, detectedMods.Count);
        string shown = string.Join(", ", detectedMods.GetRange(0, shownCount));

        if (detectedMods.Count > shownCount)
            return $"Mods: {shown} +{detectedMods.Count - shownCount}";

        return $"Mods: {shown}";
    }

    private int GetPlayerPing(VRRig rig)
    {
        Player playerRef = rig?.Creator?.GetPlayerRef();
        if (playerRef?.CustomProperties != null)
        {
            object value;

            if (playerRef.CustomProperties.TryGetValue("ping", out value) ||
                playerRef.CustomProperties.TryGetValue("Ping", out value) ||
                playerRef.CustomProperties.TryGetValue("latency", out value) ||
                playerRef.CustomProperties.TryGetValue("Latency", out value))
            {
                if (value is int intPing)
                    return intPing;

                if (value != null && int.TryParse(value.ToString(), out int parsedPing))
                    return parsedPing;
            }
        }

        return PhotonNetwork.GetPing();
    }

    private void ClearNametags()
    {
        foreach (var kv in _nametags)
        {
            if (kv.Value?.Root != null)
                Object.Destroy(kv.Value.Root.gameObject);
        }
        _nametags.Clear();
        _modsNametagCache.Clear();
        _modsCacheTimestamp.Clear();
        _tagStatsCache.Clear();
        _tagStatsTimestamp.Clear();
        _activeRigSet.Clear();
    }
}
