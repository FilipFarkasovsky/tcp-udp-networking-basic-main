using UnityEngine;
using UnityEngine.Animations.Rigging;

public class PlayerAnimation : MonoBehaviour
{
    public Animator animator;
    public Camera playerCamera;
    public float aimDuration = 0.3f;
    public Rig aimLayer;

    private void Update(){
        if(Input.GetMouseButton(1)){
            aimLayer.weight += Time.deltaTime / aimDuration;
        }
        else {
            aimLayer.weight -= Time.deltaTime / aimDuration;
        }
    }

    public void UpdateAnimatorProperties(float lateralSpeed, float forwardSpeed, bool grounded, bool jumping){
        animator.SetFloat("LateralSpeed", lateralSpeed);
        animator.SetFloat("ForwardSpeed", forwardSpeed);
        animator.SetBool("Grounded", grounded);
        animator.SetBool("Jumping", jumping);
    }
}
