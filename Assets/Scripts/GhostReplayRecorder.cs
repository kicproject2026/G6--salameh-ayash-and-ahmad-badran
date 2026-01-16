using System;
using System.IO;
using UnityEngine;

public class GhostReplayRecorder : MonoBehaviour
{
    [Header("Links")]
    public RecordingManager recordingManager;

    [Header("Settings")]
    public int sampleRate = 15;

    private StreamWriter _writer;
    private float _t0;
    private float _nextSampleTime;
    private bool _active;
    private string _filePath;

    void Update()
    {
        if (!_active) return;

        float now = Time.unscaledTime;
        if (now < _nextSampleTime) return;
        _nextSampleTime = now + (1f / Mathf.Max(1, sampleRate));

        WriteSample(now);
    }

    public void BeginRecordingToCurrentSessionFolder()
    {
        if (_active) return;

        if (recordingManager == null || string.IsNullOrWhiteSpace(recordingManager.CurrentSessionFolder))
        {
            Debug.LogError("[GhostReplayRecorder] Missing RecordingManager or session folder. Start your normal recording first.");
            return;
        }

        string folder = recordingManager.CurrentSessionFolder;
        Directory.CreateDirectory(folder);

        _filePath = Path.Combine(folder, "replay.jsonl");

        _writer = new StreamWriter(_filePath, false);
        _writer.AutoFlush = true;

        _t0 = Time.unscaledTime;
        _nextSampleTime = Time.unscaledTime;
        _active = true;

        Debug.Log("[GhostReplayRecorder] Started: " + _filePath);

        _writer.WriteLine(JsonUtility.ToJson(new ReplayHeader
        {
            type = "ReplayBegin",
            time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
        }));
    }

    public void EndRecording()
    {
        if (!_active) return;

        _active = false;

        _writer.WriteLine(JsonUtility.ToJson(new ReplayFooter
        {
            type = "ReplayEnd",
            time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
        }));

        _writer.Flush();
        _writer.Close();
        _writer = null;

        Debug.Log("[GhostReplayRecorder] Saved: " + _filePath);
    }

    private void WriteSample(float now)
    {
        var trackables = FindObjectsOfType<GhostTrackable>(false);
        float t = now - _t0;

        for (int i = 0; i < trackables.Length; i++)
        {
            var tr = trackables[i];
            if (tr == null) continue;

            Color c = tr.GetBodyColor();
            string display = tr.GetDisplayName();

            // fallback (so it never records "")
            if (string.IsNullOrWhiteSpace(display))
                display = tr.gameObject.name;

            var line = new ReplayFrame
            {
                type = "Frame",
                time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                t = t,
                id = tr.GetId(),

                // NAME (replay will use this)
                who = display,

                // COLOR
                bodyColor = c,

                head = PoseTo(tr.head),
                left = PoseTo(tr.leftHand),
                right = PoseTo(tr.rightHand)
            };

            _writer.WriteLine(JsonUtility.ToJson(line));
        }
    }

    private static PoseData PoseTo(Transform t)
    {
        if (t == null) return PoseData.Empty();
        return new PoseData
        {
            px = t.position.x, py = t.position.y, pz = t.position.z,
            rx = t.rotation.x, ry = t.rotation.y, rz = t.rotation.z, rw = t.rotation.w
        };
    }

    [Serializable] private class ReplayHeader { public string type; public string time; }
    [Serializable] private class ReplayFooter { public string type; public string time; }

    [Serializable]
    private class ReplayFrame
    {
        public string type;
        public string time;
        public float t;

        public string id;

        // NAME
        public string who;

        // COLOR
        public Color bodyColor;

        public PoseData head;
        public PoseData left;
        public PoseData right;
    }

    [Serializable]
    public struct PoseData
    {
        public float px, py, pz;
        public float rx, ry, rz, rw;

        public static PoseData Empty() => new PoseData { px = 0, py = 0, pz = 0, rx = 0, ry = 0, rz = 0, rw = 1 };
    }
}