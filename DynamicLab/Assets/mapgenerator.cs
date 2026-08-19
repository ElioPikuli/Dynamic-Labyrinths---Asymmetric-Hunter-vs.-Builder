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
    
    public NetworkVariable<int> networkedSeed = new NetworkVariable<int>(0);

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


    // ==========================================
    // START - FOR MAIN MENU BACKGROUND MAP
    // ==========================================
    void Start()
    {
        if (isMainMenu)
        {
            // 1. קריאת הגדרות הגודל והסוג
            mapSize = PlayerPrefs.GetInt("SavedMapSize", 100);
            if (mapSize < 30) mapSize = 100;
            
            int savedMapType = PlayerPrefs.GetInt("SavedMapType", 0);
            randomlySelectMapType = false;

            if (useRandomSeed) currentSeed = Random.Range(1000, 99999);
            Random.InitState(currentSeed);

            // 2. טיפול בבחירת ה-Random כדי שלא נקבל ביום ריק
            if (savedMapType == 0) currentMapType = (MapType)Random.Range(1, 4);
            else currentMapType = (MapType)savedMapType;

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

    // ==========================================
    // ON NETWORK SPAWN - FOR ACTUAL GAMEPLAY
    // ==========================================
    public override void OnNetworkSpawn()
    {
        if (isMainMenu) return;

        // קריאת ההגדרות למשחק האמיתי
        mapSize = PlayerPrefs.GetInt("SavedMapSize", 100);
        if (mapSize < 30) mapSize = 100;

        int savedMapType = PlayerPrefs.GetInt("SavedMapType", 0);
        randomlySelectMapType = false;

        if (IsServer)
        {
            if (useRandomSeed) networkedSeed.Value = Random.Range(1000, 99999);
            else networkedSeed.Value = currentSeed;
            Debug.Log("Server chose Map Seed: " + networkedSeed.Value);
        }
        else 
        {
            Debug.Log("Client received Map Seed: " + networkedSeed.Value);
        }

        Random.InitState(networkedSeed.Value);

        if (savedMapType == 0) currentMapType = (MapType)Random.Range(1, 4);
        else currentMapType = (MapType)savedMapType;

        switch (currentMapType)
        {
            case MapType.RandomScatter: currentBiome = randomScatterBiome; break;
            case MapType.Maze_ARA: currentBiome = mazeARABiome; break;
            case MapType.Caverns_LPA: currentBiome = cavernsLPABiome; break;
            case MapType.Arena_DLite: currentBiome = arenaDLiteBiome; break;
        }

        StartCoroutine(BuildMapRoutine());
    }

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

            if (!IsServer) 
            {
                SetupMobileCamera();
            }
        }
    }

    void GenerateMap()
    {
        // ==========================================
        // התיקון: יצירת בסיס פיזי עבה מתחת למפה (מונע נפילות)
        // ==========================================
        GameObject dynamicCollider = GameObject.CreatePrimitive(PrimitiveType.Cube);
        dynamicCollider.name = "Solid Physics Base";
        // ממקמים את הקובייה בדיוק באמצע (0,0) אבל בחצי מטר מתחת לרצפה, כדי שהיא תתפוס את כל המפה
        dynamicCollider.transform.position = new Vector3(0f, -0.5f, 0f);
        // העובי שלה הוא 1 מטר, והגודל שלה מותאם בדיוק למשתנה mapSize
        dynamicCollider.transform.localScale = new Vector3(mapSize, 1f, mapSize);
        // מכבים את הנראות כדי שנראה רק את טקסטורות הרצפה שלך
        dynamicCollider.GetComponent<MeshRenderer>().enabled = false;
        // ==========================================

        grid = new bool[mapSize, mapSize];
        decoMap = new bool[mapSize, mapSize]; 
        
        switch (currentMapType)
        {
            case MapType.RandomScatter: GenerateRandomScatter(); break;
            case MapType.Maze_ARA: GenerateMazeARA(); break;
            case MapType.Caverns_LPA: GenerateCavernsLPA(); break;
            case MapType.Arena_DLite: GenerateArenaDLite(); break;
        }

        for (int i = 0; i < mapSize; i++)
        {
            grid[i, 0] = true; grid[i, mapSize - 1] = true;
            grid[0, i] = true; grid[mapSize - 1, i] = true;
        }

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

                if (currentBiome != null && currentBiome.floorPrefabs != null && currentBiome.floorPrefabs.Length > 0)
                {
                    GameObject floorChoice = currentBiome.floorPrefabs[Random.Range(0, currentBiome.floorPrefabs.Length)];
                    Instantiate(floorChoice, pos, Quaternion.identity, transform);
                }

                if (grid[x, z])
                {
                    if (currentBiome != null && currentBiome.obstaclePrefabs != null && currentBiome.obstaclePrefabs.Length > 0)
                    {
                        GameObject obstacleChoice = currentBiome.obstaclePrefabs[Random.Range(0, currentBiome.obstaclePrefabs.Length)];
                        Instantiate(obstacleChoice, pos, Quaternion.identity, transform);
                    }
                    else Instantiate(wallPrefab, pos, Quaternion.identity, transform);
                }
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
        Vector3 newPlayerPos = new Vector3(chosenStart.x - offset + 0.5f, 1f, chosenStart.y - offset + 0.5f);
        
        // עצירת פיזיקה כדי למנוע צבירת תאוצה במעבר
        player.SetActive(false);
        player.transform.position = newPlayerPos;
        player.SetActive(true);

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
            if (!decoMap[curr.x, curr.y]) reachable.Add(curr);

            foreach (Vector2Int d in dirs)
            {
                int nx = curr.x + d.x;
                int ny = curr.y + d.y;

                if (nx > 0 && nx < mapSize - 1 && ny > 0 && ny < mapSize - 1)
                {
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

    private Vector3 dragOrigin;
    private Vector3 clickScreenPosition;
    private bool isDragging = false;
    private float dragThreshold = 10f; 

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

            float moveX = Input.GetAxis("Horizontal");
            float moveZ = Input.GetAxis("Vertical");
            if (moveX != 0 || moveZ != 0) 
            {
                cam.transform.position += new Vector3(moveX, 0, moveZ) * 30f * Time.deltaTime;
            }

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0.0f)
            {
                cam.orthographicSize -= scroll * 15f;
            }

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
            
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, 5f, (mapSize / 2f) + 5f);

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
        if (!grid[x, z] && !decoMap[x, z]) 
        {
            ExecutePlaceWallClientRpc(x, z);
        }
    }

    [Rpc(SendTo.Everyone)]
    public void ExecutePlaceWallClientRpc(int x, int z)
    {
        grid[x, z] = true;

        float offset = mapSize / 2f;
        Vector3 spawnPos = new Vector3(x - offset + 0.5f, 0f, z - offset + 0.5f);
        Instantiate(wallPrefab, spawnPos, Quaternion.identity, transform);

        PathfindingGrid pg = FindFirstObjectByType<PathfindingGrid>();
        if (pg != null) 
        {
            pg.CreateGrid(); 
        }
    }
}