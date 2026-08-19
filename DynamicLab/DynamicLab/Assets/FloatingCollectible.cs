using UnityEngine;

public class FloatingCollectible : MonoBehaviour
{
    [Header("Animation Settings")]
    public float spinSpeed = 90f;       // מהירות הסיבוב
    public float floatAmplitude = 0.25f; // כמה גבוה/נמוך האובייקט ירחף
    public float floatFrequency = 1f;    // קצב הריחוף (מהירות העלייה והירידה)

    private Vector3 startPos;

    void Start()
    {
        // שומרים את מיקום ההתחלה כדי לרחף סביבו
        startPos = transform.position;
    }

    void Update()
    {
        // 1. סיבוב האובייקט סביב ציר ה-Y
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

        // 2. חישוב הריחוף בעזרת פונקציית סינוס
        Vector3 tempPos = startPos;
        tempPos.y += Mathf.Sin(Time.time * Mathf.PI * floatFrequency) * floatAmplitude;
        
        // החלת המיקום החדש
        transform.position = tempPos;
    }
}