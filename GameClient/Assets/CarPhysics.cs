using System;
using System.Collections.Generic;
using UnityEngine;

class CarPhysics : MonoBehaviour
{
    public Transform frontLeft;
    public Transform frontRight;
    public Transform frontAxle;
    public Transform rearAxle;


    [Header("regular")]
    private Vector3 fDrag; // aerodynamics drag - resistence force
    private Vector3 fRr; // rolling resistance

    public float engineForce = 8000f; // engine force
    
    private float cDrag = 0.4257f; // drag concstant
    private float cRr = 0.4257f * 30; // rolling resistance constant

    public float M = 800f; // cars mass

    public float cBraking; // braking constant
    public bool braking;

    private float g = 10f;
    private float mu = 1.2f; //friction coefficient of the tyre.
                           //For street tyres this may be 1.0,
                           //for racing car tyres this can get as high as 1.5
    private Vector3 fMax; // maximum traction force per wheel
    private float length = 5; // distance between front and rear axle
    private float height = 0.2f; // height of center of mass
    private float c = 2.5f; // distance from center of mass to rear axle
    private float b = 2.5f; // distance from center of mass t front axle
    private float W; // weight of the car

    private float cT; // traction constat, which is the slope of the slip ratio at the orgin of the curve
    private Vector3 slipRatio; // amount of slip

    public Vector3 veloc;
    public float steeringAngle;
    public Rigidbody rb;
    private Vector3 lateralForceFront;
    private Vector3 lateralForceRear;
    private float cA = 0.3f;
    private Vector3 totalResistance;
    private Vector3 forceLong;
    private Vector3 forceLat;
    private Vector3 forceNet;
    private float torque= 0f;
    private Vector3 acceleration ;
    private float angularAcceleration = 0f;
    public float angularVelocity = 0;

    private float alphaFront;
    private float alphaRear;

    float longResistance;
    public float maxSteerAngle = 90f;

    private Vector3 worldVelocity;
    private void Start()
    {
        rb.mass = M;
        W = M * g;

        Debug.Log(rb.inertiaTensor.y);
        Debug.Log(M * (5 * 5 + 2 * 2) / 12);
        Debug.Log(M * (5 * 5 + 2 * 2) / 12 + M * 2.5 * 2.5);
    }

    private void OnGUI()
    {
        GUI.Box(new Rect(35f, 5f, 180f, 25f), $"AlphaFront {(int)alphaFront}");
        GUI.Box(new Rect(35f, 35f, 180f, 25f), $"AlphaRear {(int)alphaRear}");


        GUI.Box(new Rect(35f, 70f, 180f, 25f), $"ForceX {forceLong}");
        GUI.Box(new Rect(35f, 135f, 180f, 25f), $"LateralForceFront {lateralForceFront.x}");
        GUI.Box(new Rect(35f, 165f, 180f, 25f), $"LateralForceRear {lateralForceRear.x}");

        GUI.Box(new Rect(35f, 255f, 180f, 25f), $"ForceY {forceLat}");
        GUI.Box(new Rect(35f, 285f, 180f, 25f), $"rear {lateralForceRear.x}");
        GUI.Box(new Rect(35f, 315f, 180f, 25f), $"front {lateralForceFront.x * Mathf.Cos(steeringAngle * Mathf.Deg2Rad)}");
        GUI.Box(new Rect(35f, 340f, 180f, 25f), $"torque {torque}");
    }

    private void FixedUpdate()
    {
        float vert = Input.GetAxis("Vertical");
        float horz = Input.GetAxis("Horizontal");
        steeringAngle = maxSteerAngle * horz;

        veloc = Quaternion.Euler(0, - transform.rotation.eulerAngles.y, 0) * rb.velocity;
        angularVelocity = rb.angularVelocity.y;
        //Debug.Log(rb.angularVelocity);

        // Rotate wheels
        frontLeft.localRotation = Quaternion.Euler(0, steeringAngle, 90);
        frontRight.localRotation = Quaternion.Euler(0, steeringAngle, 90);

        //alpha represents the slip angle for each wheel
        alphaFront = Vector3.Angle(frontLeft.forward, rb.velocity);
        alphaRear = Vector3.Angle(transform.forward, rb.velocity);

        alphaFront = Mathf.Atan((veloc.x + angularVelocity * b) / Mathf.Abs(veloc.z)) - steeringAngle * Mathf.Deg2Rad * Mathf.Sign(veloc.z);
        alphaRear = Mathf.Atan((veloc.x - angularVelocity * c) / Mathf.Abs(veloc.z));

        //this equation is an approxiamtion of values below the peak shown in the graph
        lateralForceFront = new Vector3(-0.3f * alphaFront, 0f, 0f);
        lateralForceRear = new Vector3(-0.3f * alphaRear, 0f, 0f);


        //These values are supposed to represent the magnitudes of these velocities
        //Vector3 longVelocityFront = Mathf.Cos(alphaFront * Mathf.Deg2Rad) * rb.velocity;
        //Vector3 latVelocityFront = Mathf.Sin(alphaFront * Mathf.Deg2Rad) * rb.velocity;

        //Vector3 longVelocityRear = Mathf.Cos(alphaRear * Mathf.Deg2Rad) * rb.velocity;
        //Vector3 latVelocityRear = Mathf.Sin(alphaRear * Mathf.Deg2Rad) * rb.velocity;

        //float frontSlipAngle = Mathf.Rad2Deg * Mathf.Atan((latVelocityFront.magnitude + (rb.angularVelocity.magnitude * b)) / longVelocityFront.magnitude) - ( steeringAngle);
        //float rearSlipAngle = Mathf.Rad2Deg * Mathf.Atan((latVelocityRear.magnitude - (rb.angularVelocity.magnitude * c)) / longVelocityRear.magnitude);

        //Debug.Log($"{(int)alphaFront}   {(int)frontSlipAngle}");
        //Debug.Log($"{(int)alphaRear}   {(int)rearSlipAngle}");

        //Calculates the lateral force of the wheels
        Vector3 latFrontForce = lateralForceFront.normalized * (M * 10 / 2f) ;
        Vector3 latRearForce = lateralForceRear.normalized * (M * 10 / 2f);

        Vector3 frontTorque = Mathf.Cos(steeringAngle * Mathf.Deg2Rad) * latFrontForce * b;
        Vector3 rearTorque = -latRearForce * c;

        Vector3 corneringForce = latRearForce + Mathf.Cos(steeringAngle * Mathf.Deg2Rad) * latFrontForce;
        Vector3 centripedalForce = corneringForce;
        float steeringRadius = centripedalForce.magnitude / (rb.mass * Mathf.Pow(rb.velocity.magnitude, 2f));

        Vector3 totalTorque = frontTorque + rearTorque;
        Debug.Log($"{(int)totalTorque.magnitude}   {(int)corneringForce.magnitude}");
        rb.AddRelativeTorque(totalTorque.x * Vector3.up);
        rb.AddForce(engineForce * vert * transform.forward);
        rb.AddRelativeForce(corneringForce);
        //rb.AddForce(corneringForce.magnitude * transform.right * Mathf.Sign(corneringForce.z));

        Debug.DrawRay(transform.position, Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0) * veloc, Color.red);
        Debug.DrawRay(transform.position, rb.velocity, Color.red);
        Debug.DrawRay(transform.position, Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0) * totalTorque / M * 5, Color.blue);
        Debug.DrawRay(frontAxle.position, transform.forward * vert * 5, Color.green);

        Debug.DrawRay(frontAxle.position, Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0) * (latFrontForce) / M * 5, Color.yellow);
        Debug.DrawRay(rearAxle.position, Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0) * latRearForce / M * 5, Color.yellow);

        /*
        // Get input
        float vert = Input.GetAxis("Vertical");
        float horz = Input.GetAxis("Horizontal");
        steeringAngle = maxSteerAngle * horz;

        // Rotate wheels
        frontLeft.localRotation = Quaternion.Euler(0, steeringAngle, 90);
        frontRight.localRotation = Quaternion.Euler(0, steeringAngle, 90);

        // Compute slip angles - angle between the tire's heading and its direction of travel.
        alphaFront = Mathf.Atan((veloc.x + angularVelocity * b) / Mathf.Abs(veloc.z)) - steeringAngle * Mathf.Deg2Rad * Mathf.Sign(veloc.z);
        alphaRear = Mathf.Atan((veloc.x - angularVelocity * c) / Mathf.Abs(veloc.z));
        if (Mathf.Abs(veloc.z) <= 0.01f)
        {
            alphaFront = (90 - Mathf.Abs(steeringAngle)) * Mathf.Deg2Rad * Mathf.Sign((veloc.x + angularVelocity * b));
            alphaRear = 90 * Mathf.Deg2Rad * Mathf.Sign((veloc.x - angularVelocity * c));
            alphaFront = 0;
            alphaRear = 0;
            Debug.Log("Very slow.");
        }

        // Convert alpha angles to degrees
        alphaFront *= Mathf.Rad2Deg;
        alphaRear *= Mathf.Rad2Deg;

        // Correct them if they have more than 90 degrees - without it some angles could be 170 degrees etc.
        if (Mathf.Abs(alphaFront) > 90) alphaFront = (Mathf.Abs(alphaFront) - 90f) * (-1) *Mathf.Sign(alphaFront);

        // Compute lateral forces - muliplied by load on tyre
        lateralForceFront.x = - Mathf.Clamp(cA * alphaFront, -1, 1) * (c / length) * W / 4 *0.5f;
        lateralForceRear.x = - Mathf.Clamp(cA * alphaRear, -1, 1) * (b / length) * W / 4*0.5f;

        // compute resistance
        fRr = -cRr * veloc;
        fRr.y = 0;
        fDrag = - cDrag * veloc.magnitude * veloc;
        fDrag.y = 0;
        totalResistance = fRr + fDrag;

        // Sum forces
        longResistance = Mathf.Abs(lateralForceFront.x) * Mathf.Abs(Mathf.Sin(steeringAngle * Mathf.Deg2Rad)) * totalResistance.z;
        forceLong = Vector3.forward * ( engineForce * vert + longResistance);
        forceLat = lateralForceRear + lateralForceFront * Mathf.Abs(Mathf.Cos(steeringAngle * Mathf.Deg2Rad)) * Mathf.Abs(totalResistance.x);
        forceNet = forceLong + forceLat + totalResistance;

        

        // velocity
        acceleration = forceNet / M;
        veloc +=  acceleration * Time.deltaTime;
        //worldVelocity += Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0) * acceleration * Time.deltaTime;
        //veloc = Quaternion.Euler(0, -transform.rotation.eulerAngles.y, 0) * worldVelocity;
        //transform.position += worldVelocity * Time.deltaTime;
        

        // toruqe
        torque = Mathf.Cos(steeringAngle * Mathf.Deg2Rad) * lateralForceFront.x * b - lateralForceRear.x * c;
        angularAcceleration = torque / rb.inertiaTensor.y;
        angularAcceleration = torque / (M * (5f * 5f + 2f * 2f) / 12f + M * 2.5f * 2.5f);
        angularVelocity += Time.deltaTime * angularAcceleration;
        //transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y + angularVelocity * Time.deltaTime * Mathf.Rad2Deg, 0);
    
        rb.AddTorque(torque * Vector3.up, ForceMode.Force);
        rb.AddRelativeForce(forceNet);
        */

        Physics.Simulate(Time.deltaTime);

        //Debug
        //Debug.DrawRay(transform.position, Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0) * forceLong / M * 5, Color.red);
        //Debug.DrawRay(transform.position, Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0) * forceLat / M * 5, Color.blue);
        //Debug.DrawRay(frontAxle.position, Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0) * veloc * 5, Color.green);

        //Debug.DrawRay(frontAxle.position, Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0) * (forceLat - lateralForceRear)/ M * 5, Color.yellow);
        //Debug.DrawRay(rearAxle.position, Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0) * lateralForceRear / M * 5, Color.yellow);
    }





    private void Constants(float coefficientOfFriction, float frontalAreaOfCar, float densityOfAir)
    {
        cDrag = 0.5f * coefficientOfFriction * frontalAreaOfCar * densityOfAir;
        cRr = cDrag * 30f;
    }

    //private void SlipRatio()
    //{
    //    slipRatio = (v / (2 * Mathf.PI * Rw) - v) / v.magnitude;
    //    fLong = cT * slipRatio;
    //    if (fLong.magnitude > fMax.magnitude) fLong = fMax;
    //}

    //private void TorqueOnTheDriveWheels()
    //{
}
