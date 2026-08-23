using Unity.Netcode;
using Unity.Netcode.Transports.UTP; 
using UnityEngine;
using UnityEngine.SceneManagement; 

public class NetworkUI : MonoBehaviour
{
    private string ipAddress = "192.168.1."; 
    private bool showSettings = false;

    private bool randomMap;
    private int mapTypeIndex;
    private int mapSize;
    private string[] mapTypeNames = { "Random Scatter", "Maze (ARA*)", "Caverns (LPA*)", "Arena (D* Lite)" };
    private static NetworkUI instance;

void Awake()
    {
        // THE GHOSTBUSTER: If another NetworkUI exists from a previous scene, 
        // destroy this duplicate script instantly so they don't overlap!
        if (instance != null && instance != this)
        {
            Destroy(this); 
            return;
        }
        instance = this;
    }
    
    void Start()
    {
        randomMap = PlayerPrefs.GetInt("RandomMap", 1) == 1;
        mapTypeIndex = PlayerPrefs.GetInt("MapType", 1);
        mapSize = PlayerPrefs.GetInt("MapSize", 100);
    }

    void OnGUI()
    {
        float scale = Application.isMobilePlatform ? 3.5f : 1.5f; 
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));

        GUILayout.BeginArea(new Rect(60, 60, 350, 500));
        
        // ==========================================
        // THE FIX: SAFETY CHECK FOR THE MAIN MENU
        // ==========================================
        if (NetworkManager.Singleton == null)
        {
            // If there is no network manager, we are in the Main Menu! 
            // Just show the Host Settings for the PC.
            if (showSettings) DrawSettingsMenu();
            else if (!Application.isMobilePlatform)
            {
                if (GUILayout.Button("Host Settings", GUILayout.Height(50))) showSettings = true;
            }
        }
        else 
        {
            // If the NetworkManager exists, we are in the Game Scene! 
            if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
            {
                if (showSettings) DrawSettingsMenu();
                else DrawMainMenu();
            }
            else 
            {
                GUILayout.Label("Status: Connected!", new GUIStyle() { fontSize = 24, normal = new GUIStyleState() { textColor = Color.green } });
                GUILayout.Label("Mode: " + (NetworkManager.Singleton.IsHost ? "Host" : "Client"));
            }
        }
        
        // Because the script won't crash anymore, it will always safely reach this line!
        GUILayout.EndArea();
    }

    void DrawMainMenu()
    {
        if (GUILayout.Button("PC: Start Host", GUILayout.Height(50))) 
        {
            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null) transport.ConnectionData.ServerListenAddress = "0.0.0.0";
            NetworkManager.Singleton.StartHost();
        }
        
        GUILayout.Space(10);
        
        if (!Application.isMobilePlatform)
        {
            if (GUILayout.Button("Host Settings", GUILayout.Height(40))) showSettings = true;
        }
        
        GUILayout.Space(20);
        
        GUILayout.Label("Host IP Address (Phone Only):");
        ipAddress = GUILayout.TextField(ipAddress, GUILayout.Height(30));
        
        if (GUILayout.Button("Mobile: Connect to IP", GUILayout.Height(50))) 
        {
            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null) transport.ConnectionData.Address = ipAddress;
            NetworkManager.Singleton.StartClient();
        }
    }

    void DrawSettingsMenu()
    {
        GUILayout.Label("--- HOST SETTINGS ---", new GUIStyle() { fontSize = 20, fontStyle = FontStyle.Bold, normal = new GUIStyleState() { textColor = Color.white } });
        GUILayout.Space(10);

        GUILayout.Label("Map Size: " + mapSize);
        mapSize = (int)GUILayout.HorizontalSlider(mapSize, 100, 250);
        GUILayout.Space(20);

        string randomBtnText = randomMap ? "Generation: RANDOM" : "Generation: CHOOSE SPECIFIC";
        if (GUILayout.Button(randomBtnText, GUILayout.Height(40))) randomMap = !randomMap;

        if (!randomMap)
        {
            GUILayout.Space(10);
            GUILayout.Label("Specific Map Layout:");
            if (GUILayout.Button(mapTypeNames[mapTypeIndex], GUILayout.Height(40)))
            {
                mapTypeIndex++;
                if (mapTypeIndex >= mapTypeNames.Length) mapTypeIndex = 0;
            }
        }

        GUILayout.Space(30);

        if (GUILayout.Button("Save & Back", GUILayout.Height(50)))
        {
            PlayerPrefs.SetInt("MapSize", mapSize);
            PlayerPrefs.SetInt("RandomMap", randomMap ? 1 : 0);
            PlayerPrefs.SetInt("MapType", mapTypeIndex);
            PlayerPrefs.Save();
            
            showSettings = false;

            // Reloads whatever scene you are currently in to instantly apply the new settings
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}