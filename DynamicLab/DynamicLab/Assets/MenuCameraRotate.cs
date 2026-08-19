using UnityEngine;

public class MenuCameraRotate : MonoBehaviour
{
    [Header("Rotation Settings")]
    public float rotationSpeed = 2f;
    public Vector3 targetPoint = Vector3.zero; // The center of your map

    [Header("Hover Settings")]
    public float height = 15f;
    public float distance = 30f;

    void Start()
    {
        // Start the camera at a nice angle
        transform.position = new Vector3(distance, height, -distance);
    }

    void Update()
    {
        // Slowly orbit around the center of the map
        transform.RotateAround(targetPoint, Vector3.up, rotationSpeed * Time.deltaTime);
        
        // Always look at the center
        transform.LookAt(targetPoint);
    }
}