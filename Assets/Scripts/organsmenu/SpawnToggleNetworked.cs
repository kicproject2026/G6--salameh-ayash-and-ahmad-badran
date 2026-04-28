using System.Collections.Generic;
using UnityEngine;
using Normal.Realtime;

public class SpawnToggleNetworked : MonoBehaviour
{
    [Header("Resources path (no .prefab), e.g. Organs/Heart")]
    public string prefabPath;

    [Header("Audit name (what appears in audit.jsonl)")]
    public string auditObjectType = "Spawn Heart";

    [Header("Spawn point TAG in the scene")]
    public string spawnPointTag = "OrganSpawnPoint";

    private Transform spawnPoint;
    private GameObject instance;

    private void Awake()
    {
        var sp = GameObject.FindGameObjectWithTag(spawnPointTag);
        if (sp != null) spawnPoint = sp.transform;
    }

    public void ToggleSpawn()
    {
        if (instance == null)
        {
            if (spawnPoint == null)
            {
                Debug.LogError("Spawn point not found. Tag: " + spawnPointTag);
                return;
            }

            // Networked spawn
            instance = Realtime.Instantiate(prefabPath, spawnPoint.position, spawnPoint.rotation);

            // Auto-add OrganTrackable if not present
            if (instance != null && instance.GetComponent<OrganTrackable>() == null)
            {
                instance.AddComponent<OrganTrackable>();
            }

            // NEW: log spawn for replay
            if (instance != null && AuditLogger.Instance != null)
            {
                Vector3 p = instance.transform.position;
                Quaternion r = instance.transform.rotation;

                AuditLogger.Instance.Log(
                    "SpawnObject",
                    GetWho(),
                    new Dictionary<string, object>
                    {
                        { "objectType", auditObjectType },
                        { "prefabPath", prefabPath },   // ✅ IMPORTANT
                        { "px", p.x }, { "py", p.y }, { "pz", p.z },
                        { "rx", r.x }, { "ry", r.y }, { "rz", r.z }, { "rw", r.w }
                    }
                );
            }
        }
        else
        {
            Realtime.Destroy(instance);
            instance = null;
        }
    }

    private string GetWho()
    {
        if (SessionData.CurrentUser == null) return "UnknownUser";
        return (SessionData.CurrentUser.role == "Doctor")
            ? $"Dr:{SessionData.CurrentUser.username}"
            : $"Patient:{SessionData.CurrentUser.username}";
    }
}
