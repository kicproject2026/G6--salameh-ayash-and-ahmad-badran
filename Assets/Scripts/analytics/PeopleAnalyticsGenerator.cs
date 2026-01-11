using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public class PeopleAnalyticsGenerator : MonoBehaviour
{
    [Tooltip("audit file name inside the version folder")]
    public string auditFileName = "audit.jsonl";

    [Tooltip("output analytics file name inside the version folder")]
    public string outputFileName = "analytics_summary.json";

    // Call this after recording finishes and audit.jsonl exists
    public void Generate(string sessionFolder)
    {
        try
        {
            string auditPath = Path.Combine(sessionFolder, auditFileName);
            if (!File.Exists(auditPath))
            {
                Debug.LogWarning("[PeopleAnalytics] audit.jsonl not found: " + auditPath);
                return;
            }

            var lines = File.ReadAllLines(auditPath);

            // Metrics
            string filename = "";
            string version = new DirectoryInfo(sessionFolder).Name;
            float maxT = 0f;

            var participants = new HashSet<string>();
            var actionsPerUser = new Dictionary<string, int>();
            var buttonClicksPerUser = new Dictionary<string, Dictionary<string, int>>();
            var totalButtonCounts = new Dictionary<string, int>();
            int totalButtonClicks = 0;

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Parse minimal fields from JSON without extra packages:
                // We'll extract "type", "who", "t", and meta fields for filename/button.
                string type = ExtractString(line, "\"type\":\"", "\"");
                string who  = ExtractString(line, "\"who\":\"", "\"");

                float t = ExtractFloat(line, "\"t\":");
                if (t > maxT) maxT = t;

                if (!string.IsNullOrWhiteSpace(who) && who != "system" && who != "UnknownUser")
                    participants.Add(who);

                // AuditBegin meta: {"filename":"..."}
                if (type == "AuditBegin")
                {
                    filename = ExtractString(line, "\"filename\":\"", "\"");
                }

                // Count actions per user
                if (!string.IsNullOrWhiteSpace(who))
                {
                    if (!actionsPerUser.ContainsKey(who))
                        actionsPerUser[who] = 0;

                    // Count meaningful events (you can tune this list)
                    if (type == "ButtonClick" || type == "StartRecording" || type == "StopRecording" ||
                        type == "EncodeMp4Success" || type == "EncodeMp4Failed" ||
                        type == "AvatarCreatedLocal" || type == "AvatarCreatedRemote")
                    {
                        actionsPerUser[who]++;
                    }
                }

                // Button clicks breakdown
                if (type == "ButtonClick")
                {
                    totalButtonClicks++;

                    string button = ExtractString(line, "\"button\":\"", "\"");
                    if (string.IsNullOrWhiteSpace(button))
                        button = "UnknownButton";

                    // global button counts
                    if (!totalButtonCounts.ContainsKey(button))
                        totalButtonCounts[button] = 0;
                    totalButtonCounts[button]++;

                    // per user
                    if (!buttonClicksPerUser.ContainsKey(who))
                        buttonClicksPerUser[who] = new Dictionary<string, int>();

                    if (!buttonClicksPerUser[who].ContainsKey(button))
                        buttonClicksPerUser[who][button] = 0;
                    buttonClicksPerUser[who][button]++;
                }
            }

            // Top buttons (sorted)
            var topButtons = new List<(string button, int count)>();
            foreach (var kv in totalButtonCounts)
                topButtons.Add((kv.Key, kv.Value));
            topButtons.Sort((a, b) => b.count.CompareTo(a.count));

            // Build JSON manually (simple + stable)
            string outPath = Path.Combine(sessionFolder, outputFileName);
            string json = BuildSummaryJson(
                filename,
                version,
                maxT,
                participants,
                totalButtonClicks,
                actionsPerUser,
                buttonClicksPerUser,
                topButtons
            );

            File.WriteAllText(outPath, json);
            Debug.Log("[PeopleAnalytics] Saved: " + outPath);
        }
        catch (Exception e)
        {
            Debug.LogError("[PeopleAnalytics] Failed: " + e.Message);
        }
    }

    // ---------------- helpers ----------------

    private static string ExtractString(string src, string start, string end)
    {
        int i = src.IndexOf(start, StringComparison.Ordinal);
        if (i < 0) return "";
        i += start.Length;

        int j = src.IndexOf(end, i, StringComparison.Ordinal);
        if (j < 0) return "";

        return Unescape(src.Substring(i, j - i));
    }

    private static float ExtractFloat(string src, string key)
    {
        int i = src.IndexOf(key, StringComparison.Ordinal);
        if (i < 0) return 0f;
        i += key.Length;

        // read number until comma or }
        int j = i;
        while (j < src.Length && (char.IsDigit(src[j]) || src[j] == '.' || src[j] == '-'))
            j++;

        var s = src.Substring(i, j - i);
        if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
            return v;
        return 0f;
    }

    private static string Unescape(string s) =>
        s.Replace("\\\"", "\"").Replace("\\\\", "\\");

    private static string Escape(string s) =>
        (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string BuildSummaryJson(
        string filename,
        string version,
        float durationSeconds,
        HashSet<string> participants,
        int totalButtonClicks,
        Dictionary<string, int> actionsPerUser,
        Dictionary<string, Dictionary<string, int>> buttonClicksPerUser,
        List<(string button, int count)> topButtons
    )
    {
        var pList = new List<string>(participants);
        pList.Sort(StringComparer.Ordinal);

        // JSON builder
        System.Text.StringBuilder sb = new System.Text.StringBuilder(2048);
        sb.Append("{\n");
        sb.Append("  \"filename\": \"").Append(Escape(filename)).Append("\",\n");
        sb.Append("  \"version\": \"").Append(Escape(version)).Append("\",\n");
        sb.Append("  \"durationSeconds\": ").Append(durationSeconds.ToString("0.###", CultureInfo.InvariantCulture)).Append(",\n");

        sb.Append("  \"participants\": [");
        for (int i = 0; i < pList.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append("\"").Append(Escape(pList[i])).Append("\"");
        }
        sb.Append("],\n");

        sb.Append("  \"totalButtonClicks\": ").Append(totalButtonClicks).Append(",\n");

        sb.Append("  \"actionsPerUser\": {\n");
        bool first = true;
        foreach (var kv in actionsPerUser)
        {
            if (!first) sb.Append(",\n");
            first = false;
            sb.Append("    \"").Append(Escape(kv.Key)).Append("\": ").Append(kv.Value);
        }
        sb.Append("\n  },\n");

        sb.Append("  \"buttonClicksPerUser\": {\n");
        bool firstUser = true;
        foreach (var userKv in buttonClicksPerUser)
        {
            if (!firstUser) sb.Append(",\n");
            firstUser = false;

            sb.Append("    \"").Append(Escape(userKv.Key)).Append("\": {");

            bool firstBtn = true;
            foreach (var btnKv in userKv.Value)
            {
                if (!firstBtn) sb.Append(", ");
                firstBtn = false;
                sb.Append("\"").Append(Escape(btnKv.Key)).Append("\": ").Append(btnKv.Value);
            }

            sb.Append("}");
        }
        sb.Append("\n  },\n");

        sb.Append("  \"topButtons\": [\n");
        int topN = Mathf.Min(10, topButtons.Count);
        for (int i = 0; i < topN; i++)
        {
            sb.Append("    { \"button\": \"").Append(Escape(topButtons[i].button)).Append("\", \"count\": ").Append(topButtons[i].count).Append(" }");
            if (i < topN - 1) sb.Append(",");
            sb.Append("\n");
        }
        sb.Append("  ]\n");

        sb.Append("}\n");
        return sb.ToString();
    }
}
