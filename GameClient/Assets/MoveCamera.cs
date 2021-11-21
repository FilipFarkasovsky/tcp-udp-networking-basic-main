using UnityEngine;

public class MoveCamera : MonoBehaviour
{

    public Transform playerHead;
    public GameObject player;
    public Camera Cam;
    public float wallRunTilt = 25f;

    // Try also LateUpdate
    void Update()
    {
        transform.position = playerHead.transform.position;
    }
}
