using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Destructed : MonoBehaviour
{   
    void Awake() {
        Physics.IgnoreLayerCollision(3, LayerMask.NameToLayer("Destructed"));

    }
    void Start()
    {
        StartCoroutine(SelfDestruct());
    }
    IEnumerator SelfDestruct()
    {
        yield return new WaitForSeconds(5f);
        Destroy(gameObject);
    }
}
