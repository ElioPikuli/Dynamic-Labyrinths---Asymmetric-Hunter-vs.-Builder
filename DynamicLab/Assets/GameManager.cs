using UnityEngine;
using Unity.Netcode; // ADDED: Required for network communication

// CHANGED: Must inherit from NetworkBehaviour to send messages across the network
public class GameManager : NetworkBehaviour 
{
    public static GameManager Instance;
    
    [Header("UI Elements")]
    public GameObject gameOverPanel; 

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    // The HunterAgent calls this method when the 3 seconds are up
    public void TriggerGameOver()
    {
        // Security check: Only the Server (PC) is allowed to initiate a Game Over
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            TriggerGameOverRpc();
        }
    }

    // This tag blasts the command to EVERY connected device (PC and Phone)
    [Rpc(SendTo.Everyone)]
    private void TriggerGameOverRpc()
    {
        // This will now activate the UI and freeze the bots on BOTH screens simultaneously!
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        Time.timeScale = 0f; 
    }

    // Resumes time (useful for Restart / Next Map buttons)
    public void ResumeTime()
    {
        Time.timeScale = 1f;
    }
}