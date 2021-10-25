using UnityEngine;

public class CameraLook : MonoBehaviour
{
    public Camera playerCamera;

    private void Update()
    {
        Rotation();
    }

    private void Rotation()
    {
        Quaternion cameraRotation = playerCamera.transform.rotation;
        transform.rotation = Quaternion.Euler(0f, cameraRotation.eulerAngles.y, 0f);     
    }
}
