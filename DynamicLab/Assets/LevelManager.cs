using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics; // הוספנו כדי לאפשר את השימוש בשעון העצר (Stopwatch)
using Unity.Netcode;

public class LevelManager : NetworkBehaviour
{
    public static LevelManager instance;

    [Header("Cameras")]
    public Camera mainCamera; 

    [Header("In-Game UI")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI algorithmHintText; 
    public GameObject pauseMenuWindow; 

    [Header("End Game UI")]
    public GameObject levelEndWindow; 
    public TextMeshProUGUI runStatsText; 

    [Header("Pathfinding Visuals")]
    public GameObject pathNodePrefab; 
    public int pathHintLength = 15;
    public int puzzlesRequiredForHint = 5; 
    private int puzzlesTowardsNextHint = 0; 
    private List<GameObject> activeBreadcrumbs = new List<GameObject>(); 

    [Header("Player & Destination")]
    public GameObject player;      
    public GameObject destination; 

    [Header("Seed Settings")]
    public static int lastUsedSeed;
    public static bool shouldReplaySameSeed = false;

    private int collectedPuzzles = 0;
    private bool isPaused = false; 
    private string usedAlgorithm = "None"; 
    private bool isHintSystemActive = false; 

    // --- משתנים חדשים למדידת זמנים ---
    private float playerStartTime;
    private double algorithmComputationTimeMs = 0;

    void Awake()
    {
        if (instance == null) instance = this;

        MapGenerator generator = FindFirstObjectByType<MapGenerator>();
        if (generator != null)
        {
            if (shouldReplaySameSeed)
            {
                generator.useRandomSeed = false;
                generator.currentSeed = lastUsedSeed;
            }
            else
            {
                generator.useRandomSeed = true;
            }
        }
    }

    void Start()
    {
        collectedPuzzles = 0;
        puzzlesTowardsNextHint = 0;
        isHintSystemActive = false;
        
        // התחלת שעון העצר של השחקן ברגע שהשלב מתחיל
        playerStartTime = Time.time; 

        UpdateScoreUI();
        
        if (levelEndWindow != null) levelEndWindow.SetActive(false);
        if (pauseMenuWindow != null) pauseMenuWindow.SetActive(false);
        if (algorithmHintText != null) algorithmHintText.text = ""; 

        ResumeGame(); 
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (levelEndWindow.activeSelf) return;

            if (isPaused) ResumeGame();
            else PauseGame();
        }
    }

    public void PauseGame()
    {
        isPaused = true;
        if (pauseMenuWindow != null) pauseMenuWindow.SetActive(true);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ResumeGame()
    {
        isPaused = false;
        if (pauseMenuWindow != null) pauseMenuWindow.SetActive(false);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked; // עדיף לנעול את העכבר בחזרה כשחוזרים לשחק
        Cursor.visible = false; 
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;

        if (NetworkManager.Singleton != null)
        {
            // THE FIX: Memorize the physical object BEFORE shutting down the network!
            GameObject networkManagerObj = NetworkManager.Singleton.gameObject;
            
            NetworkManager.Singleton.Shutdown();
            Destroy(networkManagerObj); // Safely destroy the memorized object
        }

        SceneManager.LoadScene(0); 
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            MapGenerator generator = FindFirstObjectByType<MapGenerator>();
            if (generator != null) lastUsedSeed = generator.currentSeed;
            EndLevel();
        }
    }

    public void CollectPuzzle()
    {
        collectedPuzzles++;
        puzzlesTowardsNextHint++;
        UpdateScoreUI();

        if (puzzlesTowardsNextHint >= puzzlesRequiredForHint)
        {
            if (!isHintSystemActive)
            {
                puzzlesTowardsNextHint -= puzzlesRequiredForHint; 
                StartCoroutine(HintSequence());
            }
        }
    }

    void UpdateScoreUI()
    {
        if (scoreText != null) scoreText.text = "Puzzles: " + collectedPuzzles;
    }

    // ==========================================
    // --- PATHFINDING LOGIC ---
    // ==========================================
    List<Node> CalculateAlgorithmPath(Vector3 startPos, Vector3 endPos)
    {
        MapGenerator generator = FindFirstObjectByType<MapGenerator>();
        if (generator == null) return null;

        List<Node> path = null;
        Stopwatch sw = new Stopwatch(); // הפעלת שעון העצר של המעבד

        switch (generator.currentMapType)
        {
            case MapGenerator.MapType.Maze_ARA:
                usedAlgorithm = "ARA*";
                ARAStarPathfinder ara = FindFirstObjectByType<ARAStarPathfinder>();
                if (ara != null)
                {
                    sw.Start();
                    path = ara.FindPath(startPos, endPos);
                    sw.Stop();
                }
                break;
                
            case MapGenerator.MapType.Caverns_LPA:
                usedAlgorithm = "LPA*";
                LPAStarPathfinder lpa = FindFirstObjectByType<LPAStarPathfinder>();
                if (lpa != null)
                {
                    sw.Start();
                    path = lpa.FindPath(startPos, endPos);
                    sw.Stop();
                }
                break;

            case MapGenerator.MapType.Arena_DLite:
            case MapGenerator.MapType.RandomScatter:
                usedAlgorithm = "D* Lite";
                DStarLitePathfinder dlite = FindFirstObjectByType<DStarLitePathfinder>();
                if (dlite != null)
                {
                    sw.Start();
                    path = dlite.FindPath(startPos, endPos);
                    sw.Stop();
                }
                break;
        }

        // שמירת זמן הריצה שנמדד
        if (path != null)
        {
            algorithmComputationTimeMs = sw.Elapsed.TotalMilliseconds;
        }

        return path;
    }

    string GetAlgorithmExplanation()
    {
        if (usedAlgorithm == "ARA*") return "<b>ARA* Active:</b> Prioritizing survival! Fast, sub-optimal path generated.";
        if (usedAlgorithm == "LPA*") return "<b>LPA* Active:</b> Monitoring environment! Reusing previous calculations.";
        return "<b>D* Lite Active:</b> Planning backwards! Patching route dynamically.";
    }

    // ==========================================
    // --- DYNAMIC HINT PULSE SEQUENCE ---
    // ==========================================
    IEnumerator HintSequence()
    {
        isHintSystemActive = true;
        
        List<Node> path1 = CalculateAlgorithmPath(player.transform.position, destination.transform.position);
        
        if (path1 != null && path1.Count > 0)
        {
            int nodesToDraw = Mathf.Min(path1.Count, pathHintLength);
            DrawBreadcrumbs(path1.GetRange(0, nodesToDraw));
        }
        
        if (algorithmHintText != null) algorithmHintText.text = GetAlgorithmExplanation();
        
        yield return new WaitForSeconds(10f);

        ClearBreadcrumbs();
        if (algorithmHintText != null) algorithmHintText.text = "<b>Recalculating...</b>"; 
        
        yield return new WaitForSeconds(5f);

        List<Node> path2 = CalculateAlgorithmPath(player.transform.position, destination.transform.position);
        
        if (path2 != null && path2.Count > 0)
        {
            int nodesToDraw = Mathf.Min(path2.Count, pathHintLength);
            DrawBreadcrumbs(path2.GetRange(0, nodesToDraw));
        }

        if (algorithmHintText != null) algorithmHintText.text = GetAlgorithmExplanation() + " <color=yellow>(Final Flash)</color>";

        yield return new WaitForSeconds(5f);

        ClearBreadcrumbs();
        if (algorithmHintText != null) algorithmHintText.text = ""; 
        isHintSystemActive = false; 
    }

    void DrawBreadcrumbs(List<Node> path)
    {
        ClearBreadcrumbs(); 

        if (path == null || pathNodePrefab == null) return;
        
        for (int i = 1; i < path.Count; i++)
        {
            Vector3 spawnPos = path[i].worldPosition + Vector3.up * 1f; 
            GameObject crumb = Instantiate(pathNodePrefab, spawnPos, Quaternion.identity);
            activeBreadcrumbs.Add(crumb);
        }
    }

    void ClearBreadcrumbs()
    {
        foreach (GameObject crumb in activeBreadcrumbs)
        {
            if (crumb != null) Destroy(crumb);
        }
        activeBreadcrumbs.Clear();
    }

    // ==========================================
    // --- NETWORKED END LEVEL ---
    // ==========================================
    void EndLevel()
    {
        isHintSystemActive = false; 
        ClearBreadcrumbs(); 
        if (algorithmHintText != null) algorithmHintText.text = ""; 

        float playerFinishTime = Time.time - playerStartTime;
        int minutes = Mathf.FloorToInt(playerFinishTime / 60f);
        int seconds = Mathf.FloorToInt(playerFinishTime % 60);
        string formattedTime = string.Format("{0:00}:{1:00}", minutes, seconds);

        CalculateAlgorithmPath(player.transform.position, destination.transform.position);

        if (levelEndWindow != null) levelEndWindow.SetActive(true);
        
        if (runStatsText != null)
        {
            runStatsText.text = $"Labyrinth Conquered!\n\n" +
                                $"Puzzles Collected: {collectedPuzzles}\n" +
                                $"Optimal Algorithm: {usedAlgorithm}\n" +
                                $"Your Run Time: {formattedTime}\n" +
                                $"Algorithm Run Time: {algorithmComputationTimeMs:F2} ms";
        }

        // THE FIX: Tell the mobile phone that the game is over!
        if (IsServer) ShowEndScreenClientRpc();

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    [ClientRpc]
    public void ShowEndScreenClientRpc()
    {
        // This forces the phone to show its own End Level screen
        if (!IsServer && levelEndWindow != null) 
        {
            levelEndWindow.SetActive(true);
        }
    }

    public void ReplayLevel()
    {
        shouldReplaySameSeed = true;
        Time.timeScale = 1f;
        
        // THE FIX: Use the NetworkManager to load the scene so the phone stays connected!
        if (IsServer) NetworkManager.Singleton.SceneManager.LoadScene(SceneManager.GetActiveScene().name, LoadSceneMode.Single);
    }

    public void NextMap()
    {
        shouldReplaySameSeed = false;
        Time.timeScale = 1f;
        
        if (IsServer) NetworkManager.Singleton.SceneManager.LoadScene(SceneManager.GetActiveScene().name, LoadSceneMode.Single);
    }
}