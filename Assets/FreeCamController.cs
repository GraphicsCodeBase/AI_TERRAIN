using UnityEngine;
using UnityEngine.EventSystems; 

public class FreeCameraController : MonoBehaviour
{
    public float mouseSensitivity = 100f;
    public float moveSpeed = 10f;

    float xRotation = 0f;
    float yRotation = 0f;

    private bool mouseLookEnabled = true;

    void Start()
    {
        EnableMouseLook(true);
    }

    void Update()
    {
        // Prevent WASD from controlling UI sliders
        if (EventSystem.current.currentSelectedGameObject != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        // Toggle mouse look on Tab press
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            mouseLookEnabled = !mouseLookEnabled;
            EnableMouseLook(mouseLookEnabled);
        }

        if (mouseLookEnabled)
            HandleMouseLook();

        HandleMovement();
    }

    void EnableMouseLook(bool enable)
    {
        Cursor.lockState = enable ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !enable;
    }

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        yRotation += mouseX;

        transform.localRotation = Quaternion.Euler(xRotation, yRotation, 0f);
    }

    void HandleMovement()
    {
        float moveX = Input.GetAxis("Horizontal"); // A/D
        float moveZ = Input.GetAxis("Vertical");   // W/S

        // Q = down, E = up
        float moveY = 0f;
        if (Input.GetKey(KeyCode.E)) moveY += 1f;
        if (Input.GetKey(KeyCode.Q)) moveY -= 1f;

        Vector3 move = transform.right * moveX + transform.forward * moveZ + transform.up * moveY;
        transform.position += move * moveSpeed * Time.deltaTime;
    }
}
