using UnityEngine;

public class SpawnToggle : MonoBehaviour
{
    public GameObject prefab;

    [Header("Spawn Point (auto found if empty)")]
    public Transform spawnPoint;
    public string spawnPointTag = "OrganSpawnPoint";

    private GameObject instance;

    private void Awake()
    {
        if (spawnPoint == null)
        {
            var go = GameObject.FindGameObjectWithTag(spawnPointTag);
            if (go != null) spawnPoint = go.transform;
        }
    }

    public void ToggleSpawn()
    {
        if (instance == null)
        {
            if (prefab == null || spawnPoint == null)
            {
                Debug.LogError("[SpawnToggle] Missing prefab or spawnPoint");
                return;
            }

            instance = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
        }
        else
        {
            Destroy(instance);
            instance = null;
        }
    }
}
