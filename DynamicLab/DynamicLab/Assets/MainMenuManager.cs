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
    public GameObject settingsPanel; // הפאנל של ההגדרות

    [Header("Settings UI")]
    public TMP_Dropdown mapDropdown; // רשימת המפות (0=Random, 1=Maze, 2=Caverns, 3=Arena)
    public TMP_InputField sizeInput; // שדה הקלדת גודל המפה

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
        // טעינת ההגדרות השמורות כשהמשחק עולה (אם אין, נשים 100 ו-0 כברירת מחדל)
        if (mapDropdown != null) mapDropdown.value = PlayerPrefs.GetInt("SavedMapType", 0);
        if (sizeInput != null) sizeInput.text = PlayerPrefs.GetInt("SavedMapSize", 100).ToString();

        // Ensure we start on the right screen with the mouse visible
        mainMenuPanel.SetActive(true);
        loadingPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // --- מערכת ההגדרות ---

    public void OpenSettings()
    {
        mainMenuPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        // 1. שמירת סוג המפה
        if (mapDropdown != null) 
        {
            PlayerPrefs.SetInt("SavedMapType", mapDropdown.value);
        }
        
        // 2. שמירת גודל המפה (עם הגנה מקריסות)
        if (sizeInput != null)
        {
            if (int.TryParse(sizeInput.text, out int size))
            {
                // אם השחקן הקליד מספר קטן מדי, נתקן אותו ל-30 כדי למנוע באגים
                if (size < 30) size = 30;
                PlayerPrefs.SetInt("SavedMapSize", size);
                sizeInput.text = size.ToString(); // מעדכן את השדה למספר התקין
            }
            else
            {
                // אם השחקן הקליד בטעות אותיות, נחזיר ל-100
                PlayerPrefs.SetInt("SavedMapSize", 100);
                sizeInput.text = "100";
            }
        }

        PlayerPrefs.Save(); // שומר פיזית לזיכרון של המכשיר

        // חזרה לתפריט הראשי
        settingsPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }

    // --- מערכת קיימת ---

    public void PlayGame()
    {
        StartCoroutine(LoadGameplayScene());
    }

    public void QuitGame()
    {
        Debug.Log("Quitting Game...");

        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }

    IEnumerator LoadGameplayScene()
    {
        mainMenuPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        loadingPanel.SetActive(true);

        if (tooltipText != null)
        {
            tooltipText.text = tooltips[Random.Range(0, tooltips.Length)];
        }

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(1);

        while (!asyncLoad.isDone)
        {
            if (loadingBar != null)
            {
                float progress = Mathf.Clamp01(asyncLoad.progress / 0.9f);
                loadingBar.value = progress;
            }
            yield return null;
        }
    }
}