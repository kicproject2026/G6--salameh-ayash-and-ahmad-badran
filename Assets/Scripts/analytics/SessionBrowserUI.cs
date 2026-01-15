using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class TopButtonEntry
{
    public string button;
    public int count;
}

[Serializable]
public class AnalyticsSummary
{
    public string filename;
    public string version;
    public float durationSeconds;
    public string[] participants;
    public int totalButtonClicks;
    public TopButtonEntry[] topButtons;
}

public class SessionBrowserUI : MonoBehaviour
{
    [Header("UI References")]
    public Transform sessionsContent;   // SessionsScrollView/Viewport/Content
    public Transform versionsContent;   // VersionsScrollView/Viewport/Content
    public Button listItemPrefab;       // ListItemButton prefab
    public TMP_Text outputText;         // RightPanel OutputText
    public VrVideoPlayback videoPlayback;

    [Header("3D Replay")]
    public ReplayUIHelper replayUIHelper; // <-- Assign ReplayUI object here in Inspector

    [Header("Buttons")]
    public Button openAuditButton;
    public Button openAnalyticsButton;

    [Header("Settings")]
    public string recordingsFolderName = "Recordings";
    public int maxAuditLinesToRead = 600;      // read last N lines for speed
    public int maxTimelineLinesToShow = 60;    // show last N readable events

    private string rootPath;
    private string selectedSessionFolder;   // full path
    private string selectedVersionFolder;   // full path
    public SharedWallSync sharedWall;

    void Awake()
    {
        rootPath = Path.Combine(Application.persistentDataPath, recordingsFolderName);

        if (openAuditButton != null) openAuditButton.onClick.AddListener(OpenAuditFriendly);
        if (openAnalyticsButton != null) openAnalyticsButton.onClick.AddListener(OpenAnalyticsFriendly);
    }

    void Start()
    {
        RefreshSessions();
        ShowMessage("Select a session (left).");
    }

    public void RefreshSessions()
    {
        ClearChildren(sessionsContent);
        ClearChildren(versionsContent);

        selectedSessionFolder = null;
        selectedVersionFolder = null;

        if (!Directory.Exists(rootPath))
        {
            ShowMessage("No recordings found yet.\nPath:\n" + rootPath);
            return;
        }

        var sessionDirs = Directory.GetDirectories(rootPath)
            .OrderByDescending(Directory.GetLastWriteTime)
            .ToArray();

        if (sessionDirs.Length == 0)
        {
            ShowMessage("No sessions found.\nRecord something first.");
            return;
        }

        foreach (var dir in sessionDirs)
        {
            string name = Path.GetFileName(dir);
            var btn = Instantiate(listItemPrefab, sessionsContent);
            btn.GetComponentInChildren<TMP_Text>().text = name;
            btn.onClick.AddListener(() => SelectSession(dir));
        }

        ShowMessage("Sessions loaded. Pick one.");
    }

    private void SelectSession(string sessionDir)
    {
        selectedSessionFolder = sessionDir;
        selectedVersionFolder = null;

        ClearChildren(versionsContent);

        var versionDirs = Directory.GetDirectories(sessionDir)
            .OrderBy(d => ExtractVersionNumber(Path.GetFileName(d)))
            .ToArray();

        if (versionDirs.Length == 0)
        {
            ShowMessage("No versions found in:\n" + Path.GetFileName(sessionDir));
            return;
        }

        foreach (var vdir in versionDirs)
        {
            string vname = Path.GetFileName(vdir);
            var btn = Instantiate(listItemPrefab, versionsContent);
            btn.GetComponentInChildren<TMP_Text>().text = vname;
            btn.onClick.AddListener(() => SelectVersion(vdir));
        }

        ShowMessage($"Selected session:\n{Path.GetFileName(sessionDir)}\nPick a version (middle).");
    }

    private void SelectVersion(string versionDir)
    {
        selectedVersionFolder = versionDir;
        ShowMessage($"Selected version:\n{Path.GetFileName(versionDir)}\nNow open Audit or Analytics (right).");

        if (videoPlayback != null)
            videoPlayback.SetSelectedVersionFolder(versionDir);

        // ✅ THIS IS THE IMPORTANT LINE: sets the 3D replay folder automatically
        if (replayUIHelper != null)
            replayUIHelper.SetSelectedVersionFolder(versionDir);
    }

    // -------------------------- TASK 2 (AUDIT) - Friendly --------------------------

    private void OpenAuditFriendly()
    {
        if (!EnsureVersionSelected()) return;

        string path = Path.Combine(selectedVersionFolder, "audit.jsonl");
        if (!File.Exists(path))
        {
            ShowMessage("Audit file not found:\nmissing audit.jsonl");
            return;
        }

        try
        {
            var allLines = File.ReadAllLines(path);
            var lines = allLines.Skip(Mathf.Max(0, allLines.Length - maxAuditLinesToRead)).ToArray();

            string sessionName = Path.GetFileName(selectedSessionFolder);
            string versionName = Path.GetFileName(selectedVersionFolder);

            int buttonClicks = 0;
            int participantsJoined = 0;
            string doctorName = "Unknown";
            string patientName = "Unknown";
            string encodeStatus = null;

            var timeline = new System.Collections.Generic.List<string>();

            foreach (var line in lines)
            {
                string type = ExtractJsonString(line, "type");
                string who = ExtractJsonString(line, "who");
                string time = ExtractJsonString(line, "time");
                string button = ExtractJsonString(line, "button");
                if (string.IsNullOrEmpty(button))
                    button = ExtractJsonMetaButton(line);

                if (type == "AuditBegin")
                {
                    string p = ExtractJsonString(line, "patient");
                    if (!string.IsNullOrEmpty(p)) patientName = p;
                }

                if (type == "AvatarCreatedLocal" && !string.IsNullOrEmpty(who))
                    doctorName = who;

                if (type == "AvatarCreatedRemote")
                {
                    participantsJoined++;
                    string readable = $"👤 Participant joined: {who}";
                    timeline.Add(FormatTimeline(time, readable));
                }

                if (type == "StartRecording")
                    timeline.Add(FormatTimeline(time, "▶ Recording started"));

                if (type == "StopRecording")
                    timeline.Add(FormatTimeline(time, "⏹ Recording stopped"));

                if (type == "ButtonClick")
                {
                    buttonClicks++;
                    string readable = $"🔘 Used tool: {button}";
                    timeline.Add(FormatTimeline(time, readable));
                }

                if (type == "EncodeMp4Success")
                {
                    encodeStatus = "✅ Video saved successfully";
                    timeline.Add(FormatTimeline(time, "✅ Video saved successfully"));
                }
                else if (type == "EncodeMp4Failed" || type == "EncodeMp4Exception")
                {
                    encodeStatus = "❌ Video export failed";
                    timeline.Add(FormatTimeline(time, "❌ Video export failed"));
                }
            }

            if (timeline.Count > maxTimelineLinesToShow)
                timeline = timeline.Skip(timeline.Count - maxTimelineLinesToShow).ToList();

            var sb = new StringBuilder();

            sb.AppendLine("TASK 2 — AUDIT (User Friendly View)");
            sb.AppendLine(new string('=', 36));
            sb.AppendLine($"Session: {sessionName}");
            sb.AppendLine($"Version: {versionName}");
            sb.AppendLine();

            sb.AppendLine("Summary");
            sb.AppendLine("-------");
            sb.AppendLine($"Doctor: {doctorName}");
            sb.AppendLine($"Patient (typed): {patientName}");
            sb.AppendLine($"Participants joined: {participantsJoined}");
            sb.AppendLine($"Total interactions (button clicks): {buttonClicks}");
            if (!string.IsNullOrEmpty(encodeStatus)) sb.AppendLine($"Export: {encodeStatus}");
            sb.AppendLine();

            sb.AppendLine("Timeline (what happened)");
            sb.AppendLine("------------------------");
            if (timeline.Count == 0)
                sb.AppendLine("No readable actions found yet.");
            else
                foreach (var t in timeline) sb.AppendLine(t);

            sb.AppendLine();
            sb.AppendLine("Meaning");
            sb.AppendLine("-------");
            sb.AppendLine("This log shows *who joined* and *what actions happened* during the session, in time order.");

            string finalText = sb.ToString();
            outputText.text = finalText;

            if (sharedWall != null)
            {
                string sessionFolderName = Path.GetFileName(selectedSessionFolder);
                string versionFolderName = Path.GetFileName(selectedVersionFolder);
                sharedWall.DoctorShowAudit($"Audit: {sessionFolderName}/{versionFolderName}", finalText);
            }
        }
        catch (Exception e)
        {
            ShowMessage("Error reading audit file:\n" + e.Message);
        }
    }

    // -------------------------- TASK 3 (ANALYTICS) - Friendly --------------------------

    private void OpenAnalyticsFriendly()
    {
        if (!EnsureVersionSelected()) return;

        string path = Path.Combine(selectedVersionFolder, "analytics_summary.json");
        if (!File.Exists(path))
        {
            ShowMessage("Analytics file not found:\nmissing analytics_summary.json");
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<AnalyticsSummary>(json);

            string sessionName = Path.GetFileName(selectedSessionFolder);
            string versionName = Path.GetFileName(selectedVersionFolder);

            var sb = new StringBuilder();
            sb.AppendLine("TASK 3 — PEOPLE ANALYTICS (User Friendly View)");
            sb.AppendLine(new string('=', 46));
            sb.AppendLine($"Session: {sessionName}");
            sb.AppendLine($"Version: {versionName}");
            sb.AppendLine();

            sb.AppendLine("Summary");
            sb.AppendLine("-------");
            sb.AppendLine($"Duration: {data.durationSeconds:0.0} seconds");
            sb.AppendLine($"Participants: {(data.participants != null && data.participants.Length > 0 ? string.Join(", ", data.participants) : "Unknown")}");
            sb.AppendLine($"Total button clicks: {data.totalButtonClicks}");
            sb.AppendLine();

            sb.AppendLine("Most used tools");
            sb.AppendLine("--------------");
            if (data.topButtons != null && data.topButtons.Length > 0)
            {
                foreach (var b in data.topButtons)
                    sb.AppendLine($"• {b.button} — used {b.count} times");
            }
            else
            {
                sb.AppendLine("No top tools data found.");
            }

            sb.AppendLine();
            sb.AppendLine("Insight (simple conclusion)");
            sb.AppendLine("--------------------------");

            if (data.topButtons != null && data.topButtons.Length > 0)
            {
                var top = data.topButtons.OrderByDescending(x => x.count).First();
                sb.AppendLine($"Main focus of this session was: {top.button} (used {top.count} times).");
            }
            else
            {
                sb.AppendLine("Not enough data to generate a clear insight yet.");
            }

            string finalText = sb.ToString();
            outputText.text = finalText;

            if (sharedWall != null)
            {
                string sessionFolderName = Path.GetFileName(selectedSessionFolder);
                string versionFolderName = Path.GetFileName(selectedVersionFolder);
                sharedWall.DoctorShowAnalytics($"Analytics: {sessionFolderName}/{versionFolderName}", finalText);
            }
        }
        catch (Exception e)
        {
            ShowMessage("Error reading analytics file:\n" + e.Message);
        }
    }

    // -------------------------- Helpers --------------------------

    private bool EnsureVersionSelected()
    {
        if (string.IsNullOrEmpty(selectedSessionFolder))
        {
            ShowMessage("Select a session first (left).");
            return false;
        }
        if (string.IsNullOrEmpty(selectedVersionFolder))
        {
            ShowMessage("Select a version first (middle).");
            return false;
        }
        return true;
    }

    private void ShowMessage(string msg)
    {
        if (outputText != null) outputText.text = msg;
    }

    private static void ClearChildren(Transform t)
    {
        if (t == null) return;
        for (int i = t.childCount - 1; i >= 0; i--)
            Destroy(t.GetChild(i).gameObject);
    }

    private static int ExtractVersionNumber(string name)
    {
        if (name.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            string digits = new string(name.Skip(1).TakeWhile(char.IsDigit).ToArray());
            if (int.TryParse(digits, out int v)) return v;
        }
        return int.MaxValue;
    }

    private static string ExtractJsonString(string jsonLine, string key)
    {
        if (string.IsNullOrEmpty(jsonLine)) return null;

        var m = Regex.Match(jsonLine, $"\"{Regex.Escape(key)}\"\\s*:\\s*\"(.*?)\"");
        if (m.Success) return m.Groups[1].Value;

        m = Regex.Match(jsonLine, $"\"{Regex.Escape(key)}\"\\s*:\\s*(\\d+(?:\\.\\d+)?)");
        if (m.Success) return m.Groups[1].Value;

        return null;
    }

    private static string ExtractJsonMetaButton(string jsonLine)
    {
        var m = Regex.Match(jsonLine, "\"meta\"\\s*:\\s*\\{.*?\"button\"\\s*:\\s*\"(.*?)\".*?\\}");
        if (m.Success) return m.Groups[1].Value;
        return null;
    }

    private static string FormatTimeline(string time, string message)
    {
        if (string.IsNullOrEmpty(time)) return $"• {message}";
        return $"• {time} — {message}";
    }
}