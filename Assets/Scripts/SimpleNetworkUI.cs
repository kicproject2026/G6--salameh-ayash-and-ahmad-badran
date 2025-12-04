using Unity.Netcode;
using UnityEngine;

public class SimpleNetworkUI : MonoBehaviour
{
    void OnGUI()
    {
        const int width = 200;
        const int height = 40;
        const int padding = 10;

        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            if (GUI.Button(new Rect(padding, padding, width, height), "Host"))
            {
                NetworkManager.Singleton.StartHost();
            }

            if (GUI.Button(new Rect(padding, padding * 2 + height, width, height), "Join"))
            {
                NetworkManager.Singleton.StartClient();
            }
        }
        else
        {
            string mode = NetworkManager.Singleton.IsServer ? "Host" : "Client";
            GUI.Label(new Rect(padding, padding, 300, height), "Running as: " + mode);
        }
    }
}
