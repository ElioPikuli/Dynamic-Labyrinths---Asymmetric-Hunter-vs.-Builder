using UnityEngine;

[CreateAssetMenu(fileName = "NewBiome", menuName = "Map/Biome Data")]
public class BiomeData : ScriptableObject
{
    [Header("Environment Prefabs")]
    public GameObject[] floorPrefabs;
    public GameObject[] obstaclePrefabs;

    [Header("Decorations (Non-Obstacles)")]
    public GameObject[] decorationPrefabs; 
    [Range(0, 100)]
    public int decorationChance = 15; 

    [Header("Atmosphere & Lighting")]
    public Material skyboxMaterial;      // השמיים הייחודיים לכל ביום
    public Color fogColor = Color.gray;   // צבע הערפל
    [Range(0f, 0.1f)]
    public float fogDensity = 0.01f;     // סמיכות הערפל (0.01 עדין, 0.05 כבד)
    public Color sunColor = Color.white;  // גוון אור השמש

    [Header("Weather Effects")]
    public GameObject weatherSystemPrefab; // גשם, שלג, עלים נושרים וכו'
}