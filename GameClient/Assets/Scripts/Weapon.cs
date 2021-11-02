using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Weapon : MonoBehaviour
{
    [Header("Raycast settings")]
    public LayerMask whatIsHittable;
    public GameObject playerCamera;

    [Header("Effect references")]
    public ParticleSystem muzzleFlash;
    public ParticleSystem hitEffect;
    public TrailRenderer tracerEffect;
    public int fireRate = 20;

    [Header("Hitmarker settings")]
    public UnityEngine.UI.Image hitmarkerImage;
    public float showTime;
    public AudioClip hitSound;
    public AudioClip killSound;
    public AudioSource audioSource;

    
    //     -----  PRIVATE VARIABLES  -----
    private LayerMask layer;
    private float acccumulatedTime = 0;
    private Vector3 hitPoint, normal;

    private void Start()
    {
        
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


        // Hit and trail effect
        RaycastHit[] hits = Physics.RaycastAll(playerCamera.transform.position, playerCamera.transform.forward, 100, whatIsHittable);
        if(hits.Length > 1){
            for (int i = 0; i < hits.Length; i++){
                if (hits[i].transform.gameObject.layer == LayerMask.NameToLayer("Ground")||
                    hits[i].transform.gameObject.layer == LayerMask.NameToLayer("Enemy")||
                    hits[i].transform.gameObject.layer == LayerMask.NameToLayer("Object")
                ){
                    hitPoint = hits[i].point;
                    normal = hits[i].normal;
                    layer = hits[i].transform.gameObject.layer;
                    break;
                }
            }
        }
        else if(hits.Length == 1){
            hitPoint = hits[0].point;
            normal = hits[0].normal;
            layer = hits[0].transform.gameObject.layer;
        }
        else{
            return;
        }

        TrailEffect(hitPoint);
        HitEffect(hitPoint, normal);
        if(layer == LayerMask.NameToLayer("Enemy")) GetHitmarker();
    }

    public void TrailEffect(Vector3 hitPoint){
        TrailRenderer tracer = Instantiate(tracerEffect, muzzleFlash.transform.position, Quaternion.identity);
        tracer.AddPosition(muzzleFlash.transform.position);
        tracer.transform.position = hitPoint;
    }

    public void HitEffect(Vector3 hitPoint, Vector3 normal){
                    hitEffect.transform.position = hitPoint;
                    hitEffect.transform.forward = normal;
                    hitEffect.Emit(1);
    }

    public void GetHitmarker(){
        StopCoroutine("showhitmarker");
        audioSource.PlayOneShot(hitSound);
        hitmarkerImage.enabled = true;
        StartCoroutine("showhitmarker");
    }

    private IEnumerator showhitmarker(){
        yield return new WaitForSeconds(showTime);
        hitmarkerImage.enabled = false;
    }
}