using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using TMPro;

public class GhostReplayPlayer : MonoBehaviour
{
    [Header("Ghost Prefab (MUST have GhostRig)")]
    public GameObject ghostPrefab;

    [Header("Playback")]
    public float playbackSpeed = 1f;
    public bool loop = false;

    [Header("Debug")]
    public bool logLoadedInfo = true;

    private List<ReplayFrame> _frames = new List<ReplayFrame>();
    private Dictionary<string, GhostRig> _ghosts = new Dictionary<string, GhostRig>();

    private bool _playing = false;
    private float _playT = 0f;
    private float _maxT = 0f;
    private int _nextFrameIndex = 0;

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
            if (!line.Contains("\"type\":\"Frame\"")) continue;

            try
            {
                ReplayFrame f = JsonUtility.FromJson<ReplayFrame>(line);
                if (f != null && !string.IsNullOrEmpty(f.id))
                    _frames.Add(f);
            }
            catch { }
        }

        _frames.Sort((a, b) => a.t.CompareTo(b.t));
        _maxT = (_frames.Count > 0) ? _frames[_frames.Count - 1].t : 0f;

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

    public void Pause() => _playing = false;

    public void Stop()
    {
        _playing = false;
        ResetPlayback();
        CleanupGhosts();
    }

    public void LoadAndPlaySessionFolder(string sessionFolder)
    {
        LoadFromSessionFolder(sessionFolder);
        Play();
    }

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

    private void ResetPlayback()
    {
        _playT = 0f;
        _nextFrameIndex = 0;
    }

    private void ApplyFramesUpToTime(float t)
    {
        while (_nextFrameIndex < _frames.Count && _frames[_nextFrameIndex].t <= t)
        {
            ReplayFrame f = _frames[_nextFrameIndex];

            if (!_ghosts.TryGetValue(f.id, out GhostRig rig) || rig == null)
            {
                rig = SpawnGhost(f);
                if (rig != null)
                    _ghosts[f.id] = rig;

                ApplyColor(rig, f.bodyColor);
                ApplyName(rig, f.who);
            }
            else
            {
                ApplyColor(rig, f.bodyColor);
                ApplyName(rig, f.who);
            }

            if (rig != null)
            {
                ApplyLocalPose(rig.head, f.head);
                ApplyLocalPose(rig.leftHand, f.left);
                ApplyLocalPose(rig.rightHand, f.right);
                ApplyPose(rig.transform, f.root);
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

        if (rig.bodyRenderer == null)
            rig.bodyRenderer = obj.GetComponentInChildren<Renderer>(true);

        if (rig.nameText == null)
            rig.nameText = obj.GetComponentInChildren<TMP_Text>(true);

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

    private static void ApplyLocalPose(Transform target, PoseData p)
    {
        if (target == null) return;
        target.localPosition = new Vector3(p.px, p.py, p.pz);
        target.localRotation = new Quaternion(p.rx, p.ry, p.rz, p.rw);
    }

    private static void ApplyColor(GhostRig rig, Color c)
    {
        if (rig == null || rig.bodyRenderer == null) return;

        // if it wasn't recorded, ignore
        if (c.a == 0f && c.r == 0f && c.g == 0f && c.b == 0f)
            return;

        rig.bodyRenderer.material.color = c;
    }

    private static void ApplyName(GhostRig rig, string who)
    {
        if (rig == null || rig.nameText == null) return;
        if (string.IsNullOrWhiteSpace(who)) return;

        rig.nameText.text = who;
    }

    [Serializable]
    public class ReplayFrame
    {
        public string type;
        public string time;
        public float t;

        public string id;
        public string who;

        public Color bodyColor;

        public PoseData head;
        public PoseData left;
        public PoseData right;
        public PoseData root;
    }

    [Serializable]
    public struct PoseData
    {
        public float px, py, pz;
        public float rx, ry, rz, rw;
    }
}
