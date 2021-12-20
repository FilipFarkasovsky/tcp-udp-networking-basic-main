using System.Collections;
using UnityEngine;

/// <summary Handles wall destruction </summary>
public class Destructible : MonoBehaviour
{
    public enum Type
    {
        destructed,     // Already destructed
        destructible,   // Waiting to be destructed
    }

    [SerializeField] Type type; // Type of wall where is this script attched
    public GameObject destroyedVersion; // reference to version that will fall apart (DESTRUCTED)
    
    void Start()
    {
        if (type == Type.destructed)
        {
            // Ignore collisions with player
            Physics.IgnoreLayerCollision(3, LayerMask.NameToLayer("Destructible"));
            // Destroy shrapnels after time
            StartCoroutine(SelfDestruct(5f));
        }

    }

    // Destroy wall 
    public void Destruct(){
        if(type == Type.destructible)
        {
            Instantiate(destroyedVersion, transform.position, transform.rotation);
            StartCoroutine(SelfDestruct(0.1f));
        }
        
    }

    // Destroyes wall after time
    IEnumerator SelfDestruct(float time)
    {
        yield return new WaitForSeconds(time);
        Destroy(gameObject);
    }
}
