using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class MainMenuManager : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject mainMenuPanel;
    public GameObject loadingPanel;

    [Header("Loading UI")]
    public Slider loadingBar;
    public TextMeshProUGUI tooltipText;
    
    // Dynamic tooltips to show off your mechanics while loading!
    private string[] tooltips = {
        "Hint: ARA* quickly finds a path, then refines it over time.",
        "Hint: LPA* is highly efficient when the maze shifts around you.",
        "Hint: D* Lite calculates backward from the goal to adapt instantly.",
        "Hint: The Builder can place blocks to trap the Hunter!"
    };

    void Start()
    {
        // Ensure we start on the right screen with the mouse visible
        mainMenuPanel.SetActive(true);
        loadingPanel.SetActive(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void PlayGame()
    {
        StartCoroutine(LoadGameplayScene());
    }

    public void QuitGame()
    {
        Debug.Log("Quitting Game...");

        // If we are testing in the Unity Editor, stop playing
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        
        // If this is a final compiled build, actually quit the application
        #else
            Application.Quit();
        #endif
    }

    IEnumerator LoadGameplayScene()
    {
        // Swap panels
        mainMenuPanel.SetActive(false);
        loadingPanel.SetActive(true);

        // Pick a random tooltip
        if (tooltipText != null)
        {
            tooltipText.text = tooltips[Random.Range(0, tooltips.Length)];
        }

        // Asynchronously load the gameplay scene (Build Index 1)
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(1);

        while (!asyncLoad.isDone)
        {
            // Update the loading bar slider
            if (loadingBar != null)
            {
                // Unity's async progress goes from 0 to 0.9. We normalize it to 0 to 1.
                float progress = Mathf.Clamp01(asyncLoad.progress / 0.9f);
                loadingBar.value = progress;
            }
            yield return null;
        }
    }
}