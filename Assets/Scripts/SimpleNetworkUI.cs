using Unity.Netcode;
using UnityEngine;

public class SimpleNetworkUI : MonoBehaviour
{
    [SerializeField] private NetworkManager networkManager;

    private void OnGUI()
    {
        if (networkManager == null)
        {
            GUILayout.Label("NetworkManager reference missing!");
            return;
        }

        if (!networkManager.IsClient && !networkManager.IsServer)
        {
            if (GUILayout.Button("Host"))
            {
                networkManager.StartHost();
                Debug.Log("Running as HOST");
            }

            if (GUILayout.Button("Join"))
            {
                networkManager.StartClient();
                Debug.Log("Running as CLIENT");
            }
        }
    }
}
