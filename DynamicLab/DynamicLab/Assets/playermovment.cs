using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public CharacterController controller;
    public float walkSpeed = 5f;
    public float runSpeed = 10f;
    public float gravity = -9.81f;
    public float lookSensitivity = 2f;

    Vector3 velocity;
    float xRotation = 0f;
    public Transform cameraTransform;

    void Start()
    {
        cameraTransform = GetComponentInChildren<Camera>().transform;
        // This locks the mouse to the center of the screen
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        // --- LOOK AROUND (Mouse) ---
        float mouseX = Input.GetAxis("Mouse X") * lookSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * lookSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f); // Prevents flipping upside down

        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);

        // --- MOVEMENT (Keyboard) ---
        float x = Input.GetAxis("Horizontal"); // A/D keys
        float z = Input.GetAxis("Vertical");   // W/S keys

        // Determine if we are running
        float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;

        // Calculate horizontal movement vector
        Vector3 move = transform.right * x + transform.forward * z;

        // --- GRAVITY ---
        // Reset downward velocity if standing on the ground
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; 
        }

        // Apply gravity over time
        velocity.y += gravity * Time.deltaTime;

        // --- THE FIX: Combine both into ONE single movement call ---
        // We multiply the horizontal move by speed, and add the vertical velocity
        Vector3 finalMovement = (move * currentSpeed) + velocity;
        
        // Move the Character Controller exactly once per frame
        controller.Move(finalMovement * Time.deltaTime);
    }
}