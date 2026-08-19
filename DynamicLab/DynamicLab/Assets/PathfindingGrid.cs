using UnityEngine;
using System.Collections.Generic;

public class PathfindingGrid : MonoBehaviour 
{
    public float nodeRadius = 0.5f;
    Node[,] grid;
    
    // We will store the dynamic size and offset here
    int mapSize;
    float offset;

    public void CreateGrid() 
    {
        // 1. Ask the MapGenerator for the blueprint AND the size!
        MapGenerator mapGen = FindFirstObjectByType<MapGenerator>();
        bool[,] logicalGrid = null;

        if (mapGen != null) 
        {
            logicalGrid = mapGen.GetGrid();
            mapSize = mapGen.mapSize; // Instantly adapt to the Inspector setting
        }
        else 
        {
            mapSize = 100; // Failsafe
        }

        // Calculate the center offset dynamically (always half the map size)
        offset = mapSize / 2f;

        // Create the mathematical grid exactly matching the map size
        grid = new Node[mapSize, mapSize];

        for (int x = 0; x < mapSize; x++) 
        {
            for (int y = 0; y < mapSize; y++) 
            {
                // NO MORE 50f: We use our dynamic 'offset' instead
                Vector3 worldPoint = transform.position + Vector3.right * (x * 1f - offset + 0.5f) + Vector3.forward * (y * 1f - offset + 0.5f);
                
                bool walkable = true;
                if (logicalGrid != null && x < logicalGrid.GetLength(0) && y < logicalGrid.GetLength(1)) 
                {
                    walkable = !logicalGrid[x, y];
                }
                
                grid[x, y] = new Node(walkable, worldPoint, x, y);
            }
        }
    }

    public List<Node> GetNeighbors(Node node) 
    {
        List<Node> neighbors = new List<Node>();
        for (int x = -1; x <= 1; x++) 
        {
            for (int y = -1; y <= 1; y++) 
            {
                if (x == 0 && y == 0) continue;
                
                int checkX = node.gridX + x;
                int checkY = node.gridY + y;
                
                // NO MORE 100: We check against the dynamic 'mapSize'
                if (checkX >= 0 && checkX < mapSize && checkY >= 0 && checkY < mapSize) 
                    neighbors.Add(grid[checkX, checkY]);
            }
        }
        return neighbors;
    }

    public Node NodeFromWorldPoint(Vector3 worldPosition) 
    {
        // NO MORE 50f: We use our dynamic 'offset' to calculate math
        int x = Mathf.FloorToInt(worldPosition.x + offset);
        int y = Mathf.FloorToInt(worldPosition.z + offset);
        
        // Clamp it safely inside the new dynamic map size
        x = Mathf.Clamp(x, 0, mapSize - 1);
        y = Mathf.Clamp(y, 0, mapSize - 1);
        
        return grid[x, y];
    }

    public Node[,] GetGrid() { return grid; }
}