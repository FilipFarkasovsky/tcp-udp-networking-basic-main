using UnityEngine;

public class CarNetwork : MonoBehaviour
{
    void FixedUpdate()
    {
        SendMessages.CarTransform(transform);
    }
}
