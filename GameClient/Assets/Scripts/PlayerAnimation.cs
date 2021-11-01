using UnityEngine;
using UnityEngine.Animations.Rigging;

public class PlayerAnimation : MonoBehaviour
{
    public Animator animator;
    public Camera playerCamera;
    public float aimDuration = 0.3f;
    public Rig aimLayer;
    private bool isFiring;
    private bool isAiming;
    public ParticleSystem muzzleFlash;
    public ParticleSystem hitEffect;
    public TrailRenderer tracerEffect;
    private Ray ray;
    private RaycastHit hitInfo;
    public LayerMask whatIsHittable;
    public int fireRate = 20;
    float acccumulatedTime = 0;
    private void Update(){
        if(isAiming){
            aimLayer.weight += Time.deltaTime / aimDuration;
        }
        else {
            aimLayer.weight -= Time.deltaTime / aimDuration;
        }

        if(isFiring) UpdateFiring(Time.deltaTime);
    }

    public void UpdateAnimatorProperties(float lateralSpeed, float forwardSpeed, bool grounded, bool jumping){
        animator.SetFloat("LateralSpeed", lateralSpeed);
        animator.SetFloat("ForwardSpeed", forwardSpeed);
        animator.SetBool("Grounded", grounded);
        animator.SetBool("Jumping", jumping);
    }
    public void IsAiming(bool _isAiming = true){
        isAiming = _isAiming;
    }
    public void IsFiring(bool _isFiring){
        isFiring = _isFiring;
    }

    public void UpdateFiring(float deltaTime){
        acccumulatedTime += deltaTime;
        float fireInterval = 1f/fireRate;
        while(acccumulatedTime >=0 ){
            FireBullet();
            acccumulatedTime -= fireInterval;
        }

    }
    public void FireBullet(){
        //Muzzle Flash
        muzzleFlash.Emit(1);

        //Hit Effect
        ray.origin = playerCamera.transform.position;
        ray.direction = playerCamera.transform.forward;
        RaycastHit[] hits = Physics.RaycastAll(playerCamera.transform.position, playerCamera.transform.forward, 100, whatIsHittable);
        if(hits.Length > 1){
            for (int i = 0; i < hits.Length; i++){
                if (hits[i].transform.gameObject.layer == LayerMask.NameToLayer("Ground")||
                    hits[i].transform.gameObject.layer == LayerMask.NameToLayer("Enemy")||
                    hits[i].transform.gameObject.layer == LayerMask.NameToLayer("Object")
                ){
                    hitEffect.transform.position = hits[i].point;
                    hitEffect.transform.forward = hits[i].normal;
                    hitEffect.Emit(1);
                }
            }
        }
        else if(hits.Length == 1){
                    hitEffect.transform.position = hits[0].point;
                    hitEffect.transform.forward = hits[0].normal;
                    hitEffect.Emit(1);
        }

        //Trail Effect
        Physics.Raycast(ray, out hitInfo, 100f, LayerMask.NameToLayer("Ground"));
        TrailRenderer tracer = Instantiate(tracerEffect, ray.origin, Quaternion.identity);
        tracer.AddPosition(muzzleFlash.transform.position);
        if(hitInfo.point != Vector3.zero){
        tracer.transform.position = hitInfo.point;
        }
        else{
        tracer.transform.position = playerCamera.transform.position + (playerCamera.transform.forward * 100);
        }
    }
    /// <summary> Finds the first point playerAim hits</summary>
    public Vector3 HitPoint(){
        RaycastHit[] hits = Physics.RaycastAll(playerCamera.transform.position, playerCamera.transform.forward, 100, whatIsHittable);
        if(hits.Length < 1) return playerCamera.transform.position + (playerCamera.transform.forward * 100);
        if(hits.Length > 1){
            for (int i = 0; i < hits.Length; i++){
                if (hits[i].transform.gameObject.layer == LayerMask.NameToLayer("Ground")||
                    hits[i].transform.gameObject.layer == LayerMask.NameToLayer("Enemy")||
                    hits[i].transform.gameObject.layer == LayerMask.NameToLayer("Object")
                ){
                    return hits[i].point;
                }
            }
        }
        return hits[0].point;
    }
}
