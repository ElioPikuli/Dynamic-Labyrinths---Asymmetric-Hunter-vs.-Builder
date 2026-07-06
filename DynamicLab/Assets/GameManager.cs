using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    
    [Header("UI Elements")]
    public GameObject gameOverPanel; 

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public void TriggerGameOver()
    {
        // מדליק את מסך ההפסד ועוצר את הזמן (מקפיא את הבוטים)
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        Time.timeScale = 0f; 
    }

    // פונקציה חדשה שרק מחזירה את הזמן לרוץ כרגיל
    public void ResumeTime()
    {
        Time.timeScale = 1f;
    }
}