using UnityEngine;

public class PuzzleItem : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        // Check if the object entering the trigger is the Player
        if (other.CompareTag("Player"))
        {
            // Tell the LevelManager to add a point
            LevelManager.instance.CollectPuzzle();
            
            // Remove the puzzle from the map
            Destroy(gameObject);
        }
    }
}