using UnityEngine;
using Normal.Realtime;

public class SpawnToggleNetworked : MonoBehaviour
{
    [Header("Resources path (no .prefab), e.g. Organs/Heart")]
    public string prefabPath;

    [Header("Spawn point TAG in the scene")]
    public string spawnPointTag = "OrganSpawnPoint";

    private Transform spawnPoint;
    private GameObject instance;

    private void Awake() {
        var sp = GameObject.FindGameObjectWithTag(spawnPointTag);
        if (sp != null) spawnPoint = sp.transform;
    }

    public void ToggleSpawn()
    {
        if (instance == null)
        {
            if (spawnPoint == null) {
                Debug.LogError("Spawn point not found. Tag: " + spawnPointTag);
                return;
            }

            instance = Realtime.Instantiate(prefabPath, spawnPoint.position, spawnPoint.rotation);
        }
        else
        {
            Realtime.Destroy(instance);
            instance = null;
        }
    }
}
