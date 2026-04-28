using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Normal.Realtime;

public class SpawnReplayPlayer : MonoBehaviour
{
    [Header("Optional: parent for replay-spawned objects")]
    public Transform spawnedRoot;

    private SpawnEvent[] _events = Array.Empty<SpawnEvent>();
    private int _nextIndex = 0;
    private float _t = 0f;
    private bool _playing = false;

    private readonly List<GameObject> _spawned = new List<GameObject>();

    public void LoadFromSessionFolder(string sessionFolder)
    {
        LoadFromFile(Path.Combine(sessionFolder, "audit.jsonl"));
    }

    public void LoadFromFile(string auditJsonlPath)
    {
        if (!File.Exists(auditJsonlPath))
        {
            Debug.LogError("[SpawnReplayPlayer] audit.jsonl not found: " + auditJsonlPath);
            _events = Array.Empty<SpawnEvent>();
            return;
        }

        var lines = File.ReadAllLines(auditJsonlPath);
        var temp = new List<SpawnEvent>();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!line.Contains("\"type\":\"SpawnObject\"")) continue;

            try
            {
                var e = JsonUtility.FromJson<SpawnEvent>(line);
                if (e != null && e.meta != null && !string.IsNullOrWhiteSpace(e.meta.prefabPath))
                    temp.Add(e);
            }
            catch { }
        }

        temp.Sort((a, b) => a.t.CompareTo(b.t));
        _events = temp.ToArray();

        Debug.Log($"[SpawnReplayPlayer] Loaded {_events.Length} SpawnObject events");
        StopReplay();
    }

    public void PlayReplay()
    {
        _playing = true;
    }

    public void StopReplay()
    {
        _playing = false;
        _t = 0f;
        _nextIndex = 0;

        for (int i = 0; i < _spawned.Count; i++)
            if (_spawned[i] != null) Destroy(_spawned[i]);

        _spawned.Clear();
    }

    private void Update()
    {
        if (!_playing || _events.Length == 0) return;

        _t += Time.unscaledDeltaTime;

        while (_nextIndex < _events.Length && _events[_nextIndex].t <= _t)
        {
            //SpawnNow(_events[_nextIndex]);
            _nextIndex++;
        }
    }

    private void SpawnNow(SpawnEvent e)
    {

        var prefab = Resources.Load<GameObject>(e.meta.prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("[SpawnReplayPlayer] Resources.Load failed for: " + e.meta.prefabPath);
            return;
        }

        Vector3 pos = new Vector3(e.meta.px, e.meta.py, e.meta.pz);
        Quaternion rot = new Quaternion(e.meta.rx, e.meta.ry, e.meta.rz, e.meta.rw);

        var go = Instantiate(prefab, pos, rot);
        go.name = "Replay_" + (string.IsNullOrWhiteSpace(e.meta.objectType) ? e.meta.prefabPath : e.meta.objectType);

        StripNormcore(go);

        if (spawnedRoot != null)
            go.transform.SetParent(spawnedRoot, true);

        _spawned.Add(go);
    }

    // Remove Normcore components in safe order:
    // RealtimeTransform depends on RealtimeView.
    private void StripNormcore(GameObject root)
    {
        if (root == null) return;

        // 1) Remove RealtimeTransform first
        var realtimeTransforms = root.GetComponentsInChildren<RealtimeTransform>(true);
        for (int i = 0; i < realtimeTransforms.Length; i++)
            if (realtimeTransforms[i] != null) Destroy(realtimeTransforms[i]);

        // 2) Remove any other RealtimeComponent
        var realtimeComponents = root.GetComponentsInChildren<RealtimeComponent>(true);
        for (int i = 0; i < realtimeComponents.Length; i++)
            if (realtimeComponents[i] != null) Destroy(realtimeComponents[i]);

        // 3) Now remove RealtimeView
        var realtimeViews = root.GetComponentsInChildren<RealtimeView>(true);
        for (int i = 0; i < realtimeViews.Length; i++)
            if (realtimeViews[i] != null) Destroy(realtimeViews[i]);
    }

    [Serializable]
    private class SpawnEvent
    {
        public string time;
        public float t;
        public string type;
        public string who;
        public SpawnMeta meta;
    }

    [Serializable]
    private class SpawnMeta
    {
        public string objectType;
        public string prefabPath;
        public float px, py, pz;
        public float rx, ry, rz, rw;
    }
}