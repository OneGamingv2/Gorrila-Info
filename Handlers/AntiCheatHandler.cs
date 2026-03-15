using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace GorillaInfo
{
    public class AntiCheatHandler
    {
        private const float HeavyScanInterval = 0.30f;
        private const float SpeedThreshold = 22f;
        private const float JumpThreshold = 9f;
        private const int FastSpeedConsecutive = 8;
        private const float FastSpeedDist = 2.0f;
        private const int LagSwitchFreezeMin = 2;
        private const float LagSwitchStillDist = 0.03f;
        private const float LagSwitchJumpDist = 12f;
        private const float ScaleMin           = 0.40f;
        private const float ScaleMax           = 1.60f;
        private const float PlayerWarmupSec = 6f;

        private const float FastSampleHz = 20f;
        private const float PredWindowSec = 2f;
        private const int HighPredThreshold = 44;

        private const float AlertCooldownShort = 8f;
        private const float AlertCooldownLong  = 15f;
        private const float MinHeavyDt = 0.10f;
        private const float MaxHeavyDt = 1.10f;

        private const float RigCacheInterval   = 1.2f;
        private float _nextRigCacheTime;
        private VRRig[] _rigCache = new VRRig[0];

        private float _nextFastSampleTime;
        private float _nextHeavyScanTime;

        private readonly Dictionary<string, Queue<float>> _predTimestamps
            = new Dictionary<string, Queue<float>>(32);
        private readonly Dictionary<string, Vector3>      _fastLastPos
            = new Dictionary<string, Vector3>(32);

        private readonly Dictionary<string, Vector3> _heavyLastPos
            = new Dictionary<string, Vector3>(32);
        private readonly Dictionary<string, float>   _heavyLastTime
            = new Dictionary<string, float>(32);

        private readonly Dictionary<string, int>     _fastSpeedCount
            = new Dictionary<string, int>(32);

        private readonly Dictionary<string, float>   _playerFirstSeen
            = new Dictionary<string, float>(32);

        private readonly Dictionary<string, int>     _stillCount
            = new Dictionary<string, int>(32);

        private readonly Dictionary<string, int>     _predStrikes
            = new Dictionary<string, int>(32);
        private readonly Dictionary<string, int>     _speedStrikes
            = new Dictionary<string, int>(32);
        private readonly Dictionary<string, int>     _jumpStrikes
            = new Dictionary<string, int>(32);
        private readonly Dictionary<string, int>     _scaleStrikes
            = new Dictionary<string, int>(32);

        private readonly Dictionary<string, float>   _alertExpiry
            = new Dictionary<string, float>(64);

        public void Update()
        {
            if (!PhotonNetwork.InRoom)
            {
                ClearAll();
                return;
            }

            float now   = Time.time;
            string self = PhotonNetwork.LocalPlayer?.UserId;

            if (now >= _nextRigCacheTime)
            {
                _nextRigCacheTime = now + RigCacheInterval;
                _rigCache = Object.FindObjectsByType<VRRig>(FindObjectsSortMode.None);
            }

            if (now >= _nextFastSampleTime)
            {
                _nextFastSampleTime = now + (1f / FastSampleHz);
                SamplePredictions(self, now);
            }

            if (now >= _nextHeavyScanTime)
            {
                _nextHeavyScanTime = now + HeavyScanInterval;
                HeavyScan(self, now);
            }
        }

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

                if (!_playerFirstSeen.TryGetValue(uid, out float firstSeen))
                {
                    _playerFirstSeen[uid] = now;
                    _fastLastPos[uid] = rig.transform.position;
                    continue;
                }

                bool inWarmup = (now - firstSeen) < PlayerWarmupSec;

                Vector3 pos = rig.transform.position;

                bool hasPrev = _fastLastPos.TryGetValue(uid, out Vector3 prev);
                bool changed = !hasPrev || (pos - prev).sqrMagnitude > 0.0025f;

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

                if (!_predTimestamps.TryGetValue(uid, out Queue<float> q))
                {
                    q = new Queue<float>(64);
                    _predTimestamps[uid] = q;
                }

                q.Enqueue(now);

                while (q.Count > 0 && now - q.Peek() > PredWindowSec)
                    q.Dequeue();

                if (q.Count > HighPredThreshold)
                {
                    int strikes = AddStrike(_predStrikes, uid, 1, 6);
                    if (strikes >= 3)
                    {
                        float rate = q.Count / PredWindowSec;
                        string name = player.NickName ?? uid;
                        Alert(uid, "pred", AlertCooldownShort,
                            $"<color=red>[AC]</color> <color=yellow>{name}</color> " +
                            $"<color=red>HIGH PREDS</color>  ({rate:F0}/s  ~vanilla 10/s)");
                        _predStrikes[uid] = 0;
                    }
                }
                else
                {
                    DecayStrike(_predStrikes, uid);
                }
            }
        }

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

                    if (dt < MinHeavyDt || dt > MaxHeavyDt)
                    {
                        _heavyLastPos[uid] = pos;
                        _heavyLastTime[uid] = now;
                        continue;
                    }

                    if (dt > 0f)
                    {
                        int ping = GetPlayerPing(player);
                        float pingBoost = Mathf.Clamp(ping, 0f, 250f) * 0.02f;
                        float speedThreshold = SpeedThreshold + pingBoost;
                        float jumpThreshold = JumpThreshold + (pingBoost * 0.35f);

                        if (dist <= LagSwitchStillDist)
                        {
                            int sc = _stillCount.TryGetValue(uid, out int sv) ? sv + 1 : 1;
                            _stillCount[uid] = sc;
                        }
                        else if (_stillCount.TryGetValue(uid, out int frozen) && frozen >= LagSwitchFreezeMin
                                 && dist > (LagSwitchJumpDist + (pingBoost * 0.2f)))
                        {
                            _stillCount[uid] = 0;
                            Alert(uid, "lagswitch", AlertCooldownShort,
                                $"<color=red>[AC]</color> <color=yellow>{name}</color> " +
                                $"<color=red>LAG SWITCH</color>  (freeze + {dist:F1} m jump)");
                        }
                        else
                        {
                            _stillCount[uid] = 0;

                            float speed = dist / dt;

                            if (dist > jumpThreshold && speed > speedThreshold * 1.15f)
                            {
                                int jumpStrike = AddStrike(_jumpStrikes, uid, 1, 4);
                                if (jumpStrike >= 2)
                                {
                                    Alert(uid, "fly", AlertCooldownShort,
                                        $"<color=red>[AC]</color> <color=yellow>{name}</color> " +
                                        $"<color=red>FLY / PULL</color>  ({dist:F1} m jump)");
                                    _jumpStrikes[uid] = 0;
                                }
                            }
                            else
                            {
                                DecayStrike(_jumpStrikes, uid);

                                if (speed > speedThreshold)
                                {
                                    int speedStrike = AddStrike(_speedStrikes, uid, 1, 5);
                                    if (speedStrike >= 3)
                                    {
                                        Alert(uid, "speed", AlertCooldownLong,
                                            $"<color=red>[AC]</color> <color=yellow>{name}</color> " +
                                            $"<color=red>SPEED BOOST</color>  ({speed:F0} m/s)");
                                        _speedStrikes[uid] = 0;
                                    }
                                }
                                else
                                {
                                    DecayStrike(_speedStrikes, uid);
                                }
                            }
                        }
                    }
                }

                _heavyLastPos[uid]  = pos;
                _heavyLastTime[uid] = now;

                float ws2 = WorldScaleResolver.GetWorldScale(rig);
                if (ws2 < ScaleMin || ws2 > ScaleMax)
                {
                    int strike = AddStrike(_scaleStrikes, uid, 1, 5);
                    if (strike >= 3)
                    {
                        int pct = Mathf.RoundToInt(ws2 * 100f);
                        Alert(uid, "scale", AlertCooldownLong,
                            $"<color=red>[AC]</color> <color=yellow>{name}</color> " +
                            $"<color=red>SCALE HACK</color>  ({pct}%)");
                        _scaleStrikes[uid] = 0;
                    }
                }
                else
                {
                    DecayStrike(_scaleStrikes, uid);
                }
            }
        }

        private void Alert(string uid, string type, float cooldown, string message)
        {
            string key = uid + "_" + type;
            if (_alertExpiry.TryGetValue(key, out float expiry) && Time.time < expiry)
                return;

            _alertExpiry[key] = Time.time + cooldown;
            GorillaInfoMain.Instance?.notificationManager?.Notify(message);
        }

        private int AddStrike(Dictionary<string, int> dict, string uid, int amount, int max)
        {
            int next = (dict.TryGetValue(uid, out int cur) ? cur : 0) + amount;
            if (next > max)
                next = max;
            dict[uid] = next;
            return next;
        }

        private void DecayStrike(Dictionary<string, int> dict, string uid)
        {
            if (!dict.TryGetValue(uid, out int cur) || cur <= 0)
                return;

            cur -= 1;
            if (cur <= 0)
                dict.Remove(uid);
            else
                dict[uid] = cur;
        }

        private int GetPlayerPing(Photon.Realtime.Player player)
        {
            if (player?.CustomProperties != null)
            {
                object value;
                if (player.CustomProperties.TryGetValue("ping", out value) ||
                    player.CustomProperties.TryGetValue("Ping", out value) ||
                    player.CustomProperties.TryGetValue("latency", out value) ||
                    player.CustomProperties.TryGetValue("Latency", out value))
                {
                    if (value is int i)
                        return i;
                    if (value != null && int.TryParse(value.ToString(), out int parsed))
                        return parsed;
                }
            }

            return PhotonNetwork.GetPing();
        }

        private void ClearAll()
        {
            _predTimestamps.Clear();
            _fastLastPos.Clear();
            _fastSpeedCount.Clear();
            _playerFirstSeen.Clear();
            _stillCount.Clear();
            _predStrikes.Clear();
            _speedStrikes.Clear();
            _jumpStrikes.Clear();
            _scaleStrikes.Clear();
            _heavyLastPos.Clear();
            _heavyLastTime.Clear();
            _rigCache = new VRRig[0];
        }
    }
}

