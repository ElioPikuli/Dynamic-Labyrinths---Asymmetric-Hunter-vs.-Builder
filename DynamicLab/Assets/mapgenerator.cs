using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine.EventSystems;

public partial class MapGenerator : NetworkBehaviour
{
    public enum MapType { RandomScatter, Maze_ARA, Caverns_LPA, Arena_DLite }

    [Header("Mode Settings")]
    public bool isMainMenu = false; 

    [Header("Generation Style")]
    public bool randomlySelectMapType = true; 
    public MapType currentMapType = MapType.Maze_ARA;

    [Header("Biome Settings")]
    public BiomeData randomScatterBiome;
    public BiomeData mazeARABiome;
    public BiomeData cavernsLPABiome;
    public BiomeData arenaDLiteBiome;
    private BiomeData currentBiome;

    [Header("Basic Settings")]
    public GameObject wallPrefab; 
    public int mapSize = 100;
    [Range(0, 100)] public int obstacleDensity = 10;

    [Header("Start and End Settings")]
    public GameObject player;
    public GameObject destination;
    public float minDistance = 20f;

    [Header("Seed System")]
    public int currentSeed;
    public bool useRandomSeed = true;
    
    // 3. ADD THIS: The magic variable that syncs over Wi-Fi!
    public NetworkVariable<int> networkedSeed = new NetworkVariable<int>(0);
    public NetworkVariable<int> networkedMapSize = new NetworkVariable<int>(100);
    public NetworkVariable<bool> networkedRandomMode = new NetworkVariable<bool>(true);
    public NetworkVariable<int> networkedMapType = new NetworkVariable<int>(1);

    [Header("Puzzle Settings")]
    public GameObject puzzlePrefab;
    public int puzzleCount = 200;

    [Header("Bot Settings")]
    public GameObject botPrefab;
    public int numberOfBots = 15;
    private List<GameObject> spawnedBots = new List<GameObject>();

    private bool[,] grid;
    private bool[,] decoMap; 
    
    [HideInInspector] public Vector2Int startGridPos;
    [HideInInspector] public Vector2Int endGridPos;
    private List<Vector2Int> validSpawnTiles = new List<Vector2Int>(); 

    public bool[,] GetGrid() { return grid; }

    void Awake()
    {
        // Pull the Host's custom settings from memory so the local variables match the UI
        if (PlayerPrefs.HasKey("MapSize")) mapSize = PlayerPrefs.GetInt("MapSize");
        if (PlayerPrefs.HasKey("RandomMap")) randomlySelectMapType = PlayerPrefs.GetInt("RandomMap") == 1;
        if (PlayerPrefs.HasKey("MapType")) currentMapType = (MapType)PlayerPrefs.GetInt("MapType");
    }
    // ==========================================
    // THE FIX: MAIN MENU BYPASS
    // ==========================================
    void Start()
    {
        // If this is the Main Menu, we don't care about networking yet!
        // Just instantly generate a map for the background camera.
        if (isMainMenu)
        {
            if (useRandomSeed) currentSeed = Random.Range(1000, 99999);
            Random.InitState(currentSeed);

            if (randomlySelectMapType) currentMapType = (MapType)Random.Range(1, 4);

            switch (currentMapType)
            {
                case MapType.RandomScatter: currentBiome = randomScatterBiome; break;
                case MapType.Maze_ARA: currentBiome = mazeARABiome; break;
                case MapType.Caverns_LPA: currentBiome = cavernsLPABiome; break;
                case MapType.Arena_DLite: currentBiome = arenaDLiteBiome; break;
            }

            StartCoroutine(BuildMapRoutine());
        }
    }
    // 4. REPLACE 'IEnumerator Start()' with 'OnNetworkSpawn()'
    // This ensures the map waits to generate until the network is officially connected.
    public override void OnNetworkSpawn()
    {
        // If this is the Main Menu, abort! The Start() method already built the map.
        if (isMainMenu) return;
        
        // SERVER ACTIONS (PC Host)
        if (IsServer)
        {
            if (useRandomSeed) networkedSeed.Value = Random.Range(1000, 99999);
            else networkedSeed.Value = currentSeed;
            
            // The PC injects its custom UI settings into the network stream
            networkedMapSize.Value = mapSize;
            networkedRandomMode.Value = randomlySelectMapType;
            networkedMapType.Value = (int)currentMapType;
            
            Debug.Log("Server chose Map Seed: " + networkedSeed.Value);
        }
        else // CLIENT ACTIONS (Phone)
        {
            Debug.Log("Client received Map Seed: " + networkedSeed.Value);
        }

        // =====================================
        // THE FIX: ABSOLUTE SYNCHRONIZATION
        // Both devices now force their local variables to perfectly match the network stream
        // =====================================
        mapSize = networkedMapSize.Value;
        randomlySelectMapType = networkedRandomMode.Value;
        currentMapType = (MapType)networkedMapType.Value;

        // Both devices now initialize their random math using the exact same synced seed
        Random.InitState(networkedSeed.Value);

        if (randomlySelectMapType) currentMapType = (MapType)Random.Range(1, 4);

        switch (currentMapType)
        {
            case MapType.RandomScatter: currentBiome = randomScatterBiome; break;
            case MapType.Maze_ARA: currentBiome = mazeARABiome; break;
            case MapType.Caverns_LPA: currentBiome = cavernsLPABiome; break;
            case MapType.Arena_DLite: currentBiome = arenaDLiteBiome; break;
        }

        // Start the generation process
        StartCoroutine(BuildMapRoutine());
    }

    // 5. This is just the second half of your old Start() method, renamed.
    IEnumerator BuildMapRoutine()
    {
        GenerateMap();
        ApplyAtmosphere(); 

        if (!isMainMenu)
        {
            yield return new WaitForFixedUpdate();

            SetStartAndEnd();
            SpawnPuzzles();

            PathfindingGrid pg = FindFirstObjectByType<PathfindingGrid>();
            if (pg != null)
            {
                pg.CreateGrid();
                SpawnBots(pg);
            }

            // If we are the Mobile Client, switch into 2D God-Mode!
            if (!IsServer) 
            {
                SetupMobileCamera();
            }
        }
    }

    void GenerateMap()
    {
        grid = new bool[mapSize, mapSize];
        decoMap = new bool[mapSize, mapSize]; 
        
        switch (currentMapType)
        {
            case MapType.RandomScatter: GenerateRandomScatter(); break;
            case MapType.Maze_ARA: GenerateMazeARA(); break;
            case MapType.Caverns_LPA: GenerateCavernsLPA(); break;
            case MapType.Arena_DLite: GenerateArenaDLite(); break;
        }

        // Borders
        for (int i = 0; i < mapSize; i++)
        {
            grid[i, 0] = true; grid[i, mapSize - 1] = true;
            grid[0, i] = true; grid[mapSize - 1, i] = true;
        }

        // Plan Decorations
        if (currentBiome != null && currentBiome.decorationPrefabs != null && currentBiome.decorationPrefabs.Length > 0)
        {
            for (int x = 1; x < mapSize - 1; x++)
            {
                for (int z = 1; z < mapSize - 1; z++)
                {
                    if (!grid[x, z])
                    {
                        bool isWideOpen = true;
                        for (int i = -1; i <= 1; i++)
                        {
                            for (int j = -1; j <= 1; j++)
                            {
                                if (grid[x + i, z + j]) isWideOpen = false;
                            }
                        }
                        
                        if (isWideOpen && Random.Range(0, 100) < currentBiome.decorationChance)
                        {
                            decoMap[x, z] = true;
                        }
                    }
                }
            }
        }

        if (currentBiome != null && currentBiome.weatherSystemPrefab != null)
            Instantiate(currentBiome.weatherSystemPrefab, Vector3.zero, Quaternion.identity, transform);

        float offset = mapSize / 2f;
        for (int x = 0; x < mapSize; x++)
        {
            for (int z = 0; z < mapSize; z++)
            {
                Vector3 pos = new Vector3(x - offset + 0.5f, 0f, z - offset + 0.5f);

                // --- FLOOR SPAWNING LOGIC COMPLETELY REMOVED ---

                if (grid[x, z])
                {
                    if (currentBiome != null && currentBiome.obstaclePrefabs != null && currentBiome.obstaclePrefabs.Length > 0)
                    {
                        GameObject obstacleChoice = currentBiome.obstaclePrefabs[Random.Range(0, currentBiome.obstaclePrefabs.Length)];
                        Instantiate(obstacleChoice, pos, Quaternion.identity, transform);
                    }
                    else Instantiate(wallPrefab, pos, Quaternion.identity, transform);
                }
                // Spawn Decorations on open floors
                else if (decoMap[x, z])
                {
                    GameObject decoChoice = currentBiome.decorationPrefabs[Random.Range(0, currentBiome.decorationPrefabs.Length)];
                    Instantiate(decoChoice, pos, Quaternion.identity, transform);
                }
            }
        }
    }

    void GenerateRandomScatter()
    {
        for (int x = 1; x < mapSize - 1; x++)
            for (int z = 1; z < mapSize - 1; z++)
                if (Random.Range(0, 100) < obstacleDensity) grid[x, z] = true;

        for (int x = 1; x < mapSize - 1; x++)
            for (int z = 1; z < mapSize - 1; z++)
                if (!grid[x, z] && IsTooNarrow(x, z)) grid[x, z] = true;
    }

    void GenerateMazeARA()
    {
        int miniSize = mapSize / 2;
        bool[,] miniGrid = new bool[miniSize, miniSize];

        for (int x = 0; x < miniSize; x++)
            for (int z = 0; z < miniSize; z++)
                miniGrid[x, z] = true; 

        Stack<Vector2Int> stack = new Stack<Vector2Int>();
        Vector2Int current = new Vector2Int(1, 1);
        miniGrid[current.x, current.y] = false;
        stack.Push(current);

        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        while (stack.Count > 0)
        {
            current = stack.Pop();
            List<Vector2Int> unvisitedNeighbors = new List<Vector2Int>();

            foreach (var dir in directions)
            {
                int nx = current.x + dir.x * 2;
                int nz = current.y + dir.y * 2;

                if (nx > 0 && nx < miniSize - 1 && nz > 0 && nz < miniSize - 1 && miniGrid[nx, nz])
                    unvisitedNeighbors.Add(dir);
            }

            if (unvisitedNeighbors.Count > 0)
            {
                stack.Push(current);
                Vector2Int chosenDir = unvisitedNeighbors[Random.Range(0, unvisitedNeighbors.Count)];
                miniGrid[current.x + chosenDir.x, current.y + chosenDir.y] = false; 
                miniGrid[current.x + chosenDir.x * 2, current.y + chosenDir.y * 2] = false; 
                stack.Push(new Vector2Int(current.x + chosenDir.x * 2, current.y + chosenDir.y * 2));
            }
        }

        for (int x = 0; x < miniSize; x++)
        {
            for (int z = 0; z < miniSize; z++)
            {
                bool isWall = miniGrid[x, z];
                grid[x * 2, z * 2] = isWall;
                grid[x * 2 + 1, z * 2] = isWall;
                grid[x * 2, z * 2 + 1] = isWall;
                grid[x * 2 + 1, z * 2 + 1] = isWall;
            }
        }

        int chunksToBreak = (mapSize * mapSize) / 40; 
        for (int i = 0; i < chunksToBreak; i++)
        {
            int randomX = Random.Range(1, miniSize - 1) * 2;
            int randomZ = Random.Range(1, miniSize - 1) * 2;
            grid[randomX, randomZ] = false; 
            grid[randomX + 1, randomZ] = false;
            grid[randomX, randomZ + 1] = false;
            grid[randomX + 1, randomZ + 1] = false;
        }
    }

    void GenerateCavernsLPA()
    {
        int fillPercent = 45;
        for (int x = 1; x < mapSize - 1; x++)
            for (int z = 1; z < mapSize - 1; z++)
                grid[x, z] = (Random.Range(0, 100) < fillPercent);

        for (int i = 0; i < 5; i++)
        {
            bool[,] newGrid = (bool[,])grid.Clone();
            for (int x = 1; x < mapSize - 1; x++)
            {
                for (int z = 1; z < mapSize - 1; z++)
                {
                    int neighborWallTiles = GetSurroundingWallCount(x, z);
                    if (neighborWallTiles > 4) newGrid[x, z] = true;
                    else if (neighborWallTiles < 4) newGrid[x, z] = false;
                }
            }
            grid = newGrid;
        }
    }

    void GenerateArenaDLite()
    {
        float scale = 0.1f; 
        float threshold = 0.65f; 
        float offsetX = Random.Range(0f, 10000f);
        float offsetZ = Random.Range(0f, 10000f);

        for (int x = 1; x < mapSize - 1; x++)
        {
            for (int z = 1; z < mapSize - 1; z++)
            {
                float noiseValue = Mathf.PerlinNoise((x + offsetX) * scale, (z + offsetZ) * scale);
                grid[x, z] = (noiseValue > threshold);
            }
        }
    }

    int GetSurroundingWallCount(int gridX, int gridZ)
    {
        int wallCount = 0;
        for (int neighbourX = gridX - 1; neighbourX <= gridX + 1; neighbourX++)
        {
            for (int neighbourZ = gridZ - 1; neighbourZ <= gridZ + 1; neighbourZ++)
            {
                if (neighbourX >= 0 && neighbourX < mapSize && neighbourZ >= 0 && neighbourZ < mapSize)
                {
                    if (neighbourX != gridX || neighbourZ != gridZ)
                    {
                        if (grid[neighbourX, neighbourZ]) wallCount++;
                    }
                }
                else wallCount++; 
            }
        }
        return wallCount;
    }

    bool IsTooNarrow(int x, int z)
    {
        int horizontalWalls = (grid[x - 1, z] ? 1 : 0) + (grid[x + 1, z] ? 1 : 0);
        int verticalWalls = (grid[x, z - 1] ? 1 : 0) + (grid[x, z + 1] ? 1 : 0);
        return (horizontalWalls > 1 || verticalWalls > 1);
    }

    void SetStartAndEnd()
    {
        List<Vector2Int> openTiles = new List<Vector2Int>();
        List<Vector2Int> allEmptyTiles = new List<Vector2Int>();

        for (int x = 1; x < mapSize - 1; x++) 
        {
            for (int z = 1; z < mapSize - 1; z++) 
            {
                // Must be an empty floor AND have no decorations!
                if (!grid[x, z] && !decoMap[x, z]) 
                {
                    allEmptyTiles.Add(new Vector2Int(x, z));
                    
                    bool isOpen = true;
                    for(int i = -1; i <= 1; i++) {
                        for(int j = -1; j <= 1; j++) {
                            if (grid[x+i, z+j] || decoMap[x+i, z+j]) isOpen = false; 
                        }
                    }
                    if (isOpen) openTiles.Add(new Vector2Int(x, z));
                }
            }
        }

        List<Vector2Int> startPool = openTiles.Count > 0 ? openTiles : allEmptyTiles;
        Vector2Int chosenStart = startPool[0];

        for (int i = 0; i < 50; i++) 
        {
            Vector2Int candidate = startPool[Random.Range(0, startPool.Count)];
            List<Vector2Int> reachable = GetReachableTiles(candidate);
            if (reachable.Count > (mapSize * mapSize * 0.05f)) 
            {
                chosenStart = candidate;
                validSpawnTiles = reachable;
                break;
            }
        }

        if (validSpawnTiles.Count == 0) validSpawnTiles = GetReachableTiles(startPool[Random.Range(0, startPool.Count)]);

        Vector2Int chosenEnd = chosenStart;
        List<Vector2Int> validEndPool = new List<Vector2Int>();

        foreach(Vector2Int tile in validSpawnTiles) 
        {
            if (openTiles.Contains(tile)) validEndPool.Add(tile);
        }
        if (validEndPool.Count == 0) validEndPool = validSpawnTiles;

        float bestDist = 0;
        foreach (Vector2Int tile in validEndPool) 
        {
            float d = Vector2.Distance(chosenStart, tile);
            if (d >= minDistance) 
            {
                chosenEnd = tile;
                break; 
            }
            if (d > bestDist) 
            {
                bestDist = d;
                chosenEnd = tile;
            }
        }

        float offset = mapSize / 2f;
        player.transform.position = new Vector3(chosenStart.x - offset + 0.5f, 1f, chosenStart.y - offset + 0.5f);
        destination.transform.position = new Vector3(chosenEnd.x - offset + 0.5f, 1f, chosenEnd.y - offset + 0.5f);

        startGridPos = chosenStart;
        endGridPos = chosenEnd;
    }

    List<Vector2Int> GetReachableTiles(Vector2Int startPoint)
    {
        List<Vector2Int> reachable = new List<Vector2Int>();
        bool[,] visited = new bool[mapSize, mapSize];
        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        queue.Enqueue(startPoint);
        visited[startPoint.x, startPoint.y] = true;

        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        while (queue.Count > 0)
        {
            Vector2Int curr = queue.Dequeue();
            // Only add to reachable if it doesn't have a decoration blocking a puzzle spawn
            if (!decoMap[curr.x, curr.y]) reachable.Add(curr);

            foreach (Vector2Int d in dirs)
            {
                int nx = curr.x + d.x;
                int ny = curr.y + d.y;

                if (nx > 0 && nx < mapSize - 1 && ny > 0 && ny < mapSize - 1)
                {
                    // Flood fill completely ignores decorations (meaning the algorithm can walk through them)
                    if (!grid[nx, ny] && !visited[nx, ny])
                    {
                        visited[nx, ny] = true;
                        queue.Enqueue(new Vector2Int(nx, ny));
                    }
                }
            }
        }
        return reachable;
    }

    void SpawnPuzzles()
    {
        if (validSpawnTiles == null || validSpawnTiles.Count == 0) return;

        int placedPuzzles = 0;
        int safetyNet = 0;
        float offset = mapSize / 2f;

        while (placedPuzzles < puzzleCount && safetyNet < 5000)
        {
            safetyNet++;
            
            Vector2Int pos = validSpawnTiles[Random.Range(0, validSpawnTiles.Count)];
            Vector3 spawnPos = new Vector3(pos.x - offset + 0.5f, 1f, pos.y - offset + 0.5f);

            if (Vector3.Distance(spawnPos, player.transform.position) > 2f &&
                Vector3.Distance(spawnPos, destination.transform.position) > 2f)
            {
                Instantiate(puzzlePrefab, spawnPos, Quaternion.identity, transform);
                placedPuzzles++;
            }
        }
    }

    void SpawnBots(PathfindingGrid gridScript)
    {
        if (botPrefab == null) { Debug.LogError("HunterBot Prefab is null!"); return; }

        foreach (GameObject bot in spawnedBots) { if(bot != null) Destroy(bot); }
        spawnedBots.Clear();

        Node[,] gridNodes = gridScript.GetGrid();
        if (gridNodes == null) { Debug.LogError("Grid Nodes not initialized!"); return; }

        int spawnedCount = 0;
        int attempts = 0; 

        while (spawnedCount < numberOfBots && attempts < 1000)
        {
            attempts++;
            int x = Random.Range(0, mapSize);
            int z = Random.Range(0, mapSize);

            if (x >= gridNodes.GetLength(0) || z >= gridNodes.GetLength(1)) continue;

            if (gridNodes[x, z].walkable)
            {
                Vector3 spawnPos = gridNodes[x, z].worldPosition + Vector3.up * 1f;
                GameObject newBot = Instantiate(botPrefab, spawnPos, Quaternion.identity);
                
                HunterAgent agent = newBot.GetComponent<HunterAgent>();
                if (agent != null) agent.playerTransform = player.transform;

                spawnedBots.Add(newBot);
                spawnedCount++;
            }
        }
    }

    void ApplyAtmosphere()
    {
        if (currentBiome == null) return;
        if (currentBiome.skyboxMaterial != null) RenderSettings.skybox = currentBiome.skyboxMaterial;

        RenderSettings.fog = true;
        RenderSettings.fogColor = currentBiome.fogColor;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = currentBiome.fogDensity;

        Light sun = RenderSettings.sun;
        if (sun != null) sun.color = currentBiome.sunColor;

        DynamicGI.UpdateEnvironment();
    }

    // ==========================================
    // MULTIPLAYER INTERACTION SYSTEM
    // ==========================================

    void SetupMobileCamera()
    {
        // THE FIX: Find literally every camera in the scene and turn them ALL off.
        Camera[] allCameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (Camera c in allCameras)
        {
            c.gameObject.SetActive(false);
        }

        GameObject mobileCamObj = new GameObject("MobileGodCamera");
        mobileCamObj.tag = "MainCamera"; 
        Camera mobileCam = mobileCamObj.AddComponent<Camera>();

        mobileCam.orthographic = true;
        mobileCam.orthographicSize = (mapSize / 2f) + 2f;
        
        mobileCam.transform.position = new Vector3(0f, 100f, 0f);
        mobileCam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        mobileCam.clearFlags = CameraClearFlags.SolidColor;
        mobileCam.backgroundColor = new Color(0.1f, 0.1f, 0.1f);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Add this variable right above Update() to remember where the user started dragging
    private Vector3 dragOrigin;
    private Vector3 clickScreenPosition;
    private bool isDragging = false;
    private float dragThreshold = 10f; // How many pixels the mouse must move to be considered a "drag"

    void Update()
    {
        if (!IsServer) 
        {
            if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            Camera cam = Camera.main;
            if (cam == null) return;

            // 1. WASD / ARROW KEY PANNING (PC Testing)
            float moveX = Input.GetAxis("Horizontal");
            float moveZ = Input.GetAxis("Vertical");
            if (moveX != 0 || moveZ != 0) 
            {
                cam.transform.position += new Vector3(moveX, 0, moveZ) * 30f * Time.deltaTime;
            }

            // 2. PC SCROLL WHEEL ZOOM
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0.0f)
            {
                cam.orthographicSize -= scroll * 15f;
            }

            // 3. MOBILE PINCH-TO-ZOOM
            if (Input.touchCount == 2)
            {
                Touch touchZero = Input.GetTouch(0);
                Touch touchOne = Input.GetTouch(1);

                Vector2 touchZeroPrevPos = touchZero.position - touchZero.deltaPosition;
                Vector2 touchOnePrevPos = touchOne.position - touchOne.deltaPosition;

                float prevMagnitude = (touchZeroPrevPos - touchOnePrevPos).magnitude;
                float currentMagnitude = (touchZero.position - touchOne.position).magnitude;
                float difference = currentMagnitude - prevMagnitude;

                cam.orthographicSize -= difference * 0.05f; 
            }
            
            // Clamp Zoom so they can't break the camera
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, 5f, (mapSize / 2f) + 5f);

            // 4. PANNING & BUILDING (Only runs if they aren't pinching to zoom)
            if (Input.touchCount < 2)
            {
                if (Input.GetMouseButtonDown(0)) 
                {
                    if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return; 

                    clickScreenPosition = Input.mousePosition;
                    dragOrigin = cam.ScreenToWorldPoint(Input.mousePosition);
                    isDragging = false;
                }

                if (Input.GetMouseButton(0))
                {
                    if (Vector3.Distance(clickScreenPosition, Input.mousePosition) > dragThreshold)
                    {
                        isDragging = true;
                        Vector3 difference = dragOrigin - cam.ScreenToWorldPoint(Input.mousePosition);
                        cam.transform.position += new Vector3(difference.x, 0, difference.z);
                    }
                }

                if (Input.GetMouseButtonUp(0)) 
                {
                    if (!isDragging)
                    {
                        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                        if (Physics.Raycast(ray, out RaycastHit hit))
                        {
                            float offset = mapSize / 2f;
                            int x = Mathf.FloorToInt(hit.point.x + offset);
                            int z = Mathf.FloorToInt(hit.point.z + offset);

                            if (x >= 0 && x < mapSize && z >= 0 && z < mapSize)
                            {
                                RequestPlaceWallServerRpc(x, z);
                            }
                        }
                    }
                    isDragging = false;
                }
            }

            // 5. CAMERA BOUNDARY LOCK
            // Prevents the player from dragging the map off the screen!
            float boundaryLimit = mapSize / 2f;
            Vector3 clampedPos = cam.transform.position;
            clampedPos.x = Mathf.Clamp(clampedPos.x, -boundaryLimit, boundaryLimit);
            clampedPos.z = Mathf.Clamp(clampedPos.z, -boundaryLimit, boundaryLimit);
            cam.transform.position = clampedPos;
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestPlaceWallServerRpc(int x, int z)
    {
        // The PC verifies if the move is legal
        if (!grid[x, z] && !decoMap[x, z]) 
        {
            ExecutePlaceWallClientRpc(x, z);
        }
    }

    [Rpc(SendTo.Everyone)]
    public void ExecutePlaceWallClientRpc(int x, int z)
    {
        // 1. Update the master math grid
        grid[x, z] = true;

        // 2. Determine which prefab matches the current biome!
        GameObject prefabToSpawn = wallPrefab;

        if (currentBiome != null && currentBiome.obstaclePrefabs != null && currentBiome.obstaclePrefabs.Length > 0)
        {
            // Pick a biome-accurate wall/obstacle (e.g., maze wall, castle wall, cavern rock)
            prefabToSpawn = currentBiome.obstaclePrefabs[Random.Range(0, currentBiome.obstaclePrefabs.Length)];
        }

        // 3. Spawn the biome-themed obstacle
        float offset = mapSize / 2f;
        Vector3 spawnPos = new Vector3(x - offset + 0.5f, 0f, z - offset + 0.5f);
        Instantiate(prefabToSpawn, spawnPos, Quaternion.identity, transform);

        // 4. Force the Pathfinding algorithms to recognize the new wall!
        PathfindingGrid pg = FindFirstObjectByType<PathfindingGrid>();
        if (pg != null) 
        {
            pg.CreateGrid(); 
        }
    }
}