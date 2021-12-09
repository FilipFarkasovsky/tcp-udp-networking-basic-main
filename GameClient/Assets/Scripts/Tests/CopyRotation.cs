using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Copys yaw rotation
/// </summary>
public class CopyRotation : MonoBehaviour
{
    public Transform playerCamera;
    // Update is called once per frame
    void Update()
    {
        transform.rotation =Quaternion.Euler(0f, playerCamera.transform.rotation.eulerAngles.y, 0f);
    }
}
