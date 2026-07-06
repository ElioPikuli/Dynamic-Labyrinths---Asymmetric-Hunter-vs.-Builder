using Unity.Netcode;
using Unity.Netcode.Transports.UTP; 
using UnityEngine;

public class NetworkUI : MonoBehaviour
{
    private string ipAddress = "192.168.1."; 

    void OnGUI()
    {
        float scale = Application.isMobilePlatform ? 3.5f : 1.5f; 
        
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));

        GUILayout.BeginArea(new Rect(60, 60, 350, 500));
        
        // שכבת מגן: אם ה-NetworkManager חסר, אל תעשה כלום ותצא!
        if (NetworkManager.Singleton == null)
        {
            GUILayout.Label("NetworkManager is missing from the scene!", new GUIStyle() { normal = new GUIStyleState() { textColor = Color.red } });
            GUILayout.EndArea();
            return;
        }

        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            if (GUILayout.Button("PC: Start Host", GUILayout.Height(50))) 
            {
                UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                if (transport != null) transport.ConnectionData.ServerListenAddress = "0.0.0.0";
                NetworkManager.Singleton.StartHost();
            }
            
            GUILayout.Space(20);
            
            GUILayout.Label("Host IP Address:");
            ipAddress = GUILayout.TextField(ipAddress, GUILayout.Height(30));
            
            if (GUILayout.Button("Mobile: Connect to IP", GUILayout.Height(50))) 
            {
                UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                if (transport != null) transport.ConnectionData.Address = ipAddress;
                NetworkManager.Singleton.StartClient();
            }
        }
        else 
        {
            GUILayout.Label("Status: Connected!", new GUIStyle() { fontSize = 24, normal = new GUIStyleState() { textColor = Color.green } });
            GUILayout.Label("Mode: " + (NetworkManager.Singleton.IsHost ? "Host" : "Client"));
        }
        
        GUILayout.EndArea();
    }
}