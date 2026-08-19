using UnityEngine;

public class Node {
    public bool walkable;
    public Vector3 worldPosition;
    public int gridX;
    public int gridY;

    public float gCost; // Distance from start
    public float hCost; // Estimated distance to goal
    public float rhs;   // "Lookahead" value (key for LPA* and D* Lite)
    
    public Node parent; // To retrace the path

    public Node(bool _walkable, Vector3 _worldPos, int _gridX, int _gridY) {
        walkable = _walkable;
        worldPosition = _worldPos;
        gridX = _gridX;
        gridY = _gridY;
        
        // Initialize with high values
        gCost = float.MaxValue;
        rhs = float.MaxValue;
    }

    public float fCost {
        get { return gCost + hCost; }
    }
}