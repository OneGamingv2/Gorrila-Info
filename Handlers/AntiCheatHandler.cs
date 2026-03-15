using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace GorillaInfo
{
    /// <summary>
    /// Passive client-side anti-cheat that scans every remote player every frame
    /// (cheap) and on a longer interval (heavy). Detects:
    ///
    ///   • High predictions (preds)  — Photon update rate far above vanilla ~10/s
    ///   • Speed boost               — sustained speed above threshold
    ///   • Fly / pull / teleport     — large single-tick position jump
    ///   • Lag-switch                — dead-stop followed by large jump (freeze-then-jerk)
    ///   • Scale hack                — world-scale outside normal range
    ///
    /// Notifications are rate-limited per player per flag to avoid spam.
    /// </summary>
    public class AntiCheatHandler
    {
        // ── Heavy-scan tunables ──────────────────────────────────────────────
        private const float HeavyScanInterval  = 0.30f;  // 3.3 Hz — responsive but not jittery
        private const float SpeedThreshold     = 22f;    // m/s — GT max legit swing ~15-18 m/s
        private const float JumpThreshold      = 9f;     // metres in one 300ms tick → ~30 m/s min
        // fast-path (20Hz) consecutive ticks of extreme speed before flagging
        private const int   FastSpeedConsecutive = 8;    // 8×50ms = 400ms sustained extreme speed
        // distance per 50ms tick at 20Hz that counts as "extreme" (>40 m/s)
        private const float FastSpeedDist       = 2.0f; // 2m per 50ms = 40 m/s — clearly cheating
        // freeze: must be still for N heavy ticks before lag-switch can trigger
        private const int   LagSwitchFreezeMin  = 2;    // 2 consecutive still ticks before counting
        private const float LagSwitchStillDist = 0.03f; // <= this → considered "frozen" per tick
        private const float LagSwitchJumpDist  = 12f;   // metres after freeze → lag-switch (teleport)
        private const float ScaleMin           = 0.40f;
        private const float ScaleMax           = 1.60f;
        // per-player warmup: ignore all checks for this many seconds after first seen
        private const float PlayerWarmupSec     = 6f;

        // ── Prediction-rate tunables ─────────────────────────────────────────
        // Gorilla Tag's vanilla Photon serialisation rate is ~10 updates/sec.
        // We sample at FastSampleHz and accumulate changed-position events in a
        // rolling window. Anything above HighPredThreshold events/window is flagged.
        private const float FastSampleHz       = 20f;    // how often we sample positions
        private const float PredWindowSec      = 2f;     // rolling window length
        private const int   HighPredThreshold  = 35;     // events in window → flagged (≈17.5/s)

        // ── Alert cooldowns ──────────────────────────────────────────────────
        private const float AlertCooldownShort = 8f;     // for pred / lag-switch (frequent)
        private const float AlertCooldownLong  = 15f;    // for speed / fly / scale (rarer)

        // ── Rig cache ────────────────────────────────────────────────────────
        private const float RigCacheInterval   = 1.2f;
        private float _nextRigCacheTime;
        private VRRig[] _rigCache = new VRRig[0];

        // ── Timing ───────────────────────────────────────────────────────────
        private float _nextFastSampleTime;
        private float _nextHeavyScanTime;

        // ── Per-player tracking ──────────────────────────────────────────────
        // prediction rate
        private readonly Dictionary<string, Queue<float>> _predTimestamps
            = new Dictionary<string, Queue<float>>(32);
        private readonly Dictionary<string, Vector3>      _fastLastPos
            = new Dictionary<string, Vector3>(32);

        // heavy-scan
        private readonly Dictionary<string, Vector3> _heavyLastPos
            = new Dictionary<string, Vector3>(32);
        private readonly Dictionary<string, float>   _heavyLastTime
            = new Dictionary<string, float>(32);
        private readonly Dictionary<string, bool>    _wasStill
            = new Dictionary<string, bool>(32);  // kept for binary compat — superseded by _stillCount

        // fast-path consecutive high-speed counters
        private readonly Dictionary<string, int>     _fastSpeedCount
            = new Dictionary<string, int>(32);

        // per-player: Time.time when first seen (ignore detections during warmup)
        private readonly Dictionary<string, float>   _playerFirstSeen
            = new Dictionary<string, float>(32);

        // lag-switch: consecutive frozen tick count per player
        private readonly Dictionary<string, int>     _stillCount
            = new Dictionary<string, int>(32);

        // alert cooldowns
        private readonly Dictionary<string, float>   _alertExpiry
            = new Dictionary<string, float>(64);

        // ────────────────────────────────────────────────────────────────────

        public void Update()
        {
            if (!PhotonNetwork.InRoom)
            {
                ClearAll();
                return;
            }

            float now   = Time.time;
            string self = PhotonNetwork.LocalPlayer?.UserId;

            // refresh rig list
            if (now >= _nextRigCacheTime)
            {
                _nextRigCacheTime = now + RigCacheInterval;
                _rigCache = Object.FindObjectsByType<VRRig>(FindObjectsSortMode.None);
            }

            // fast prediction-rate sampling
            if (now >= _nextFastSampleTime)
            {
                _nextFastSampleTime = now + (1f / FastSampleHz);
                SamplePredictions(self, now);
            }

            // heavy behaviour scan
            if (now >= _nextHeavyScanTime)
            {
                _nextHeavyScanTime = now + HeavyScanInterval;
                HeavyScan(self, now);
            }
        }

        // ── Prediction-rate scan (fast) ──────────────────────────────────────

        private void SamplePredictions(string self, float now)
        {
            for (int i = 0; i < _rigCache.Length; i++)
            {
                VRRig rig = _rigCache[i];
                if (rig == null) continue;

                var player = rig.Creator?.GetPlayerRef();
                if (player == null) continue;

                string uid = player.UserId;
                if (string.IsNullOrEmpty(uid) || uid == self) continue;

                // register first-seen time for warmup
                if (!_playerFirstSeen.TryGetValue(uid, out float firstSeen))
                {
                    _playerFirstSeen[uid] = now;
                    _fastLastPos[uid] = rig.transform.position; // seed so first delta is zero
                    continue;
                }

                bool inWarmup = (now - firstSeen) < PlayerWarmupSec;

                Vector3 pos = rig.transform.position;

                // Did this player's network position change since last sample?
                bool hasPrev = _fastLastPos.TryGetValue(uid, out Vector3 prev);
                bool changed = !hasPrev || (pos - prev).sqrMagnitude > 0.0001f;

                // fast-path fly/speed: check sustained movement across consecutive ticks
                if (changed && hasPrev && !inWarmup)
                {
                    float fastDist = (pos - prev).magnitude;
                    if (fastDist > FastSpeedDist)
                    {
                        int cnt = _fastSpeedCount.TryGetValue(uid, out int c) ? c + 1 : 1;
                        _fastSpeedCount[uid] = cnt;
                        if (cnt >= FastSpeedConsecutive)
                        {
                            float approxSpeed = fastDist * FastSampleHz;
                            string pname = player.NickName ?? uid;
                            Alert(uid, "fastflyspeed", AlertCooldownLong,
                                $"<color=red>[AC]</color> <color=yellow>{pname}</color> "
                                + $"<color=red>FLY/SPEED</color>  (~{approxSpeed:F0} m/s sustained)");
                        }
                    }
                    else
                    {
                        _fastSpeedCount[uid] = 0;
                    }
                }

                _fastLastPos[uid] = pos;

                if (!changed) continue;

                // Accumulate into rolling window
                if (!_predTimestamps.TryGetValue(uid, out Queue<float> q))
                {
                    q = new Queue<float>(64);
                    _predTimestamps[uid] = q;
                }

                q.Enqueue(now);

                // Expire old entries
                while (q.Count > 0 && now - q.Peek() > PredWindowSec)
                    q.Dequeue();

                if (q.Count > HighPredThreshold)
                {
                    float rate = q.Count / PredWindowSec;
                    string name = player.NickName ?? uid;
                    Alert(uid, "pred", AlertCooldownShort,
                        $"<color=red>[AC]</color> <color=yellow>{name}</color> " +
                        $"<color=red>HIGH PREDS</color>  ({rate:F0}/s  ~vanilla 10/s)");
                }
            }
        }

        // ── Heavy behavioural scan ───────────────────────────────────────────

        private void HeavyScan(string self, float now)
        {
            for (int i = 0; i < _rigCache.Length; i++)
            {
                VRRig rig = _rigCache[i];
                if (rig == null) continue;

                var player = rig.Creator?.GetPlayerRef();
                if (player == null) continue;

                string uid = player.UserId;
                if (string.IsNullOrEmpty(uid) || uid == self) continue;

                // skip this player if still in warmup window
                if (_playerFirstSeen.TryGetValue(uid, out float fs) && (now - fs) < PlayerWarmupSec)
                {
                    _heavyLastPos[uid]  = rig.transform.position;
                    _heavyLastTime[uid] = now;
                    continue;
                }

                string name = player.NickName ?? uid;
                Vector3 pos = rig.transform.position;

                bool havePrev = _heavyLastPos.TryGetValue(uid, out Vector3 prev)
                             & _heavyLastTime.TryGetValue(uid, out float prevT);

                if (havePrev)
                {
                    float dt   = now - prevT;
                    float dist = Vector3.Distance(pos, prev);

                    if (dt > 0f)
                    {
                        // (wasStill is superseded by _stillCount)

                        if (dist <= LagSwitchStillDist)
                        {
                            // increment frozen-tick counter
                            int sc = _stillCount.TryGetValue(uid, out int sv) ? sv + 1 : 1;
                            _stillCount[uid] = sc;
                        }
                        else if (_stillCount.TryGetValue(uid, out int frozen) && frozen >= LagSwitchFreezeMin
                                 && dist > LagSwitchJumpDist)
                        {
                            // multiple frozen ticks → big jump = lag-switch
                            _stillCount[uid] = 0;
                            Alert(uid, "lagswitch", AlertCooldownShort,
                                $"<color=red>[AC]</color> <color=yellow>{name}</color> " +
                                $"<color=red>LAG SWITCH</color>  (freeze + {dist:F1} m jump)");
                        }
                        else
                        {
                            _stillCount[uid] = 0;

                            if (dist > JumpThreshold)
                            {
                                Alert(uid, "fly", AlertCooldownShort,
                                    $"<color=red>[AC]</color> <color=yellow>{name}</color> " +
                                    $"<color=red>FLY / PULL</color>  ({dist:F1} m jump)");
                            }
                            else
                            {
                                float speed = dist / dt;
                                if (speed > SpeedThreshold)
                                {
                                    Alert(uid, "speed", AlertCooldownLong,
                                        $"<color=red>[AC]</color> <color=yellow>{name}</color> " +
                                        $"<color=red>SPEED BOOST</color>  ({speed:F0} m/s)");
                                }
                            }
                        }
                    }
                }

                _heavyLastPos[uid]  = pos;
                _heavyLastTime[uid] = now;

                // World-scale check
                float ws2 = WorldScaleResolver.GetWorldScale(rig);
                if (ws2 < ScaleMin || ws2 > ScaleMax)
                {
                    int pct = Mathf.RoundToInt(ws2 * 100f);
                    Alert(uid, "scale", AlertCooldownLong,
                        $"<color=red>[AC]</color> <color=yellow>{name}</color> " +
                        $"<color=red>SCALE HACK</color>  ({pct}%)");
                }
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private void Alert(string uid, string type, float cooldown, string message)
        {
            string key = uid + "_" + type;
            if (_alertExpiry.TryGetValue(key, out float expiry) && Time.time < expiry)
                return;

            _alertExpiry[key] = Time.time + cooldown;
            GorillaInfoMain.Instance?.notificationManager?.Notify(message);
        }

        private void ClearAll()
        {
            _predTimestamps.Clear();
            _fastLastPos.Clear();
            _fastSpeedCount.Clear();
            _playerFirstSeen.Clear();
            _stillCount.Clear();
            _heavyLastPos.Clear();
            _heavyLastTime.Clear();
            _wasStill.Clear();
            _rigCache = new VRRig[0];
        }
    }
}

