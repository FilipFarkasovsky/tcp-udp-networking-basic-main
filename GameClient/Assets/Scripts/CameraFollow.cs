using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{

    public Transform FollowTarget;
    public Vector3 TargetOffset;
    public float MoveSpeed = 2f;

    private Transform _myTransform;


    private void Awake()
    {
        _myTransform = transform;
        StartCoroutine(FindPlayer());
    }

    private void LateUpdate()
    {
        if (FollowTarget != null)
            transform.position = FollowTarget.position;
            //_myTransform.position = Vector3.Lerp(_myTransform.position, FollowTarget.position + TargetOffset, MoveSpeed * Time.deltaTime);
    }

    IEnumerator FindPlayer()
    {
        GameObject target = GameObject.FindGameObjectWithTag("Player");
        if (target != null)
        {
            FollowTarget = target.transform;
        }

        yield return new WaitForSeconds(2f);
        if (FollowTarget == null)
            StartCoroutine(FindPlayer());
    }
}