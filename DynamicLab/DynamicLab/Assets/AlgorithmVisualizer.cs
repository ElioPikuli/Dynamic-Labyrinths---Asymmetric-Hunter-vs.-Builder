using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AlgorithmVisualizer : MonoBehaviour
{
    [Header("References")]
    public MapGenerator mapGenerator;   
    public GameObject visPanel;        
    public RawImage displayImage;      

    [Header("Animation Settings (Realtime)")]
    public float delayBetweenSteps = 0.002f; // מהירות התפשטות גל החשיבה בלייב
    public float delayPathBuild = 0.015f;    // מהירות בניית קו הזהב הסופי

    private Texture2D mapTexture;
    private int mapSize;

    // צבעי תשתית
    private Color wallColor = Color.black;
    private Color floorColor = Color.white;
    private Color startColor = Color.green;
    private Color endColor = Color.red;
    private Color pathColor = new Color(1f, 0.75f, 0f); // זהב בוהק

    public void OnShowAlgorithmClicked()
    {
        if (mapGenerator == null) return;

        // הקפאת זמן המשחק ברקע כדי שהבוטים והפיזיקה ייעצרו
        Time.timeScale = 0f;

        visPanel.SetActive(true);
        InitializeMapTexture();
        
        StopAllCoroutines();
        StartCoroutine(AnimateSpecificAlgorithmBehavior());
    }

    void InitializeMapTexture()
    {
        bool[,] grid = mapGenerator.GetGrid();
        mapSize = grid.GetLength(0);

        mapTexture = new Texture2D(mapSize, mapSize);
        mapTexture.filterMode = FilterMode.Point; 

        for (int x = 0; x < mapSize; x++)
        {
            for (int z = 0; z < mapSize; z++)
            {
                mapTexture.SetPixel(x, z, grid[x, z] ? wallColor : floorColor);
            }
        }

        DrawLargePoint(mapGenerator.startGridPos, startColor);
        DrawLargePoint(mapGenerator.endGridPos, endColor);

        mapTexture.Apply();
        displayImage.texture = mapTexture; 
    }

    void DrawLargePoint(Vector2Int pos, Color color)
    {
        for (int x = -1; x <= 1; x++)
        {
            for (int z = -1; z <= 1; z++)
            {
                int nx = pos.x + x;
                int nz = pos.y + z;
                if (nx >= 0 && nx < mapSize && nz >= 0 && nz < mapSize)
                    mapTexture.SetPixel(nx, nz, color);
            }
        }
    }

    IEnumerator AnimateSpecificAlgorithmBehavior()
    {
        Vector2Int start = mapGenerator.startGridPos;
        Vector2Int end = mapGenerator.endGridPos;
        bool[,] grid = mapGenerator.GetGrid();
        MapGenerator.MapType currentAlg = mapGenerator.currentMapType;

        Color scanColor = new Color(0.2f, 0.6f, 1f); 
        if (currentAlg == MapGenerator.MapType.Caverns_LPA) scanColor = new Color(0.2f, 0.8f, 0.4f); 
        if (currentAlg == MapGenerator.MapType.Arena_DLite) scanColor = new Color(0.6f, 0.3f, 0.9f); 

        Vector2Int root = (currentAlg == MapGenerator.MapType.Arena_DLite) ? end : start;
        Vector2Int target = (currentAlg == MapGenerator.MapType.Arena_DLite) ? start : end;

        List<Vector2Int> openSet = new List<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        Dictionary<Vector2Int, float> gCost = new Dictionary<Vector2Int, float>(); 
        
        openSet.Add(root);
        cameFrom[root] = root;
        gCost[root] = 0;

        List<Vector2Int> directions = new List<Vector2Int> { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        if (currentAlg == MapGenerator.MapType.Caverns_LPA || currentAlg == MapGenerator.MapType.Arena_DLite)
        {
            directions.Add(new Vector2Int(1, 1)); directions.Add(new Vector2Int(1, -1));
            directions.Add(new Vector2Int(-1, 1)); directions.Add(new Vector2Int(-1, -1));
        }

        bool pathFound = false;
        int stepCount = 0;
        Vector2Int finalNodeReached = root; // THE FIX: Track the exact node we stop on!

        while (openSet.Count > 0 && stepCount < 6000)
        {
            int bestIndex = 0;
            float minTotalCost = float.MaxValue;
            for (int i = 0; i < openSet.Count; i++)
            {
                float g = gCost[openSet[i]];
                float h = Vector2Int.Distance(openSet[i], target);
                
                float f = g + h;
                if (currentAlg == MapGenerator.MapType.Maze_ARA) f = g + (h * 1.5f); 
                if (currentAlg == MapGenerator.MapType.Caverns_LPA) f = g + (h * 0.4f); 

                if (f < minTotalCost)
                {
                    minTotalCost = f;
                    bestIndex = i;
                }
            }

            Vector2Int current = openSet[bestIndex];
            openSet.RemoveAt(bestIndex);

            if (current == target || Vector2Int.Distance(current, target) <= 1.2f)
            {
                pathFound = true;
                finalNodeReached = current; // Save where we stopped
                break;
            }

            foreach (Vector2Int dir in directions)
            {
                Vector2Int next = current + dir;

                if (next.x >= 0 && next.x < mapSize && next.y >= 0 && next.y < mapSize)
                {
                    if (!grid[next.x, next.y])
                    {
                        float tentativeGCost = gCost[current] + Vector2Int.Distance(current, next);

                        if (!gCost.ContainsKey(next) || tentativeGCost < gCost[next])
                        {
                            gCost[next] = tentativeGCost;
                            cameFrom[next] = current;

                            if (!openSet.Contains(next))
                            {
                                openSet.Add(next);
                                if (next != start && next != end) mapTexture.SetPixel(next.x, next.y, scanColor);
                            }
                        }
                    }
                }
            }

            stepCount++;
            if (stepCount % 5 == 0)
            {
                mapTexture.Apply();
                displayImage.texture = mapTexture;
                yield return new WaitForSecondsRealtime(delayBetweenSteps);
            }
        }

        mapTexture.Apply();

        // THE FIX: Draw the golden line starting EXACTLY from the final node reached
        if (pathFound)
        {
            List<Vector2Int> path = new List<Vector2Int>();
            Vector2Int trace = finalNodeReached; 

            path.Add(target); // Ensure the exact target gets colored

            while (trace != root)
            {
                if (cameFrom.ContainsKey(trace))
                {
                    trace = cameFrom[trace];
                    if (trace != start && trace != end)
                    {
                        if (currentAlg == MapGenerator.MapType.Arena_DLite) path.Add(trace);
                        else path.Insert(0, trace);
                    }
                }
                else break;
            }

            for (int i = 0; i < path.Count; i++)
            {
                mapTexture.SetPixel(path[i].x, path[i].y, pathColor);
                if (path[i].x + 1 < mapSize) mapTexture.SetPixel(path[i].x + 1, path[i].y, pathColor);
                if (path[i].y + 1 < mapSize) mapTexture.SetPixel(path[i].x, path[i].y + 1, pathColor);

                mapTexture.Apply();
                displayImage.texture = mapTexture;
                yield return new WaitForSecondsRealtime(delayPathBuild);
            }
        }
        
        DrawLargePoint(start, startColor);
        DrawLargePoint(end, endColor);
        mapTexture.Apply();
    }

    public void CloseVisualization()
    {
        StopAllCoroutines();
        Time.timeScale = 1f; 
        visPanel.SetActive(false);
    }

    // ==========================================================
    // --- HELPER FUNCTIONS FOR FETCHING THE REAL PATH ---
    // ==========================================================

    List<Node> GetRealPathFromScripts(MapGenerator.MapType algType, Vector2Int startGrid, Vector2Int endGrid)
    {
        // ממירים קואורדינטות גריד חזרה למיקומי עולם (World Position)
        float offset = mapGenerator.mapSize / 2f;
        Vector3 startWorld = new Vector3(startGrid.x - offset + 0.5f, 1f, startGrid.y - offset + 0.5f);
        Vector3 endWorld = new Vector3(endGrid.x - offset + 0.5f, 1f, endGrid.y - offset + 0.5f);

        List<Node> path = null;
        
        // קריאה לסקריפטים האמיתיים במשחק
        switch (algType)
        {
            case MapGenerator.MapType.Maze_ARA:
                ARAStarPathfinder ara = FindFirstObjectByType<ARAStarPathfinder>();
                if (ara != null) path = ara.FindPath(startWorld, endWorld);
                break;
                
            case MapGenerator.MapType.Caverns_LPA:
                LPAStarPathfinder lpa = FindFirstObjectByType<LPAStarPathfinder>();
                if (lpa != null) path = lpa.FindPath(startWorld, endWorld);
                break;

            case MapGenerator.MapType.Arena_DLite:
            case MapGenerator.MapType.RandomScatter:
                DStarLitePathfinder dlite = FindFirstObjectByType<DStarLitePathfinder>();
                if (dlite != null) path = dlite.FindPath(startWorld, endWorld);
                break;
        }
        
        return path;
    }

    Vector2Int GetGridPosFromNode(Node node)
    {
        // המרת נתוני ה-World Position של הנתיב האמיתי חזרה לקואורדינטות בטקסטורה הדו-ממדית
        float offset = mapGenerator.mapSize / 2f;
        int x = Mathf.RoundToInt(node.worldPosition.x + offset - 0.5f);
        int y = Mathf.RoundToInt(node.worldPosition.z + offset - 0.5f);
        
        // מוודאים שאנחנו לא חורגים מגבולות המפה בשום מצב
        x = Mathf.Clamp(x, 0, mapGenerator.mapSize - 1);
        y = Mathf.Clamp(y, 0, mapGenerator.mapSize - 1);
        
        return new Vector2Int(x, y);
    }
}