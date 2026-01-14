using UnityEngine;

public class ReplayUIHelper : MonoBehaviour
{
    [Header("3D Replay Players")]
    public GhostReplayPlayer replayPlayer;       // avatars
    public SpawnReplayPlayer spawnReplayPlayer;  // organs/objects

    [Header("Selected version folder (v001...)")]
    public string sessionFolder;

    public void PlayReplay3D()
    {
        if (string.IsNullOrWhiteSpace(sessionFolder))
        {
            Debug.LogError("[ReplayUIHelper] sessionFolder is empty");
            return;
        }

        // Start avatar ghosts
        if (replayPlayer != null)
        {
            replayPlayer.LoadAndPlaySessionFolder(sessionFolder);
            replayPlayer.Play();
        }
        else
        {
            Debug.LogError("[ReplayUIHelper] replayPlayer (GhostReplayPlayer) is NULL");
        }

        // Start organs replay
        if (spawnReplayPlayer != null)
        {
            spawnReplayPlayer.LoadFromSessionFolder(sessionFolder);
            spawnReplayPlayer.PlayReplay();
        }
        else
        {
            Debug.LogError("[ReplayUIHelper] spawnReplayPlayer (SpawnReplayPlayer) is NULL");
        }
    }

    public Transform ghostsRoot;

public void StopReplay3D()
{
    if (replayPlayer != null)
        replayPlayer.Stop();

    if (spawnReplayPlayer != null)
        spawnReplayPlayer.StopReplay();

    if (ghostsRoot != null)
    {
        for (int i = ghostsRoot.childCount - 1; i >= 0; i--)
            Destroy(ghostsRoot.GetChild(i).gameObject);
    }
}

}
