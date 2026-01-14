using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class GhostReplayPlayer : MonoBehaviour
{
    [Header("Ghost Prefab (MUST have GhostRig)")]
    public GameObject ghostPrefab;

    [Header("Playback")]
    [Tooltip("1 = normal speed, 0.5 = slow, 2 = fast")]
    public float playbackSpeed = 1f;

    public bool loop = false;

    [Header("Debug")]
    public bool logLoadedInfo = true;

    // Loaded frames (sorted by time)
    private List<ReplayFrame> _frames = new List<ReplayFrame>();

    // Ghosts by avatar id
    private Dictionary<string, GhostRig> _ghosts = new Dictionary<string, GhostRig>();

    private bool _playing = false;
    private float _playT = 0f;
    private float _maxT = 0f;

    // For faster playback (we advance through frames instead of scanning from start)
    private int _nextFrameIndex = 0;

    // -------- Public API --------

    // Call this with the v001 folder path (same folder that contains replay.jsonl)
    public void LoadFromSessionFolder(string sessionFolder)
    {
        string path = Path.Combine(sessionFolder, "replay.jsonl");
        LoadFromFile(path);
    }

    public void LoadFromFile(string replayJsonlPath)
    {
        if (!File.Exists(replayJsonlPath))
        {
            Debug.LogError("[GhostReplayPlayer] replay.jsonl not found: " + replayJsonlPath);
            return;
        }

        CleanupGhosts();
        _frames.Clear();

        foreach (string line in File.ReadLines(replayJsonlPath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            // Only parse frame lines
            if (!line.Contains("\"type\":\"Frame\"")) continue;

            try
            {
                ReplayFrame f = JsonUtility.FromJson<ReplayFrame>(line);
                if (f != null && !string.IsNullOrEmpty(f.id))
                    _frames.Add(f);
            }
            catch
            {
                // ignore bad lines
            }
        }

        _frames.Sort((a, b) => a.t.CompareTo(b.t));

        _maxT = 0f;
        if (_frames.Count > 0)
            _maxT = _frames[_frames.Count - 1].t;

        if (logLoadedInfo)
            Debug.Log($"[GhostReplayPlayer] Loaded {_frames.Count} frames, duration ~ {_maxT:0.00}s");

        ResetPlayback();
    }

    public void Play()
    {
        if (_frames.Count == 0)
        {
            Debug.LogWarning("[GhostReplayPlayer] No frames loaded. Load replay.jsonl first.");
            return;
        }

        if (ghostPrefab == null)
        {
            Debug.LogError("[GhostReplayPlayer] ghostPrefab is NULL. Assign VRAvatar_Ghost prefab.");
            return;
        }

        _playing = true;
    }

    public void Pause()
    {
        _playing = false;
    }

    public void Stop()
{
    _playing = false;
    ResetPlayback();
    CleanupGhosts();      // <-- ADD THIS LINE (this actually destroys the ghost objects)
}


    public void LoadAndPlaySessionFolder(string sessionFolder)
    {
        LoadFromSessionFolder(sessionFolder);
        Play();
    }

    // -------- Unity Loop --------

    private void Update()
    {
        if (!_playing) return;
        if (_frames.Count == 0) return;

        _playT += Time.unscaledDeltaTime * Mathf.Max(0.01f, playbackSpeed);

        if (_playT > _maxT)
        {
            if (loop)
            {
                ResetPlayback();
            }
            else
            {
                _playing = false;
                return;
            }
        }

        ApplyFramesUpToTime(_playT);
    }

    // -------- Internal --------

    private void ResetPlayback()
    {
        _playT = 0f;
        _nextFrameIndex = 0;
    }

    private void ApplyFramesUpToTime(float t)
    {
        // Move through frames in order and apply all frames <= current time
        while (_nextFrameIndex < _frames.Count && _frames[_nextFrameIndex].t <= t)
        {
            ReplayFrame f = _frames[_nextFrameIndex];

            if (!_ghosts.TryGetValue(f.id, out GhostRig rig) || rig == null)
            {
                rig = SpawnGhost(f);
                if (rig != null)
                    _ghosts[f.id] = rig;
            }

            if (rig != null)
            {
                ApplyPose(rig.head, f.head);
                ApplyPose(rig.leftHand, f.left);
                ApplyPose(rig.rightHand, f.right);
            }

            _nextFrameIndex++;
        }
    }

    private GhostRig SpawnGhost(ReplayFrame f)
    {
        GameObject obj = Instantiate(ghostPrefab);
        obj.name = "Ghost_" + (string.IsNullOrEmpty(f.who) ? f.id : f.who);

        GhostRig rig = obj.GetComponent<GhostRig>();
        if (rig == null)
        {
            Debug.LogError("[GhostReplayPlayer] ghostPrefab MUST have GhostRig component.");
            Destroy(obj);
            return null;
        }

        // Optional: if you want ghosts to start invisible until first pose is applied,
        // you can set obj.SetActive(true) later. For now keep active.
        return rig;
    }

    private void CleanupGhosts()
    {
        foreach (var kv in _ghosts)
        {
            if (kv.Value != null)
                Destroy(kv.Value.gameObject);
        }
        _ghosts.Clear();
    }

    private static void ApplyPose(Transform target, PoseData p)
    {
        if (target == null) return;

        target.position = new Vector3(p.px, p.py, p.pz);
        target.rotation = new Quaternion(p.rx, p.ry, p.rz, p.rw);
    }

    // -------- Data Models (must match recorder JSON) --------

    [Serializable]
    public class ReplayFrame
    {
        public string type;
        public string time;
        public float t;

        public string id;
        public string who;

        public PoseData head;
        public PoseData left;
        public PoseData right;
    }

    [Serializable]
    public struct PoseData
    {
        public float px, py, pz;
        public float rx, ry, rz, rw;
    }
}
