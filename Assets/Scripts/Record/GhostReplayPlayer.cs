using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using TMPro;

public class GhostReplayPlayer : MonoBehaviour
{
    [Header("Ghost Prefabs (MUST have GhostRig)")]
    public GameObject doctorGhostPrefab;
    public GameObject patientGhostPrefab;

    [Header("Organ Prefabs")]
    public GameObject heartPrefab;
    public GameObject brainPrefab;
    public GameObject liverPrefab;
    public GameObject kidneyPrefab;
    public GameObject gutsPrefab;
    public GameObject skeletonPrefab;

    [Header("Playback")]
    public float playbackSpeed = 1f;
    public bool loop = false;

    [Header("Debug")]
    public bool logLoadedInfo = true;

    private List<ReplayFrame> _frames = new List<ReplayFrame>();
    private Dictionary<string, GhostRig> _ghosts = new Dictionary<string, GhostRig>();
    private Dictionary<string, GameObject> _organs = new Dictionary<string, GameObject>();
    private Dictionary<string, string> _organTypes = new Dictionary<string, string>();

    private bool _playing = false;
    private float _playT = 0f;
    private float _maxT = 0f;
    private int _nextFrameIndex = 0;

    private HashSet<string> _spawnedOrgans = new HashSet<string>();

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
        CleanupOrgans();
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

        if (doctorGhostPrefab == null || patientGhostPrefab == null)
        {
            Debug.LogError("[GhostReplayPlayer] Ghost prefabs are NULL. Assign Doctor and Patient ghost prefabs.");
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
        CleanupOrgans();
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
                CleanupOrgans();
                _spawnedOrgans.Clear();
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
                ApplyMovement(rig, f.isWalking);
            }

            if (f.organs != null && f.organs.Length > 0)
            {
                ApplyOrgans(f.organs);
            }

            _nextFrameIndex++;
        }
    }

    private void ApplyOrgans(OrganData[] organs)
    {
        HashSet<string> currentFrameOrgans = new HashSet<string>();

        for (int i = 0; i < organs.Length; i++)
        {
            var organ = organs[i];
            currentFrameOrgans.Add(organ.id);

            if (!_spawnedOrgans.Contains(organ.id))
            {
                GameObject organObj = SpawnOrgan(organ);
                if (organObj != null)
                {
                    _organs[organ.id] = organObj;
                    _organTypes[organ.id] = organ.organType;
                    _spawnedOrgans.Add(organ.id);
                }
            }

            if (_organs.TryGetValue(organ.id, out GameObject existingOrgan) && existingOrgan != null)
            {
                ApplyOrganTransform(existingOrgan, organ);
            }
        }

        List<string> toRemove = new List<string>();
        foreach (var existingOrganId in _spawnedOrgans)
        {
            if (!currentFrameOrgans.Contains(existingOrganId))
            {
                if (_organs.TryGetValue(existingOrganId, out GameObject obj) && obj != null)
                {
                    Destroy(obj);
                }
                _organs.Remove(existingOrganId);
                _organTypes.Remove(existingOrganId);
                toRemove.Add(existingOrganId);
            }
        }

        foreach (var id in toRemove)
        {
            _spawnedOrgans.Remove(id);
        }
    }

    private GameObject SpawnOrgan(OrganData organ)
    {
        GameObject prefab = GetOrganPrefab(organ.organType);
        if (prefab == null)
        {
            Debug.LogWarning($"[GhostReplayPlayer] No prefab found for organ type: {organ.organType}");
            return null;
        }

        GameObject obj = Instantiate(prefab);
        obj.name = "Ghost_" + organ.organType;

        ApplyOrganTransform(obj, organ);

        return obj;
    }

    private void ApplyOrganTransform(GameObject obj, OrganData organ)
    {
        if (obj == null) return;

        obj.transform.position = new Vector3(organ.px, organ.py, organ.pz);
        obj.transform.rotation = new Quaternion(organ.rx, organ.ry, organ.rz, organ.rw);
        obj.transform.localScale = new Vector3(organ.sx, organ.sy, organ.sz);
    }

    private GameObject GetOrganPrefab(string organType)
    {
        string lowerType = organType.ToLower();

        if (lowerType.Contains("heart")) return heartPrefab;
        if (lowerType.Contains("brain")) return brainPrefab;
        if (lowerType.Contains("liver")) return liverPrefab;
        if (lowerType.Contains("kidney")) return kidneyPrefab;
        if (lowerType.Contains("guts")) return gutsPrefab;
        if (lowerType.Contains("skeleton")) return skeletonPrefab;

        return null;
    }

    private GhostRig SpawnGhost(ReplayFrame f)
    {
        string role = f.role;
        if (string.IsNullOrEmpty(role)) role = "Patient";

        GameObject prefabToUse = (role == "Doctor") ? doctorGhostPrefab : patientGhostPrefab;

        GameObject obj = Instantiate(prefabToUse);
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

    private void CleanupOrgans()
    {
        foreach (var kv in _organs)
        {
            if (kv.Value != null)
                Destroy(kv.Value);
        }
        _organs.Clear();
        _organTypes.Clear();
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

    private static void ApplyMovement(GhostRig rig, bool isWalking)
    {
        if (rig == null || rig.animator == null) return;
        rig.animator.SetBool("isWalking", isWalking);
    }

    [Serializable]
    public class ReplayFrame
    {
        public string type;
        public string time;
        public float t;

        public string id;
        public string who;

        public string role;

        public Color bodyColor;

        public PoseData head;
        public PoseData left;
        public PoseData right;
        public PoseData root;

        public bool isWalking;

        public OrganData[] organs;
    }

    [Serializable]
    public class OrganData
    {
        public string id;
        public string organType;
        public float px, py, pz;
        public float rx, ry, rz, rw;
        public float sx, sy, sz;
    }

    [Serializable]
    public struct PoseData
    {
        public float px, py, pz;
        public float rx, ry, rz, rw;
    }
}