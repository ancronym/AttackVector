using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MissileController : MonoBehaviour {

    // Debugging stuff
    bool debug;
    public TextMesh statusText;
    
    public GameObject targetObject;
    public float armingSQRDistance = 4f;    
    float SQRdistanceToOwner = 0f; float sqrToTarget, minSqrToTarget;
    float damageRadius;
    public float initialSpeed = 4f;
    public float scoutSpeed = 20f;
    public float initialFuel = 1000f;
    public float constantAcceleration = 1f;

    // missile guidance parameters
    public float desiredHeading;
    float mass;
    Vector2 desiredVector;

    public enum MissileState { disarmed, locked, scouting, mine};
    MissileState missileState;

    float targetTimeoutDelay = 1f;

    public ShipController missile;
    public WepContr owner;
    public LayerMask damageMask;

    public AudioClip missileArmed;
    bool stateChanging = false;

    // this is set by the launching vessel:
    public Transform parentShip;
    public MissileState intendedState;
    
	// Use this for initialization
	void Start () {

        debug = SettingsManager.GetDebug() == 1;
        if (!debug) { statusText.text = ""; }

        stateChanging = false;
        missileState = MissileState.disarmed;

        damageRadius = Mathf.Sqrt(armingSQRDistance);
        
        mass = gameObject.GetComponent<Rigidbody2D>().mass;
        gameObject.GetComponent<BoxCollider2D>().enabled = false;
        missile = GetComponent<ShipController>();
        missile.fuel = initialFuel;

        InvokeRepeating("TargetCheck", 1f, 1f);
        
    }
	
	// Update is called once per frame
	void Update () {

        

        // Missile state flow:
        switch (missileState)
        {
            case MissileState.disarmed:
                missile.radar.SetRadarOn();
                if (debug) { statusText.text = "DA"; }

                missile.ThrustForward(1f); // to clear the player

                SQRdistanceToOwner = Vector2.SqrMagnitude(gameObject.transform.position - parentShip.position);

                if (SQRdistanceToOwner > armingSQRDistance)
                {
                    stateChanging = true;  

                    if (!targetObject)
                    {
                        

                        if(intendedState == MissileState.locked)
                        {
                            missileState = MissileState.scouting;
                        }
                        else { 
                        missileState = intendedState;
                        }
                        gameObject.GetComponent<BoxCollider2D>().enabled = true;
                    }
                    else
                    {
                        // InitialLockedManeuver();
                        missileState = MissileState.locked;
                        gameObject.GetComponent<BoxCollider2D>().enabled = true;
                    }
                }

                break;
            case MissileState.locked:
                if (debug) { statusText.text = "LCK"; }

                if (stateChanging)
                {
                    AudioSource.PlayClipAtPoint(missileArmed, gameObject.transform.position, 0.8f);
                    
                    stateChanging = false;
                }

                Pursue();

                break;

            case MissileState.scouting:
                if (debug) { statusText.text = "SCT"; }
                if (stateChanging)
                {
                    AudioSource.PlayClipAtPoint(missileArmed, gameObject.transform.position, 0.8f);
                    gameObject.GetComponent<BoxCollider2D>().enabled = true;
                    stateChanging = false;
                }

                Scout();
                break;
            case MissileState.mine:
                if (debug) { statusText.text = "MN"; }

                Loiter();

                break;
        }



        

        if (missile.fuel < 0 && targetObject == null) {
            missile.SelfDestruct();
        }

        if (debug) { statusText.text = statusText.text + " R: " + missile.radar.radarState + " F: " + missile.fuel; }
        

    }    

    void TargetCheck()
    {
        if(targetObject != null)
        {
            if(missile.radar.target == null)
            {
                targetObject = null;
            }
            else
            {
                targetObject = missile.radar.target;
            }            
        }
        else
        {
            targetObject = missile.radar.GetNearestTarget();
        }

        if(missile.radar.radarBogies.Count != 0)
        {
            ReportBogies();
        }
    }

    void ReportBogies()
    {
        if (owner != null)
        {
            owner.ReportBogies(missile.radar.radarBogies);
        }
    }

    void LateralCorrection() {
        Vector3 velocity = new Vector3(gameObject.GetComponent<Rigidbody2D>().velocity.x, gameObject.GetComponent<Rigidbody2D>().velocity.y, 0f);

        Vector3 crossProduct = Vector3.Cross(gameObject.transform.up, velocity);
        // Debug.Log("Cross: " + crossProduct.z);

        
        if (crossProduct.z > 0.2f)
        {
            missile.ThrustRight(1f);
        }
        else if (crossProduct.z < -0.2f)
        {
            missile.ThrustLeft(1f);
        }    
    }

    void Pursue(){
        // check for radar lock
        

        if (targetObject)
        {
            sqrToTarget = (gameObject.GetComponent<Rigidbody2D>().position - targetObject.GetComponent<Rigidbody2D>().position).sqrMagnitude;

            if(sqrToTarget < armingSQRDistance * armingSQRDistance)
            {
                if(sqrToTarget > minSqrToTarget)
                {
                    Detonate();
                }                
            }
            

            if (debug) { statusText.text = statusText.text + " " + targetObject.name; }

            Vector2 killBurn = RAMP.GetHitBurn(targetObject.transform, gameObject.transform, missile.fuel, mass);
            desiredHeading = killBurn.x;
            LateralCorrection();
            missile.Rotate(desiredHeading, Time.deltaTime);

            if (Mathf.Abs(desiredHeading - missile.transform.eulerAngles.z) < 0.5f)
            {
                ExecuteThrust(killBurn.y);
            }
        } else
        {
            stateChanging = true;
            missileState = MissileState.mine;
        }

        minSqrToTarget = sqrToTarget;
    }

    void Scout()
    {
        missile.Rotate(desiredHeading, Time.deltaTime);
        LateralCorrection();

        if (transform.InverseTransformVector(gameObject.GetComponent<Rigidbody2D>().velocity).y > scoutSpeed + 0.5f)
        {
            missile.ThrustBackward(0.33f);
        } else if (transform.InverseTransformVector(gameObject.GetComponent<Rigidbody2D>().velocity).y < scoutSpeed - 0.5f)  {
            missile.ThrustForward(0.33f);
        }

        



        // Exit scout when target found by radar
        if (targetObject != null)
        {
            if (targetObject.GetComponent<ShipController>() != null)
            {

                if (targetObject.GetComponent<ShipController>().radar.teamSide != missile.radar.teamSide)
                {

                    missileState = MissileState.locked;
                    stateChanging = true;
                }
            }
        }
    }

    void Loiter()
    {
        LateralCorrection();

        if (transform.InverseTransformVector(gameObject.GetComponent<Rigidbody2D>().velocity).y > 0.01f)
        {
            missile.ThrustBackward(0.2f);
        }
        else if (transform.InverseTransformVector(gameObject.GetComponent<Rigidbody2D>().velocity).y < -0.05f)
        {
            missile.ThrustForward(0.2f);
        }

        desiredHeading = desiredHeading + 50 * Time.deltaTime;
        if(desiredHeading > 360)
        {
            desiredHeading = 0;
        }
        missile.Rotate(desiredHeading, Time.deltaTime);

        if (targetObject != null)
        {
           // Debug.Log("Target found");

            if (targetObject.GetComponent<ShipController>() != null)
            {

                if (targetObject.GetComponent<ShipController>().radar.teamSide != missile.radar.teamSide)
                {

                    missileState = MissileState.locked;
                    stateChanging = true;
                }
            }
        }
    }

    void ExecuteThrust(float dV) {
        float targetFuel = missile.fuel - dV * gameObject.GetComponent<Rigidbody2D>().mass;

        if( missile.fuel > targetFuel) {             
            missile.ThrustForward(1f);
            if (Input.GetKeyDown(KeyCode.Escape)) { return; }
        }

    }   

    void Detonate()
    {
        float distance = 0f, damage = 0f;
        Vector2 vToTarget = new Vector2(0f,0f);
        CircleCollider2D circle =  gameObject.AddComponent<CircleCollider2D>() as CircleCollider2D;
        circle.isTrigger = true;

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(damageMask);
        Collider2D[] contacts = new Collider2D[10];

        int nrOfContacts = circle.OverlapCollider(filter, contacts);

        for (int i = 0; i < nrOfContacts; i++)
        {
            if (contacts[i].gameObject.GetComponent<ShipController>())
            {
                vToTarget = (contacts[i].gameObject.GetComponent<Rigidbody2D>().position - gameObject.GetComponent<Rigidbody2D>().position);
                distance = vToTarget.magnitude;
                damage = ((damageRadius - distance) / damageRadius) * gameObject.GetComponent<ProjectileController>().projectileDamage;
                Vector2 force = vToTarget * ((damageRadius - distance) );

                contacts[i].gameObject.GetComponent<Rigidbody2D>().AddForce(force);
                contacts[i].gameObject.GetComponent<ShipController>().AddDamage(damage);

            } else if (contacts[i].gameObject.GetComponent<AsteroidScript>())
            {
                vToTarget = (contacts[i].gameObject.GetComponent<Rigidbody2D>().position - gameObject.GetComponent<Rigidbody2D>().position);
                distance = vToTarget.magnitude;
                damage = (damageRadius - distance) * gameObject.GetComponent<ProjectileController>().projectileDamage;
                Vector2 force = vToTarget * (damageRadius - distance);

                contacts[i].gameObject.GetComponent<AsteroidScript>().AddDamage(damage);
                contacts[i].gameObject.GetComponent<Rigidbody2D>().AddForce(force);
            } 
        }

        missile.SelfDestruct();
    }
}
