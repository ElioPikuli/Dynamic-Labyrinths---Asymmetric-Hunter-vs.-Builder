using UnityEngine;
using System.Collections.Generic;

public class ARAStarPathfinder : MonoBehaviour
{
    public PathfindingGrid grid;

    [Header("ARA* Settings")]
    public float initialEpsilon = 2.5f;
    public float epsilonDecreaseAmount = 0.5f;

    public List<Node> FindPath(Vector3 startPos, Vector3 targetPos)
    {
        // 1. Full reset only happens ONCE at the very beginning of the request
        ResetNodes();

        Node startNode = grid.NodeFromWorldPoint(startPos);
        Node targetNode = grid.NodeFromWorldPoint(targetPos);

        if (startNode == null || targetNode == null || !startNode.walkable || !targetNode.walkable) {
            Debug.LogWarning("ARA* Stop: Invalid Start or Target Node!");
            return null;
        }

        // Initialize Start Node
        startNode.gCost = 0;
        startNode.hCost = GetDistance(startNode, targetNode);

        List<Node> openSet = new List<Node>();
        HashSet<Node> closedSet = new HashSet<Node>();
        
        // --- NEW: The INCONS list holds nodes to be re-evaluated in the next pass ---
        List<Node> inconsSet = new List<Node>(); 

        openSet.Add(startNode);
        
        float currentEpsilon = initialEpsilon;
        List<Node> bestPathFound = null;

        // --- NEW: The "Anytime" Loop ---
        // Keep searching and refining the path until epsilon hits 1 (Optimal A*)
        while (currentEpsilon >= 1.0f)
        {
            List<Node> currentPath = ImprovePath(startNode, targetNode, openSet, closedSet, inconsSet, currentEpsilon);
            
            if (currentPath != null)
            {
                bestPathFound = currentPath; // Save the best path we found this pass
                Debug.Log($"ARA* Path found with Epsilon: {currentEpsilon}");
            }
            else if (bestPathFound == null)
            {
                // If we couldn't even find a path on the first pass, the target is unreachable
                Debug.LogWarning("ARA* Stop: Target is completely unreachable.");
                return null; 
            }

            // Decrease epsilon for the next refinement pass
            currentEpsilon -= epsilonDecreaseAmount;

            // --- NEW: The "Repairing" Phase ---
            // Move all Inconsistent nodes back into the Open set to repair the tree
            openSet.AddRange(inconsSet);
            inconsSet.Clear();
            closedSet.Clear(); 
            // Note: We DO NOT call ResetNodes() here! We keep all the gCost math from the last pass!
        }

        return bestPathFound;
    }

    // Helper method that does the actual searching for a single pass
    private List<Node> ImprovePath(Node startNode, Node targetNode, List<Node> openSet, HashSet<Node> closedSet, List<Node> inconsSet, float epsilon)
    {
        while (openSet.Count > 0)
        {
            // Find node with lowest fCost (inflated by current epsilon)
            Node currentNode = openSet[0];
            for (int i = 1; i < openSet.Count; i++)
            {
                float currentFCost = currentNode.gCost + (currentNode.hCost * epsilon);
                float compareFCost = openSet[i].gCost + (openSet[i].hCost * epsilon);

                if (compareFCost < currentFCost || (compareFCost == currentFCost && openSet[i].hCost < currentNode.hCost))
                {
                    currentNode = openSet[i];
                }
            }

            openSet.Remove(currentNode);
            closedSet.Add(currentNode);

            // If we found the target, retrace and return this specific iteration's path
            if (currentNode == targetNode)
            {
                return RetracePath(startNode, targetNode);
            }

            foreach (Node neighbor in grid.GetNeighbors(currentNode))
            {
                if (!neighbor.walkable) continue;

                float newMovementCostToNeighbor = currentNode.gCost + GetDistance(currentNode, neighbor);
                
                if (newMovementCostToNeighbor < neighbor.gCost)
                {
                    neighbor.gCost = newMovementCostToNeighbor;
                    neighbor.hCost = GetDistance(neighbor, targetNode);
                    neighbor.parent = currentNode;

                    // --- NEW: INCONS Logic ---
                    // If the neighbor is already closed, it means we found a BETTER route to a node 
                    // we already processed. Add it to INCONS so we evaluate it in the next epsilon pass!
                    if (!closedSet.Contains(neighbor) && !openSet.Contains(neighbor))
                    {
                        openSet.Add(neighbor);
                    }
                    else if (closedSet.Contains(neighbor) && !inconsSet.Contains(neighbor))
                    {
                        inconsSet.Add(neighbor);
                    }
                }
            }
        }
        
        return null; // Return null if openSet empties out without finding the target
    }

    void ResetNodes()
    {
        Node[,] nodes = grid.GetGrid(); 
        if (nodes == null) return;

        int width = nodes.GetLength(0);
        int height = nodes.GetLength(1);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                nodes[x, y].gCost = float.MaxValue;
                nodes[x, y].hCost = 0;
                nodes[x, y].parent = null;
            }
        }
    }

    List<Node> RetracePath(Node startNode, Node endNode)
    {
        List<Node> path = new List<Node>();
        Node currentNode = endNode;

        while (currentNode != startNode)
        {
            path.Add(currentNode);
            currentNode = currentNode.parent;
        }
        path.Reverse();
        return path;
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