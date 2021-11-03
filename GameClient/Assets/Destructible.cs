using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Destructible : MonoBehaviour
{
    public GameObject destroyedVersion;
    
    public void Destruct(){
        Instantiate(destroyedVersion, transform.position, transform.rotation);
        StartCoroutine(SelfDestruct());
        
    }
    IEnumerator SelfDestruct()
    {
        yield return new WaitForSeconds(0.01f);
        Destroy(gameObject);
    }
}
