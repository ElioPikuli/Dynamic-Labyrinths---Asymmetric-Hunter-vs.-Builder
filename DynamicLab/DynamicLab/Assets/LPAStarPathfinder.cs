using UnityEngine;
using System.Collections.Generic;

public class LPAStarPathfinder : MonoBehaviour
{
    public PathfindingGrid grid;
    
    private Node startNode;
    private Node targetNode;

    // LPA* relies on persisting these values across multiple frames/updates
    private Dictionary<Node, float> gValues = new Dictionary<Node, float>();
    private Dictionary<Node, float> rhsValues = new Dictionary<Node, float>();
    private List<Node> openSet = new List<Node>();

    private bool isInitialized = false;

    // --- 1. INITIALIZATION ---
    public void Initialize(Vector3 startPos, Vector3 targetPos)
    {
        startNode = grid.NodeFromWorldPoint(startPos);
        targetNode = grid.NodeFromWorldPoint(targetPos);

        gValues.Clear();
        rhsValues.Clear();
        openSet.Clear();

        // Default all nodes to Infinity (unreached)
        foreach (Node node in grid.GetGrid())
        {
            gValues[node] = float.MaxValue;
            rhsValues[node] = float.MaxValue;
        }

        // The start node is free to reach from itself
        rhsValues[startNode] = 0;
        openSet.Add(startNode);
        
        isInitialized = true;
    }

    // --- 2. THE MAIN PATHFINDING CALL ---
    public List<Node> FindPath(Vector3 startPos, Vector3 targetPos)
    {
        Node currentStart = grid.NodeFromWorldPoint(startPos);
        Node currentTarget = grid.NodeFromWorldPoint(targetPos);

        // If the start or target completely changed, we must re-initialize
        if (!isInitialized || currentStart != startNode || currentTarget != targetNode)
        {
            Initialize(startPos, targetPos);
        }

        ComputeShortestPath();

        if (rhsValues[targetNode] == float.MaxValue)
        {
            Debug.LogWarning("LPA*: Path is completely blocked!");
            return null; // Target is unreachable
        }

        return RetracePath();
    }

    // --- 3. THE LIFELONG UPDATE (Call this when a wall is placed/destroyed!) ---
    public void MapChangedAt(Vector3 worldPosition)
    {
        if (!isInitialized) return;

        Node changedNode = grid.NodeFromWorldPoint(worldPosition);
        
        // Update the changed node and all its neighbors
        UpdateVertex(changedNode);
        foreach (Node neighbor in grid.GetNeighbors(changedNode))
        {
            UpdateVertex(neighbor);
        }

        // The next time the bot calls FindPath(), it will instantly compute the fix
    }

    // --- CORE LPA* MATH ---
    void ComputeShortestPath()
    {
        int maxIterations = 10000; // Safety net to prevent Unity freezing
        int iterations = 0;

        while (openSet.Count > 0 && iterations < maxIterations)
        {
            iterations++;
            
            // Sort OpenSet based on the LPA* Two-Tier Key System
            openSet.Sort(CompareNodes);
            Node u = openSet[0];

            // If the target is reached and consistent, we are done!
            if (CompareNodes(u, targetNode) >= 0 && rhsValues[targetNode] == gValues[targetNode])
            {
                break;
            }

            openSet.RemoveAt(0);

            if (gValues[u] > rhsValues[u]) 
            {
                // Node is "Locally Overconsistent" - we found a valid path to it
                gValues[u] = rhsValues[u];
                foreach (Node neighbor in grid.GetNeighbors(u))
                {
                    UpdateVertex(neighbor);
                }
            }
            else 
            {
                // Node is "Locally Underconsistent" - a path to it was blocked
                gValues[u] = float.MaxValue;
                UpdateVertex(u);
                foreach (Node neighbor in grid.GetNeighbors(u))
                {
                    UpdateVertex(neighbor);
                }
            }
        }
    }

    void UpdateVertex(Node u)
    {
        if (u != startNode)
        {
            float minRhs = float.MaxValue;
            foreach (Node neighbor in grid.GetNeighbors(u))
            {
                if (!neighbor.walkable) continue;

                // Calculate cost to travel from neighbor to u
                float cost = (gValues[neighbor] == float.MaxValue) ? float.MaxValue : gValues[neighbor] + GetDistance(neighbor, u);
                if (cost < minRhs)
                {
                    minRhs = cost;
                }
            }
            rhsValues[u] = minRhs;
        }

        openSet.Remove(u);

        // If g != rhs, the node is inconsistent and needs to be evaluated
        if (gValues[u] != rhsValues[u])
        {
            openSet.Add(u);
        }
    }

    // --- UTILITIES ---

    // LPA* traces backwards from the Goal to the Start by following the cheapest 'g' values
    List<Node> RetracePath()
    {
        List<Node> path = new List<Node>();
        Node currentNode = targetNode;

        int safetyNet = 0;
        while (currentNode != startNode && safetyNet < 10000)
        {
            safetyNet++;
            path.Add(currentNode);

            Node bestNeighbor = null;
            float lowestCost = float.MaxValue;

            foreach (Node neighbor in grid.GetNeighbors(currentNode))
            {
                if (!neighbor.walkable) continue;

                float cost = (gValues[neighbor] == float.MaxValue) ? float.MaxValue : gValues[neighbor] + GetDistance(neighbor, currentNode);
                if (cost < lowestCost)
                {
                    lowestCost = cost;
                    bestNeighbor = neighbor;
                }
            }

            if (bestNeighbor != null) {
                currentNode = bestNeighbor;
            } else {
                break; // Stuck
            }
        }

        path.Add(startNode);
        path.Reverse();
        return path;
    }

    // LPA* Key Comparison Logic: Primary Key = fCost, Secondary Key = gCost
    int CompareNodes(Node a, Node b)
    {
        float minA = Mathf.Min(gValues.ContainsKey(a) ? gValues[a] : float.MaxValue, rhsValues.ContainsKey(a) ? rhsValues[a] : float.MaxValue);
        float minB = Mathf.Min(gValues.ContainsKey(b) ? gValues[b] : float.MaxValue, rhsValues.ContainsKey(b) ? rhsValues[b] : float.MaxValue);

        float fA = minA + GetDistance(a, targetNode);
        float fB = minB + GetDistance(b, targetNode);

        if (fA < fB) return -1;
        if (fA > fB) return 1;

        // If fCosts are tied, favor the one with the lowest actual known cost (minA/minB)
        if (minA < minB) return -1;
        if (minA > minB) return 1;

        return 0;
    }

    int GetDistance(Node nodeA, Node nodeB)
    {
        int dstX = Mathf.Abs(nodeA.gridX - nodeB.gridX);
        int dstY = Mathf.Abs(nodeA.gridY - nodeB.gridY);

        if (dstX > dstY)
            return 14 * dstY + 10 * (dstX - dstY);
        return 14 * dstX + 10 * (dstY - dstX);
    }
}