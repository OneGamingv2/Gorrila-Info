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
    public bool lockPointerEnabled = true;
    public bool targetSphereEnabled = true;

    private bool _lastBPressed;
    private bool _lastRightTriggerPressed;
    private int _currentGunStyleIndex;
    private const float MaxDistance = 200f;
    private const float GunRayWidth = 0.006f;
    private const float GunSphereScale = 0.04f;
    private const float NametagDistance = 18f;
    private const float NametagHeight = 0.95f;
    private const float NametagTextSize = 0.078f;
    private const int MaxModsShownInTag = 2;
    private const float RigCacheRefreshInterval = 0.05f;
    private const float ModsRefreshInterval = 1.2f;
    private const float StatsRefreshInterval = 0.35f;
    private const float NametagUpdateInterval = 1f / 30f;
    private const float LockVisualUpdateInterval = 1f / 30f;
    private const float LockPointerWidth = 0.0045f;
    private const float LockSphereScale = 0.22f;
    private int _gunSizePresetIndex = 1;

    private static readonly float[] GunSizeMultipliers = new float[]
    {
        0.8f,
        1.0f,
        1.25f,
        1.5f
    };

    private readonly Dictionary<VRRig, NametagVisual> _nametags = new Dictionary<VRRig, NametagVisual>(24);
    private readonly Dictionary<VRRig, string> _modsNametagCache = new Dictionary<VRRig, string>(24);
    private readonly Dictionary<VRRig, float> _modsCacheTimestamp = new Dictionary<VRRig, float>(24);
    private readonly List<VRRig> _rigCache = new List<VRRig>(24);
    private readonly HashSet<VRRig> _activeRigSet = new HashSet<VRRig>();
    private readonly Dictionary<VRRig, string> _tagStatsCache = new Dictionary<VRRig, string>(24);
    private readonly Dictionary<VRRig, float> _tagStatsTimestamp = new Dictionary<VRRig, float>(24);
    private readonly Dictionary<VRRig, string> _tagRenderedTextCache = new Dictionary<VRRig, string>(24);
    private readonly Dictionary<VRRig, float> _targetSphereScaleCache = new Dictionary<VRRig, float>(24);
    private float _nextRigCacheRefreshTime;
    private float _nextNametagUpdateTime;
    private float _nextLockVisualUpdateTime;
    private LineRenderer _lockPointer;
    private GameObject _lockSphere;
    private Camera _mainCamera;

    private Camera GetMainCamera()
    {
        if (_mainCamera == null || !_mainCamera.gameObject.activeInHierarchy)
            _mainCamera = Camera.main;
        return _mainCamera;
    }

    private static readonly Color[] GunStyleColors = new Color[]
    {
        new Color(0.7f, 0.2f, 1f),
        new Color(1f, 0.2f, 0.2f),
        new Color(0.2f, 1f, 0.2f),
        new Color(1f, 1f, 0.2f),
        new Color(0.2f, 1f, 1f),
        new Color(1f, 0.55f, 0.1f),
        new Color(1f, 0.35f, 0.75f),
        new Color(0.85f, 0.85f, 0.85f),
        new Color(0.25f, 0.35f, 1f),
        new Color(0.1f, 0.1f, 0.1f)
    };

    private static readonly Color NametagMainColor = new Color(1f, 1f, 1f, 1f);
    private static readonly Color NametagShadowColor = new Color(0f, 0f, 0f, 0.95f);
    private static readonly Color NametagBackdropColor = new Color(0.05f, 0.05f, 0.07f, 0.72f);

    public void gunray()
    {
        if (!gunRayEnabled || GorillaInfoMain.Instance.menuState != GorillaInfoMain.MenuState.Open)
        {
            destroy();
            HideLockVisuals();
            return;
        }

        bool gripHeld = ControllerInputPoller.instance.rightControllerGripFloat > 0.8f;
        bool rightTriggerNow = SimpleInputs.RightTrigger;
        bool rightTriggerDown = rightTriggerNow && !_lastRightTriggerPressed;
        _lastRightTriggerPressed = rightTriggerNow;

        Transform hand = GorillaTagger.Instance.rightHandTransform;
        if (hand == null)
            return;

        Vector3 start = hand.position;
        if (!gripHeld)
        {
            destroy();
            UpdateLockVisuals(start);
            return;
        }

        if (gunRay == null)
            makeGun();

        Vector3 dir = hand.forward;

        RaycastHit hit;
        LayerMask mask = passThroughEnabled
            ? LayerMask.GetMask("GorillaPlayer", "GorillaInteractable")
            : -1;
        bool hitSomething = Physics.Raycast(start, dir, out hit, MaxDistance, mask);
        Vector3 end = hitSomething ? hit.point : start + dir * MaxDistance;

        if (autoLockEnabled && IsRigValid(lockedTarget))
        {
            Vector3 targetPos = lockedTarget.transform.position + Vector3.up * 0.35f;
            bool inRange = Vector3.Distance(start, targetPos) <= MaxDistance;
            bool clearSight = inRange && (passThroughEnabled || HasLineOfSight(start, targetPos, lockedTarget));

            if (clearSight)
                end = targetPos;
            else
            {
                autoLockEnabled = false;
                GorillaInfoMain.Instance.settingsHandler?.SetLockOnStateFromRuntime(false, false);
                GorillaInfoMain.Instance.updMain?.UpdateMainPage();
            }
        }

        if (gunRay != null && gunSphere != null)
        {
            gunRay.SetPosition(0, start);
            gunRay.SetPosition(1, end);
            gunSphere.transform.position = end;
        }

        if (rightTriggerDown)
        {
            VRRig hitRig = hitSomething ? hit.collider.GetComponentInParent<VRRig>() : null;

            if (!IsRigValid(hitRig))
                hitRig = FindClosestVRRigInDirection(start, dir);

            if (IsRigValid(hitRig))
            {
                if (TrySetLockedTarget(hitRig, !passThroughEnabled) && lockedTarget == hitRig)
                {
                    GorillaInfoMain.Instance.updMain.UpdateMainPage();
                }
            }
        }

        UpdateLockVisuals(start);
    }

    private VRRig FindClosestVRRigInDirection(Vector3 start, Vector3 dir)
    {
        VRRig bestRig = null;
        float bestScore = float.MinValue;
        Vector3 normalizedDir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;

        RefreshRigCache();

        for (int i = 0; i < _rigCache.Count; i++)
        {
            VRRig rig = _rigCache[i];
            if (!IsRigValid(rig))
                continue;

            Vector3 targetPos = rig.transform.position + Vector3.up * 0.35f;
            Vector3 toRig = targetPos - start;
            float dist = toRig.magnitude;
            if (dist < 0.01f || dist > MaxDistance)
                continue;

            Vector3 toRigDir = toRig / dist;
            float forwardDot = Vector3.Dot(normalizedDir, toRigDir);
            if (forwardDot < 0.35f)
                continue;

            float lateralDistance = Vector3.Cross(normalizedDir, toRig).magnitude;
            float allowedLateral = Mathf.Lerp(0.20f, 3.5f, Mathf.Clamp01(dist / MaxDistance));
            if (lateralDistance > allowedLateral)
                continue;

            if (!passThroughEnabled && !HasLineOfSight(start, targetPos, rig))
                continue;

            float score =
                (forwardDot * 2.35f) -
                (lateralDistance * 0.9f) -
                (dist * 0.012f);

            if (score > bestScore)
            {
                bestScore = score;
                bestRig = rig;
            }
        }

        return bestRig;
    }

    private void makeGun()
    {
        gunRay = new GameObject("GunRay").AddComponent<LineRenderer>();
        gunRay.material = new Material(Shader.Find("Sprites/Default"));
        gunRay.positionCount = 2;

        gunSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(gunSphere.GetComponent<Collider>());
        gunSphere.GetComponent<Renderer>().material = new Material(Shader.Find("Sprites/Default"));

        gunSphere.layer = LayerMask.NameToLayer("Ignore Raycast");

        ApplyGunSize();
        ApplyGunVisualStyle();
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
            ClearSelection();
            GorillaInfoMain.Instance.updMain.UpdateMainPage();
        }
    }

    public void ClearSelection()
    {
        bool hadSelection = IsRigValid(lockedTarget);
        lockedTarget = null;
        gunSuppressedAfterInfoUpdate = false;
        HideLockVisuals();

        if (hadSelection)
            GorillaInfoMain.Instance?.notificationManager?.Notify("Selection cleared");
    }

    public bool TrySetLockedTarget(VRRig target, bool requireLineOfSight)
    {
        if (!IsRigValid(target))
            return false;

        Vector3 origin = GetSelectionOrigin();
        Vector3 lockPos = target.transform.position + Vector3.up * 0.35f;

        if (Vector3.Distance(origin, lockPos) > MaxDistance)
            return false;

        if (requireLineOfSight && !HasLineOfSight(origin, lockPos, target))
            return false;

        if (lockedTarget == target)
            return true;

        lockedTarget = target;
        gunSuppressedAfterInfoUpdate = false;
        NotifySelection(target);
        return true;
    }

    private void NotifySelection(VRRig target)
    {
        if (target == null)
            return;

        string name = target.Creator?.GetPlayerRef()?.NickName;
        if (string.IsNullOrWhiteSpace(name))
            name = "Unknown";

        GorillaInfoMain.Instance?.notificationManager?.Notify($"Selected: <color=#00FFAA>{name}</color>");
    }

    public void OnMenuClosed()
    {
        _lastRightTriggerPressed = false;
        ClearSelection();
        destroy();
    }

    public void SetGunStyle(int styleIndex)
    {
        if (styleIndex < 0 || styleIndex >= GunStyleColors.Length) return;

        _currentGunStyleIndex = styleIndex;
        ApplyGunVisualStyle();
    }

    public int GetGunStyleCount()
    {
        return GunStyleColors.Length;
    }

    public void SetGunRayEnabled(bool enabled)
    {
        gunRayEnabled = enabled;
        if (!enabled)
            destroy();
    }

    public void SetTargetSphereEnabled(bool enabled)
    {
        targetSphereEnabled = enabled;
        if (!enabled && _lockSphere != null)
            _lockSphere.SetActive(false);
    }

    public void ForceNametagRefresh()
    {
        _nextRigCacheRefreshTime = 0f;
        _nextNametagUpdateTime = 0f;
        RefreshRigCache(true);
    }

    public int GetGunSizePresetIndex()
    {
        return _gunSizePresetIndex;
    }

    public void SetGunSizePreset(int presetIndex)
    {
        if (presetIndex < 0)
            presetIndex = 0;
        if (presetIndex >= GunSizeMultipliers.Length)
            presetIndex = GunSizeMultipliers.Length - 1;

        _gunSizePresetIndex = presetIndex;
        ApplyGunSize();
    }

    public int GetGunSizePresetCount()
    {
        return GunSizeMultipliers.Length;
    }

    private void ApplyGunSize()
    {
        float mult = GunSizeMultipliers[_gunSizePresetIndex];

        if (gunRay != null)
        {
            float width = GunRayWidth * mult;
            gunRay.startWidth = width;
            gunRay.endWidth = width;
        }

        if (gunSphere != null)
            gunSphere.transform.localScale = Vector3.one * GunSphereScale * mult;
    }

    private void ApplyGunVisualStyle()
    {
        Color styleColor = GunStyleColors[_currentGunStyleIndex];

        if (gunRay != null && gunRay.material != null)
            gunRay.material.color = styleColor;

        if (gunSphere != null)
        {
            Renderer gunRenderer = gunSphere.GetComponent<Renderer>();
            if (gunRenderer != null && gunRenderer.material != null)
                gunRenderer.material.color = styleColor;
        }

        if (_lockPointer != null && _lockPointer.material != null)
            _lockPointer.material.color = styleColor;

        if (_lockSphere != null)
        {
            Renderer lockRenderer = _lockSphere.GetComponent<Renderer>();
            if (lockRenderer != null && lockRenderer.material != null)
            {
                Color lockColor = styleColor;
                lockColor.a = 0.25f;
                lockRenderer.material.color = lockColor;
            }
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
                _tagRenderedTextCache.Remove(rig);
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
        tagTransform.position = targetPosition;

        Camera camera = GetMainCamera();
        if (camera != null)
        {
            Quaternion targetRotation = Quaternion.LookRotation(tagTransform.position - camera.transform.position);
            tagTransform.rotation = targetRotation;
        }

        string playerName = rig.Creator?.GetPlayerRef()?.NickName ?? "Unknown";
        string statsText = GetCachedStatsText(rig);
        string modsText = GetCachedModsNametagText(rig);

        bool visible = rig != null;

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

        if (!_tagRenderedTextCache.TryGetValue(rig, out string rendered) || !string.Equals(rendered, tagText))
        {
            visual.MainText.text = tagText;
            if (visual.ShadowText != null)
                visual.ShadowText.text = tagText;
            _tagRenderedTextCache[rig] = tagText;
        }

        if (visual.Backdrop != null)
            visual.Backdrop.enabled = false;
    }

    private NametagVisual CreateNametagVisual()
    {
        GameObject rootObj = new GameObject("GI_Nametag");
        Transform root = rootObj.transform;

        TextMesh main = CreateTagText(root, "Main", NametagMainColor, Vector3.zero);

        return new NametagVisual
        {
            Root = root,
            MainText = main,
            ShadowText = null,
            Backdrop = null
        };
    }

    private TextMesh CreateTagText(Transform parent, string name, Color color, Vector3 offset)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);
        textObj.transform.localPosition = offset;
        textObj.transform.localRotation = Quaternion.identity;

        TextMesh textMesh = textObj.AddComponent<TextMesh>();
        textMesh.fontSize = 100;
        textMesh.characterSize = NametagTextSize;
        textMesh.alignment = TextAlignment.Center;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.color = color;
        textMesh.fontStyle = FontStyle.Bold;
        textMesh.lineSpacing = 0.88f;

        Renderer renderer = textObj.GetComponent<Renderer>();
        if (renderer != null)
            ConfigureNametagRenderer(renderer);

        return textMesh;
    }

    private void ConfigureNametagRenderer(Renderer renderer)
    {
        Shader shader = Shader.Find("GUI/Text Shader") ?? Shader.Find("Sprites/Default");
        Material mat = new Material(shader);
        mat.renderQueue = 5000;

        if (mat.HasProperty("_ZWrite"))
            mat.SetInt("_ZWrite", 0);
        if (mat.HasProperty("_ZTest"))
            mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);

        renderer.material = mat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
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

    private string ParsePlatformForNametag(Platform platform)
    {
        switch (platform)
        {
            case Platform.Steam: return "Steam";
            case Platform.PC: return "PC";
            case Platform.Standalone:
            default: return "Quest";
        }
    }

    private void RefreshRigCache(bool force = false)
    {
        if (!force && Time.time < _nextRigCacheRefreshTime)
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

    private Vector3 GetSelectionOrigin()
    {
        Transform hand = GorillaTagger.Instance?.rightHandTransform;
        if (hand != null)
            return hand.position;

        Camera cam = GetMainCamera();
        if (cam != null)
            return cam.transform.position;

        return Vector3.zero;
    }

    private bool HasLineOfSight(Vector3 from, Vector3 to, VRRig targetRig)
    {
        if (!Physics.Linecast(from, to, out RaycastHit hit, -1, QueryTriggerInteraction.Ignore))
            return true;

        Transform hitTransform = hit.collider != null ? hit.collider.transform : null;
        if (hitTransform == null)
            return true;

        if (targetRig != null && hitTransform.IsChildOf(targetRig.transform))
            return true;

        Transform localRig = GorillaTagger.Instance?.offlineVRRig?.transform;
        if (localRig != null && hitTransform.IsChildOf(localRig))
            return true;

        return false;
    }

    private void EnsureLockVisuals()
    {
        if (_lockPointer == null)
        {
            _lockPointer = new GameObject("LockPointer").AddComponent<LineRenderer>();
            _lockPointer.startWidth = LockPointerWidth;
            _lockPointer.endWidth = LockPointerWidth;
            _lockPointer.material = new Material(Shader.Find("Sprites/Default"));
            _lockPointer.positionCount = 2;
            _lockPointer.enabled = false;
        }

        if (_lockSphere == null)
        {
            _lockSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _lockSphere.name = "SelectedTargetSphere";
            _lockSphere.transform.localScale = Vector3.one * LockSphereScale;
            Collider col = _lockSphere.GetComponent<Collider>();
            if (col != null)
                Object.Destroy(col);

            Renderer renderer = _lockSphere.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Sprites/Default"));
            }
            _lockSphere.layer = LayerMask.NameToLayer("Ignore Raycast");
            _lockSphere.SetActive(false);
        }

        ApplyGunVisualStyle();
    }

    private void UpdateLockVisuals(Vector3 start)
    {
        if (Time.time < _nextLockVisualUpdateTime)
            return;

        _nextLockVisualUpdateTime = Time.time + LockVisualUpdateInterval;

        if (!IsRigValid(lockedTarget))
        {
            HideLockVisuals();
            return;
        }

        EnsureLockVisuals();

        Vector3 targetPosition = lockedTarget.transform.position + Vector3.up * 0.35f;

        if (_lockSphere != null)
        {
            bool showSphere = targetSphereEnabled;
            _lockSphere.SetActive(showSphere);
            if (showSphere)
            {
                _lockSphere.transform.position = targetPosition;
                float pulse = 1f + Mathf.Sin(Time.time * 5.5f) * 0.05f;
                float targetScale = GetTargetSphereScale(lockedTarget);
                _lockSphere.transform.localScale = Vector3.one * targetScale * pulse;
            }
        }

        if (_lockPointer != null)
        {
            bool showPointer = autoLockEnabled && lockPointerEnabled;
            _lockPointer.enabled = showPointer;
            if (showPointer)
            {
                _lockPointer.SetPosition(0, start);
                _lockPointer.SetPosition(1, targetPosition);
            }
        }
    }

    private void HideLockVisuals()
    {
        if (_lockPointer != null)
            _lockPointer.enabled = false;

        if (_lockSphere != null)
            _lockSphere.SetActive(false);
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
        _tagRenderedTextCache.Clear();
        _targetSphereScaleCache.Clear();
        _activeRigSet.Clear();
    }

    private float GetTargetSphereScale(VRRig rig)
    {
        if (rig == null)
            return LockSphereScale;

        if (_targetSphereScaleCache.TryGetValue(rig, out float cached) && cached > 0.01f)
            return cached;

        float maxExtent = 0.45f;
        Renderer[] renderers = rig.GetComponentsInChildren<Renderer>(true);
        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                Bounds bounds = renderer.bounds;
                float localExtent = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
                if (localExtent > maxExtent)
                    maxExtent = localExtent;
            }
        }

        float scale = Mathf.Clamp(maxExtent * 1.55f, 0.55f, 1.45f);
        _targetSphereScaleCache[rig] = scale;
        return scale;
    }
}
