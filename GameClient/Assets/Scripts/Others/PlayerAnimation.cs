using UnityEngine;
using UnityEngine.Animations.Rigging;

/// <summary> Controls animation of the player</summary>
public class PlayerAnimation : MonoBehaviour
{
    public Animator animator;
    public Rig aimLayer;
    public Rig handsIK;
    public bool isFiring;
    private Weapon weapon;
    public Transform weaponParent;

    int LateralSpeed;
    int ForwardSpeed;
    int Grounded;
    int Jumping;

    private void Start(){
        Weapon existingWeapon = GetComponentInChildren<Weapon>();
        EquipWeapon(existingWeapon);

        LateralSpeed = Animator.StringToHash("LateralSpeed");
        ForwardSpeed = Animator.StringToHash("ForwardSpeed");
        Grounded = Animator.StringToHash("Grounded");
        Jumping = Animator.StringToHash("Jumping");
    }

    private void LateUpdate(){
        if(weapon){
            handsIK.weight = 1f;
            if(isFiring) weapon.UpdateFiring(Time.deltaTime);
        }
        else{
        handsIK.weight = 0f;
        }
    }

    public void UpdateAnimatorProperties(float lateralSpeed, float forwardSpeed, bool grounded, bool jumping){
        animator.SetFloat(LateralSpeed, lateralSpeed);
        animator.SetFloat(ForwardSpeed, forwardSpeed);
        animator.SetBool(Grounded, grounded);
        animator.SetBool(Jumping, jumping);
    }
    public void IsFiring(bool _isFiring){
        isFiring = _isFiring;
    }

    public void EquipWeapon(Weapon newWeapon){
        if(weapon){
            Destroy(weapon.gameObject);
        }
        weapon = newWeapon;
        weapon.transform.parent = weaponParent;
        weapon.transform.localPosition = Vector3.zero;
        weapon.transform.localRotation = Quaternion.identity;
    }
}
