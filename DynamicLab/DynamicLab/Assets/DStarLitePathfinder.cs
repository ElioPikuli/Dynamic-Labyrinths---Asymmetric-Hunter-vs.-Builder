using UnityEngine;
using System.Collections.Generic;
using System;

public class DStarLitePathfinder : MonoBehaviour
{
    public PathfindingGrid grid;

    private Node startNode; // The Agent/Hunter
    private Node targetNode; // The Goal
    private Node lastStartNode; // Used to track how far the agent moved between recalculations

    private Dictionary<Node, float> gValues = new Dictionary<Node, float>();
    private Dictionary<Node, float> rhsValues = new Dictionary<Node, float>();
    private List<Node> openSet = new List<Node>();

    // The Key Modifier: Accounts for the agent moving while the map changes
    private float km = 0; 
    private bool isInitialized = false;

    // A struct to handle D* Lite's two-tier priority keys
    private struct DKey
    {
        public float k1; // f-cost
        public float k2; // g-cost
    }

    // --- 1. INITIALIZATION ---
    public void Initialize(Vector3 startPos, Vector3 targetPos)
    {
        startNode = grid.NodeFromWorldPoint(startPos);
        targetNode = grid.NodeFromWorldPoint(targetPos);
        lastStartNode = startNode;

        gValues.Clear();
        rhsValues.Clear();
        openSet.Clear();
        km = 0;

        foreach (Node node in grid.GetGrid())
        {
            gValues[node] = float.MaxValue;
            rhsValues[node] = float.MaxValue;
        }

        // D* Lite trick: We initialize the TARGET node, not the start node!
        rhsValues[targetNode] = 0;
        openSet.Add(targetNode);
        
        isInitialized = true;
        ComputeShortestPath();
    }

    // --- 2. THE MAIN PATHFINDING CALL ---
    public List<Node> FindPath(Vector3 currentPos, Vector3 targetPos)
    {
        Node currentStart = grid.NodeFromWorldPoint(currentPos);
        Node currentTarget = grid.NodeFromWorldPoint(targetPos);

        // If the GOAL completely changes, we must completely restart
        if (!isInitialized || currentTarget != targetNode)
        {
            Initialize(currentPos, targetPos);
            currentStart = grid.NodeFromWorldPoint(currentPos);
        }

        // If the agent moved, we don't recalculate everything! We just update our tracking.
        if (currentStart != startNode)
        {
            startNode = currentStart;
        }

        if (rhsValues[startNode] == float.MaxValue)
        {
            Debug.LogWarning("D* Lite: Path is completely blocked!");
            return null;
        }

        return RetracePath();
    }

    // --- 3. THE DYNAMIC UPDATE (Call when Builder drops a wall) ---
    public void MapChangedAt(Vector3 worldPosition)
    {
        if (!isInitialized) return;

        // 1. Update the Key Modifier based on how far the agent moved since the last map change
        km += GetDistance(lastStartNode, startNode);
        lastStartNode = startNode;

        Node changedNode = grid.NodeFromWorldPoint(worldPosition);

        // 2. Update the changed node and its neighbors
        UpdateVertex(changedNode);
        foreach (Node neighbor in grid.GetNeighbors(changedNode))
        {
            UpdateVertex(neighbor);
        }

        // 3. Repair the path mathematically
        ComputeShortestPath();
    }

    // --- CORE D* LITE MATH ---
    void ComputeShortestPath()
    {
        int maxIterations = 10000;
        int iterations = 0;

        while (openSet.Count > 0 && iterations < maxIterations)
        {
            iterations++;
            openSet.Sort(CompareNodes); // Sort by our custom keys
            Node u = openSet[0];

            DKey kOld = CalculateKey(u);
            DKey kNew = CalculateKey(u, true); // Recalculate to see if 'km' changed it

            // If the target is reached and the path is consistent, we're done!
            if (CompareKeys(kOld, CalculateKey(startNode)) >= 0 && rhsValues[startNode] == gValues[startNode])
            {
                break;
            }

            openSet.RemoveAt(0);

            if (CompareKeys(kOld, kNew) < 0)
            {
                // The node's key is outdated, put it back with the new key
                openSet.Add(u);
            }
            else if (gValues[u] > rhsValues[u])
            {
                // Node is Locally Overconsistent
                gValues[u] = rhsValues[u];
                foreach (Node neighbor in grid.GetNeighbors(u))
                {
                    UpdateVertex(neighbor);
                }
            }
            else
            {
                // Node is Locally Underconsistent
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
        // We look from 'u' to its neighbors (which act as successors because we search backward)
        if (u != targetNode)
        {
            float minRhs = float.MaxValue;
            foreach (Node neighbor in grid.GetNeighbors(u))
            {
                if (!neighbor.walkable) continue;

                float cost = (gValues[neighbor] == float.MaxValue) ? float.MaxValue : gValues[neighbor] + GetDistance(neighbor, u);
                if (cost < minRhs)
                {
                    minRhs = cost;
                }
            }
            rhsValues[u] = minRhs;
        }

        if (openSet.Contains(u)) openSet.Remove(u);

        if (gValues[u] != rhsValues[u])
        {
            openSet.Add(u);
        }
    }

    // --- UTILITIES ---

    // D* Lite traces FORWARD from the Start (Agent) down the 'g' gradient to the Goal
    List<Node> RetracePath()
    {
        List<Node> path = new List<Node>();
        Node currentNode = startNode;

        int safetyNet = 0;
        while (currentNode != targetNode && safetyNet < 10000)
        {
            safetyNet++;
            path.Add(currentNode);

            Node bestNeighbor = null;
            float lowestCost = float.MaxValue;

            foreach (Node neighbor in grid.GetNeighbors(currentNode))
            {
                if (!neighbor.walkable) continue;

                float cost = (gValues[neighbor] == float.MaxValue) ? float.MaxValue : gValues[neighbor] + GetDistance(currentNode, neighbor);
                if (cost < lowestCost)
                {
                    lowestCost = cost;
                    bestNeighbor = neighbor;
                }
            }

            if (bestNeighbor != null && lowestCost != float.MaxValue) {
                currentNode = bestNeighbor;
            } else {
                break; // Path blocked
            }
        }

        path.Add(targetNode);
        return path;
    }

    DKey CalculateKey(Node s, bool ignoreKM = false)
    {
        float minVal = Mathf.Min(gValues.ContainsKey(s) ? gValues[s] : float.MaxValue, 
                                 rhsValues.ContainsKey(s) ? rhsValues[s] : float.MaxValue);
        
        // Notice the heuristic is calculated from the node to the START node, not the goal!
        float k1 = minVal + GetDistance(s, startNode) + (ignoreKM ? 0 : km);
        float k2 = minVal;
        
        return new DKey { k1 = k1, k2 = k2 };
    }

    int CompareNodes(Node a, Node b)
    {
        return CompareKeys(CalculateKey(a), CalculateKey(b));
    }

    int CompareKeys(DKey a, DKey b)
    {
        if (a.k1 < b.k1) return -1;
        if (a.k1 > b.k1) return 1;
        if (a.k2 < b.k2) return -1;
        if (a.k2 > b.k2) return 1;
        return 0;
    }

    int GetDistance(Node nodeA, Node nodeB)
    {
        if (nodeA == null || nodeB == null) return 0;
        
        int dstX = Mathf.Abs(nodeA.gridX - nodeB.gridX);
        int dstY = Mathf.Abs(nodeA.gridY - nodeB.gridY);

        if (dstX > dstY)
            return 14 * dstY + 10 * (dstX - dstY);
        return 14 * dstX + 10 * (dstY - dstX);
    }
}