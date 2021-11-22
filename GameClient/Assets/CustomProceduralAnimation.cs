using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomProceduralAnimation : MonoBehaviour
{
    private Vector3 lastPosition;
    private Transform surveyorWheel;  

    public Animator animator;
    public float wheelRadius; // 
    public float stepDistance;  // Angle for steps

    [Header("Just for spectating")]
    public float angleCounter;
    public float turnAngle;

    void Start()
    {
        lastPosition = new Vector3(transform.position.x, 0F, transform.position.z);
        surveyorWheel = transform;
    }

    void FixedUpdate()
    {
        lastPosition = new Vector3(lastPosition.x, 0F, lastPosition.z);
        Vector3 currPosition = new Vector3(transform.position.x, 0F, transform.position.z);

        float dist = Vector3.Distance(lastPosition, currPosition);
        turnAngle = (dist / (2 * Mathf.PI * wheelRadius)) * 360F;

        //For visualization. Attach to Transform.
        surveyorWheel.Rotate(new Vector3(0F, -turnAngle, 0F));

        angleCounter += turnAngle;

        if (angleCounter > stepDistance)
            angleCounter = 0F;

        animator.SetFloat("runstage", (angleCounter / stepDistance));

        if (animator.GetFloat("runstage") > 1F)
            animator.SetFloat("runstage", 0);

        lastPosition = currPosition;
    }
}
