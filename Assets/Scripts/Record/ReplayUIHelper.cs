using UnityEngine;

public class ReplayUIHelper : MonoBehaviour
{
    [Header("3D Replay Players")]
    public GhostReplayPlayer replayPlayer;       // avatars
    public SpawnReplayPlayer spawnReplayPlayer;  // organs/objects

    [Header("Selected version folder (v001...)")]
    [Tooltip("AUTO filled from SessionBrowserUI when you click a version. No need to type.")]
    public string sessionFolder;

    [Header("Optional")]
    public Transform ghostsRoot;

    // This is called by SessionBrowserUI when you select v001/v002...
    public void SetSelectedVersionFolder(string versionFolderPath)
    {
        sessionFolder = versionFolderPath;
        Debug.Log("[ReplayUIHelper] Selected version folder set to: " + sessionFolder);
    }

    public void PlayReplay3D()
    {
        if (string.IsNullOrWhiteSpace(sessionFolder))
        {
            Debug.LogError("[ReplayUIHelper] sessionFolder is empty. Select session (left) then version (middle).");
            return;
        }

        // Start avatar ghosts
        if (replayPlayer != null)
        {
            replayPlayer.LoadAndPlaySessionFolder(sessionFolder);
            // NOTE: Don't call replayPlayer.Play() again if LoadAndPlay already plays.
        }
        else
        {
            Debug.LogError("[ReplayUIHelper] replayPlayer (GhostReplayPlayer) is NULL");
        }

        // Start organs replay
        if (spawnReplayPlayer != null)
        {
            // Use your existing method names (from your project)
            spawnReplayPlayer.LoadFromSessionFolder(sessionFolder);
            spawnReplayPlayer.PlayReplay();
        }
        else
        {
            Debug.LogError("[ReplayUIHelper] spawnReplayPlayer (SpawnReplayPlayer) is NULL");
        }
    }

    public void StopReplay3D()
    {
        if (replayPlayer != null)
            replayPlayer.Stop();

        if (spawnReplayPlayer != null)
            spawnReplayPlayer.StopReplay();

        // Extra cleanup (if you spawn ghosts under a root)
        if (ghostsRoot != null)
        {
            for (int i = ghostsRoot.childCount - 1; i >= 0; i--)
                Destroy(ghostsRoot.GetChild(i).gameObject);
        }
    }
}