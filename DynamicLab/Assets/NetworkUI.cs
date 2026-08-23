using Unity.Netcode;
using Unity.Netcode.Transports.UTP; 
using UnityEngine;
using UnityEngine.SceneManagement; 

public class NetworkUI : MonoBehaviour
{
    private static NetworkUI instance;

    private string ipAddress = "192.168.1."; 
    private bool showSettings = false;
    private bool showTutorial = false;
    private Vector2 tutorialScrollPosition = Vector2.zero;

    private bool randomMap;
    private int mapTypeIndex;
    private int mapSize;
    private string[] mapTypeNames = { "Random Scatter", "Maze (ARA*)", "Caverns (LPA*)", "Arena (D* Lite)" };

    void Awake()
    {
        // The Ghostbuster: Prevents duplicate menus from overlapping
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
        // ==========================================
        // 1. GLOBAL UI SCALING & AESTHETICS
        // ==========================================
        GUI.skin.button.fontSize = 22;
        GUI.skin.label.fontSize = 20;
        GUI.skin.textField.fontSize = 22;
        GUI.skin.horizontalSlider.fixedHeight = 25; 
        GUI.skin.horizontalSliderThumb.fixedHeight = 25; 
        GUI.skin.horizontalSliderThumb.fixedWidth = 25;

        // Dark mode background tint for the whole UI area
        GUI.contentColor = Color.white; 

        float scale = Application.isMobilePlatform ? 3.5f : 1.5f; 
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));

        // Center the menu slightly better on the screen
        GUILayout.BeginArea(new Rect(50, 40, 600, 800));
        
        if (NetworkManager.Singleton == null)
        {
            if (showTutorial) DrawTutorialMenu();
            else if (showSettings) DrawSettingsMenu();
            else DrawMainMenu();
        }
        else 
        {
            if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
            {
                if (showTutorial) DrawTutorialMenu();
                else if (showSettings) DrawSettingsMenu();
                else DrawMainMenu();
            }
            else 
            {
                // In-Game Connected Status
                GUILayout.Label("Status: Connected!", new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, normal = new GUIStyleState() { textColor = Color.green } });
                GUILayout.Label("Mode: " + (NetworkManager.Singleton.IsHost ? "Host" : "Client"));
            }
        }
        
        GUILayout.EndArea();
    }

    void DrawMainMenu()
    {
        // 1. GAME TITLE
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 42, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        titleStyle.normal.textColor = new Color(0.2f, 0.8f, 1f); // Cyan
        GUILayout.Label("DYNAMIC LABYRINTHS", titleStyle);
        GUILayout.Space(20);

        // 2. TUTORIAL SECTION (Top, separated)
        GUI.backgroundColor = new Color(1f, 0.6f, 0f); // Orange tint
        if (GUILayout.Button("How to Play / Tutorial", GUILayout.Height(55))) 
        {
            showTutorial = true;
        }
        GUI.backgroundColor = Color.white; // Reset color
        
        GUILayout.Space(15);
        DrawDivider();
        GUILayout.Space(15);

        // 3. PC HOST SECTION
        GUIStyle sectionStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
        sectionStyle.normal.textColor = Color.yellow;
        GUILayout.Label("--- PC PLAYER ---", sectionStyle);
        GUILayout.Space(10);

        GUI.backgroundColor = new Color(0.2f, 0.9f, 0.2f); // Green tint
        if (GUILayout.Button("Start Game as PC (Host)", GUILayout.Height(65))) 
        {
            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null) transport.ConnectionData.ServerListenAddress = "0.0.0.0";
            NetworkManager.Singleton.StartHost();
        }
        
        if (!Application.isMobilePlatform)
        {
            GUILayout.Space(10);
            GUI.backgroundColor = new Color(0.7f, 0.7f, 0.7f); // Gray tint
            if (GUILayout.Button("Host Settings", GUILayout.Height(45))) 
            {
                showSettings = true;
            }
        }
        GUI.backgroundColor = Color.white; // Reset color

        GUILayout.Space(15);
        DrawDivider();
        GUILayout.Space(15);
        
        // 4. MOBILE CLIENT SECTION
        GUILayout.Label("--- MOBILE PLAYER ---", sectionStyle);
        GUILayout.Space(5);
        
        GUILayout.Label("Host IP Address:");
        ipAddress = GUILayout.TextField(ipAddress, GUILayout.Height(40));
        GUILayout.Space(10);
        
        GUI.backgroundColor = new Color(0.2f, 0.5f, 1f); // Blue tint
        if (GUILayout.Button("Connect as Mobile (Client)", GUILayout.Height(65))) 
        {
            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null) transport.ConnectionData.Address = ipAddress;
            NetworkManager.Singleton.StartClient();
        }
        GUI.backgroundColor = Color.white; // Reset color
    }

    void DrawTutorialMenu()
    {
        GUILayout.Label("--- HOW TO PLAY ---", new GUIStyle() { fontSize = 32, fontStyle = FontStyle.Bold, normal = new GUIStyleState() { textColor = new Color(1f, 0.6f, 0f) } });
        GUILayout.Space(10);

        GUIStyle headerStyle = new GUIStyle() { fontSize = 24, fontStyle = FontStyle.Bold, normal = new GUIStyleState() { textColor = Color.yellow } };
        GUIStyle bodyStyle = new GUIStyle() { fontSize = 18, wordWrap = true, normal = new GUIStyleState() { textColor = Color.white } };

        tutorialScrollPosition = GUILayout.BeginScrollView(tutorialScrollPosition, GUILayout.Height(500));

        GUILayout.Label("Overview", headerStyle);
        GUILayout.Label("Dynamic Labyrinths is an asymmetric multiplayer game designed to provide a unique and enjoyable approach to visualizing and learning pathfinding algorithms.", bodyStyle);
        GUILayout.Space(15);

        GUILayout.Label("Role 1: The Hunter (PC)", headerStyle);
        GUILayout.Label("• Goal: Navigate a complex 3D maze and escape as quickly as possible.\n• Puzzle Coins: Scattered throughout the map. For every 5 coins collected, you receive 10+5 seconds of path guidance towards the escape point.\n• Controls: WASD to move, Mouse to look, Shift to run.", bodyStyle);
        GUILayout.Space(15);

        GUILayout.Label("Role 2: The Builder (Mobile)", headerStyle);
        GUILayout.Label("• Goal: Alter the map topology in real-time to trap the Hunter and guide bots toward them.\n• Controls: Drag 1 finger to pan the camera, pinch 2 fingers to zoom, and tap any open tile to place a solid wall.", bodyStyle);
        GUILayout.Space(15);

        GUILayout.Label("Hunter Bots & Hazards", headerStyle);
        GUILayout.Label("• Bots constantly hunt down the player using dynamic pathfinding.\n• If a bot remains within proximity of the Hunter for 3 seconds, the game is over.", bodyStyle);

        GUILayout.EndScrollView();
        GUILayout.Space(15);

        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f); // Red tint for back button
        if (GUILayout.Button("Back to Menu", GUILayout.Height(60)))
        {
            showTutorial = false;
        }
        GUI.backgroundColor = Color.white;
    }

    void DrawSettingsMenu()
    {
        GUILayout.Label("--- HOST SETTINGS ---", new GUIStyle() { fontSize = 32, fontStyle = FontStyle.Bold, normal = new GUIStyleState() { textColor = Color.white } });
        GUILayout.Space(15);

        GUILayout.Label("Map Size: " + mapSize, new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, normal = new GUIStyleState() { textColor = Color.yellow }});
        GUILayout.Space(5);
        mapSize = (int)GUILayout.HorizontalSlider(mapSize, 100, 250);
        GUILayout.Space(35);

        string randomBtnText = randomMap ? "Generation: RANDOM" : "Generation: CHOOSE SPECIFIC";
        GUI.backgroundColor = randomMap ? new Color(0.2f, 0.9f, 0.2f) : new Color(1f, 0.6f, 0f);
        if (GUILayout.Button(randomBtnText, GUILayout.Height(60))) randomMap = !randomMap;
        GUI.backgroundColor = Color.white;

        if (!randomMap)
        {
            GUILayout.Space(20);
            GUILayout.Label("Specific Map Layout:");
            GUI.backgroundColor = new Color(0.5f, 0.8f, 1f); // Light blue
            if (GUILayout.Button(mapTypeNames[mapTypeIndex], GUILayout.Height(60)))
            {
                mapTypeIndex++;
                if (mapTypeIndex >= mapTypeNames.Length) mapTypeIndex = 0;
            }
            GUI.backgroundColor = Color.white;
        }

        GUILayout.Space(50);

        GUI.backgroundColor = new Color(0.2f, 0.9f, 0.2f); // Green tint
        if (GUILayout.Button("Save & Back", GUILayout.Height(65)))
        {
            PlayerPrefs.SetInt("MapSize", mapSize);
            PlayerPrefs.SetInt("RandomMap", randomMap ? 1 : 0);
            PlayerPrefs.SetInt("MapType", mapTypeIndex);
            PlayerPrefs.Save();
            
            showSettings = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
        GUI.backgroundColor = Color.white;
    }

    // Helper method to draw a clean horizontal dividing line
    void DrawDivider()
    {
        GUIStyle dividerStyle = new GUIStyle(GUI.skin.box);
        dividerStyle.normal.background = Texture2D.whiteTexture;
        
        Color oldColor = GUI.color;
        GUI.color = new Color(1, 1, 1, 0.2f); // Make the line semi-transparent
        GUILayout.Box("", dividerStyle, GUILayout.Height(2), GUILayout.ExpandWidth(true));
        GUI.color = oldColor;
    }
}