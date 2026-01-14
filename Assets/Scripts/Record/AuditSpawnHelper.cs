using System.Collections.Generic;
using UnityEngine;

public static class AuditSpawnHelper
{
    public static void LogSpawn(string who, string objectType, Transform spawnedTransform)
    {
        if (AuditLogger.Instance == null || spawnedTransform == null) return;

        Vector3 p = spawnedTransform.position;
        Quaternion r = spawnedTransform.rotation;

        AuditLogger.Instance.Log(
            "SpawnObject",
            who,
            new Dictionary<string, object>
            {
                { "objectType", objectType },
                { "px", p.x }, { "py", p.y }, { "pz", p.z },
                { "rx", r.x }, { "ry", r.y }, { "rz", r.z }, { "rw", r.w }
            }
        );
    }
}
