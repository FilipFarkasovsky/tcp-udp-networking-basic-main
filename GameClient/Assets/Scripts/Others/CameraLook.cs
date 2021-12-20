using UnityEngine;

/// <summary Handles rotation of local players camera</summary>
public class CameraLook : MonoBehaviour
{
    static Convar rotationBounds = new Convar("sv_maxrotation", 89f, "Maximum rotation around the x axis", Flags.NETWORK);
    static Convar rotationSensitivity = new Convar("sensitivity", 2.5f, "Camera rotation sensitivity", Flags.CLIENT);

    public Transform playerCamera;

    private ConsoleUI consoleUI;

    private float rotationX; //yaw rotation in degrees
    private float rotationY;  //pitch rotation in degrees

    private void Start()
    {
        rotationY = playerCamera.transform.localEulerAngles.x;
        rotationX = transform.eulerAngles.y;
        consoleUI = FindObjectOfType<ConsoleUI>();
    }

    private void FixedUpdate()
    {
        if (consoleUI.isActive())
            return;

        Rotation();
    }

    private void Rotation()
    {
        if (Input.GetKey(KeyCode.Escape) && Cursor.lockState == CursorLockMode.None)
        {
            Cursor.lockState = CursorLockMode.Locked;
        }
        else if (Input.GetKey(KeyCode.Escape) && Cursor.lockState == CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.None;
        }

        rotationY += -Input.GetAxis("Mouse Y") * rotationSensitivity.GetValue();
        rotationX += Input.GetAxis("Mouse X") * rotationSensitivity.GetValue();

        rotationY = Mathf.Clamp(rotationY, -rotationBounds.GetValue(), rotationBounds.GetValue());

        playerCamera.transform.rotation = Quaternion.Euler(rotationY, rotationX, 0f);
        transform.rotation = Quaternion.Euler(0f, rotationX, 0f);
    }
}
