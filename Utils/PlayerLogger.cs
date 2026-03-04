using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Photon.Pun;
using Checker;

/// <summary>
/// Logs player sightings to a newline-delimited JSON file so the
/// GorillaTracker Discord bot (playertracker.py) can read them.
///
/// Log location: BepInEx\plugins\GorillaInfoLog.ndjson
/// Each line is one self-contained JSON object.
/// </summary>
public static class PlayerLogger
{
    // -- Path ---------------------------------------------------------------
    private static readonly string LogPath = Path.Combine(
        Path.GetDirectoryName(
            System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".",
        "GorillaInfoLog.ndjson"
    );

    // Deduplicate: don't re-log the same userId+room until the room changes.
    private static readonly Dictionary<string, string> _lastRoom
        = new Dictionary<string, string>(StringComparer.Ordinal);

    private static readonly object _fileLock = new object();

    // Max lines to keep (oldest pruned when exceeded)
    private const int MaxLines = 2000;

    // -----------------------------------------------------------------------

    /// <summary>
    /// Call this whenever you encounter a player in the current room.
    /// Duplicate userId+room pairs are silently skipped.
    /// </summary>
    public static void LogSighting(VRRig rig, Utilities utilities = null)
    {
        if (rig == null) return;

        var netPlayer = rig.OwningNetPlayer;
        if (netPlayer == null) return;

        string userId = netPlayer.UserId ?? "";
        if (string.IsNullOrEmpty(userId)) return;

        string room = PhotonNetwork.CurrentRoom?.Name ?? "UNKNOWN";

        // Skip if we already logged this player for this room
        lock (_lastRoom)
        {
            if (_lastRoom.TryGetValue(userId, out string prev) && prev == room)
                return;
            _lastRoom[userId] = room;
        }

        string name     = EscapeJson(netPlayer.NickName ?? "Unknown");
        string platform = EscapeJson(rig.GetPlatform().ParsePlatform());
        string ts       = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

        // Build mods JSON array
        List<string> mods = utilities?.DetectAllMods(rig) ?? new List<string>();
        var modArr = new StringBuilder("[");
        for (int i = 0; i < mods.Count; i++)
        {
            if (i > 0) modArr.Append(',');
            modArr.Append('"').Append(EscapeJson(mods[i])).Append('"');
        }
        modArr.Append(']');

        // Build one JSON line
        string line = string.Format(
            "{{\"userId\":\"{0}\",\"name\":\"{1}\",\"room\":\"{2}\",\"platform\":\"{3}\",\"timestamp\":\"{4}\",\"mods\":{5}}}",
            EscapeJson(userId), name, EscapeJson(room), platform, ts, modArr
        );

        lock (_fileLock)
        {
            try
            {
                // Read existing lines, cap size, append new one
                var lines = new List<string>();
                if (File.Exists(LogPath))
                {
                    string[] existing = File.ReadAllLines(LogPath);
                    int start = existing.Length > MaxLines - 1
                        ? existing.Length - (MaxLines - 1)
                        : 0;
                    for (int i = start; i < existing.Length; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(existing[i]))
                            lines.Add(existing[i]);
                    }
                }
                lines.Add(line);
                File.WriteAllLines(LogPath, lines, Encoding.UTF8);
            }
            catch
            {
                // Silently swallow write errors – never crash the game
            }
        }
    }

    /// <summary>
    /// Call this when leaving a room so the dedup cache is cleared,
    /// allowing re-logging when the player is seen in a new room.
    /// </summary>
    public static void ClearRoomCache()
    {
        lock (_lastRoom)
            _lastRoom.Clear();
    }

    // -----------------------------------------------------------------------
    private static string EscapeJson(string s)
    {
        if (s == null) return "";
        return s.Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
    }
}
