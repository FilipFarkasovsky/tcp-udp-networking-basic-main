using UnityEngine;
using UnityEngine.Animations.Rigging;

public class PlayerAnimation : MonoBehaviour
{
    public Animator animator;
    public Rig aimLayer;
    private bool isFiring;
    private Weapon weapon;
    private void Start(){
        Weapon existingWeapon = GetComponentInChildren<Weapon>();
        EquipWeapon(existingWeapon);
    }

    private void Update(){
        aimLayer.weight = 1f;

        if(isFiring) weapon.UpdateFiring(Time.deltaTime);
    }

    public void UpdateAnimatorProperties(float lateralSpeed, float forwardSpeed, bool grounded, bool jumping){
        animator.SetFloat("LateralSpeed", lateralSpeed);
        animator.SetFloat("ForwardSpeed", forwardSpeed);
        animator.SetBool("Grounded", grounded);
        animator.SetBool("Jumping", jumping);
    }
    public void IsFiring(bool _isFiring){
        isFiring = _isFiring;
    }

    public void EquipWeapon(Weapon newWeapon){
        weapon = newWeapon;
    }
}
