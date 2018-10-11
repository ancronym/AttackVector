using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ShipController : MonoBehaviour {

    public enum ShipType { missile, scout, fighter, bomber, support, capital, civilian } // support and capital has fueling capability
    public ShipType shipType;

    public static float GlobalMaxSpeed = 10f;

	public float mainThrust = 1000f; public float emptyThrottle = 0.25f;
	public float secondaryThrust = 300f;
	public float health = 30f; private float initialHealth;
	public float rotateThrust = 15f;
    public float fuel = 1000000f, fuelPercentage; public bool fuelIsBingo;
    public float initialFuel;
    public float detectability = 1f;  

    PIDController pidController;

    public ParticleSystem deathBlast;	
	public AudioClip playerDeathAudio;    
    public AudioSource audioSource;

    bool mainThrustOn,  thrustBon,   leftThrustOn,  rightThrustOn;

    // Thruster controllers:
	Nozzle mainThruster;	Nozzle frontThruster1;	Nozzle frontThruster2;	Nozzle frontLeftThruster;
    Nozzle frontRightThruster;    Nozzle backLeftThruster;	Nozzle backRightThruster;
    private float thrusterThreshold = 0.2f;

	LineRenderer headingLine;

    // Weapon parameters: Obsolete mostly!?
    /*
    bool leftSide = false; float firingSide; float dispersion; public float plasmaDispersion = 0.1f;
    public float plasmaSpeed = 10f;    public float railSlugSpeed = 20f;
    public float plasmaROF = 0.1f;    public float railROF = 1f;    public float missileROF = 2f;
    float railReload = 0f;    float missileReload = 0f;
    */
        
    public List<GameObject> weapons; public int weaponNr;
    public GameObject activeWeapon;

    public RadarController radar;
    public PlayerController owner;
    public VesselAI ownerAI;

    float desiredHeading = 0f;

    // Use this for initialization
    void Start () {

        mainThruster = gameObject.transform.Find ("Thrusters/MainThruster").GetComponent<Nozzle> ();        
        frontLeftThruster = gameObject.transform.Find ("Thrusters/FrontLeftThruster").GetComponent<Nozzle> ();
		frontRightThruster = gameObject.transform.Find ("Thrusters/FrontRightThruster").GetComponent<Nozzle> ();
		backLeftThruster = gameObject.transform.Find ("Thrusters/BackLeftThruster").GetComponent<Nozzle> ();
		backRightThruster = gameObject.transform.Find ("Thrusters/BackRightThruster").GetComponent<Nozzle> ();
		frontThruster1 = gameObject.transform.Find ("Thrusters/FrontThruster1").GetComponent<Nozzle> ();
		frontThruster2 = gameObject.transform.Find ("Thrusters/FrontThruster2").GetComponent<Nozzle> ();
        
        if (weapons.Count != 0)
        {
            activeWeapon = weapons[0]; weaponNr = 1;
        } else { weaponNr = 0; }        

		pidController = gameObject.GetComponent<PIDController> ();

        if (gameObject.tag == "Vessel")
        {
            StartCoroutine(ClearSpaceAroundShip(false, 0.1f));
        }   
    

        initialFuel = fuel; fuelIsBingo = false;
        initialHealth = health;
    }

    

    IEnumerator ClearSpaceAroundShip(bool status, float delaytime) {

        yield return new WaitForSeconds(delaytime);
        SpaceClearer.ClearScenery(gameObject.transform.position, 10f);
    }

	
	// Update is called once per frame
	void Update () {
        /*railReload -= Time.deltaTime;
        missileReload -= Time.deltaTime;*/

        fuelPercentage = fuel / initialFuel * 100f;
        if(fuelPercentage < 20) { fuelIsBingo = true; }

        Vector2 velocity = gameObject.GetComponent<Rigidbody2D>().velocity;

        if (shipType != ShipType.missile)
        {                       
            if (shipType == ShipType.scout)
            {
                if (velocity.sqrMagnitude > GlobalMaxSpeed * GlobalMaxSpeed * 2)
                {                    
                    gameObject.GetComponent<Rigidbody2D>().velocity =
                        velocity.normalized * GlobalMaxSpeed;
                }
            }
            else
            {
                if (velocity.sqrMagnitude > GlobalMaxSpeed * GlobalMaxSpeed )
                {
                    gameObject.GetComponent<Rigidbody2D>().velocity =
                        velocity.normalized * GlobalMaxSpeed;
                }
            }
        }
        else
        {
            /*
            if (velocity.sqrMagnitude > GlobalMaxSpeed * GlobalMaxSpeed * 3)
            {
                gameObject.GetComponent<Rigidbody2D>().velocity =
                    velocity.normalized * GlobalMaxSpeed;
            }
            */
        }
    }

	void OnCollisionEnter2D(Collision2D collision){
		ProjectileController projectile = collision.gameObject.GetComponent<ProjectileController> ();

		if (projectile && projectile.gameObject.tag == "Projectile") {
			health -= projectile.projectileDamage;
            projectile.Hit(health, gameObject);
        }
        // the impulse is taken from health for damage
        if (    collision.gameObject.tag == "Projectile"
            ||  collision.gameObject.tag == "Vessel"
            ||  collision.gameObject.tag == "Scenery")        {
            health -= collision.relativeVelocity.magnitude * collision.gameObject.GetComponent<Rigidbody2D>().mass / 20;
        }

		if (health <= 0f) {
			Die();
		}

        if(collision.gameObject.tag == "CivWp")
        {
            Destroy(collision.gameObject);
        }
        
	}

    public void AddDamage(float damage)
    {
        health -= damage;

        if (health <= 0f)
        {
            Debug.Log("Damage added: " + damage + " Die called");
            Die();
        }
    }

    public void FireWeapon(bool firing)
    {
        if (!activeWeapon) { Debug.Log("No active weapon"); return; }

        if (firing)
        {
            activeWeapon.GetComponent<WepContr>().Fire(true);
        }
        else
        {
            activeWeapon.GetComponent<WepContr>().Fire(false);
        }
    }

    public float GetProjectileSpeed()
    {
        return activeWeapon.GetComponent<WepContr>().projectileSpeed;
    }

    public float GetProjectileRange()
    {
        float range = activeWeapon.GetComponent<WepContr>().range;
            
        return range;
    }

    public int GetAmmo()
    {
        return activeWeapon.GetComponent<WepContr>().ammo;
    }


    // Todo make list sorted
    public List<Vector3> GetWepRangesAmmoSortedList()
    {
        List<Vector3> wepRangeAmmo = new List<Vector3>(weapons.Count);
        
        for (int i = 0; i < weapons.Count; i++)
        {
            wepRangeAmmo.Add(new Vector3(
                (float)i,
                weapons[i].GetComponent<WepContr>().range,
                weapons[i].GetComponent<WepContr>().ammo
                ));
        }



        return wepRangeAmmo;
    }

    public void ReportKill(GameObject target)
    {
        if(radar.target == target)
        {
            radar.target = null;
        }

        if(owner != null)
        {
            owner.ReportKill(target);
        } else if(owner == null && ownerAI != null)
        {
            ownerAI.ReportKill(target);
        }
    }

    public string SelectNextWeapon()
    {
        FireWeapon(false);

        if(weapons.Count == 0) { return "No Weapon"; }

        if (weaponNr >= weapons.Count)
        {
            activeWeapon = weapons[0]; weaponNr = 1;
        }
        else
        {
            weaponNr++;
            activeWeapon = weapons[weaponNr - 1];
        }

        string name = activeWeapon.GetComponent<WepContr>().weaponName;

        if(activeWeapon.GetComponent<WepContr>().projectileType == WepContr.ProjectileType.missile)
        {

            name = name + " " + activeWeapon.GetComponent<WepContr>().missileLaunchMode + GetAmmo();
            
        }
        return name;
    }	

    public void SelectWeaponByNumber(int weaponNumber)
    {
        if (weaponNumber != weaponNr)
        {

            switch (weaponNumber)
            {
                case 1:
                    if (weapons.Count != 0)
                    {
                        weaponNr = weaponNumber;
                        activeWeapon = weapons[weaponNumber - 1];
                    }
                    break;
                case 2:
                    if (weapons.Count > 1)
                    {
                        weaponNr = weaponNumber;
                        activeWeapon = weapons[weaponNumber - 1];
                    }
                    break;
                case 3:
                    if (weapons.Count > 2)
                    {
                        weaponNr = weaponNumber;
                        activeWeapon = weapons[weaponNumber - 1];
                    }
                    break;
                case 4:
                    if (weapons.Count > 3)
                    {
                        weaponNr = weaponNumber;
                        activeWeapon = weapons[weaponNumber - 1];
                    }
                    break;
            }
        } else if (weaponNumber == weaponNr)
        {
            if(activeWeapon.GetComponent<WepContr>().projectileType == WepContr.ProjectileType.missile)
            {
                activeWeapon.GetComponent<WepContr>().ToggleFireMode();
            }
        }       
    }

    public string GetWeaponName()
    {
        return activeWeapon.GetComponent<WepContr>().weaponName;
    }

	public void Rotate(float desiredHeading,float deltaTime){        

        Rigidbody2D ship = gameObject.GetComponent<Rigidbody2D> ();
		float currentHeading = gameObject.transform.eulerAngles.z;

        // If the correction is too small, exit method
        if (Mathf.Abs(currentHeading - desiredHeading) < 0.01f) { return; }

		float DV = 0f;
		float CV = 0f;

		// Fixes The desired heading between 0 and 360 degrees
		if (desiredHeading < 0) {				desiredHeading = desiredHeading + 360;		}
		if (desiredHeading >= 360) {		desiredHeading = desiredHeading - 360;		}


		if (desiredHeading > currentHeading && (desiredHeading - currentHeading) > 180) {
			// right turn
			DV = -(currentHeading + (360 - desiredHeading));
		} else if (desiredHeading < currentHeading && (currentHeading - desiredHeading) > 180) {
			// left turn
			DV = (desiredHeading + (360 - currentHeading));
		}else if(desiredHeading > currentHeading){
			// left turn
			DV = desiredHeading - currentHeading;
		}else if(desiredHeading <currentHeading){
			// right turn
			DV = desiredHeading - currentHeading;
		}

		float correction = pidController.Correction(DV,CV,deltaTime);
		// Debug.Log ("Correction: " + correction);
		// Debug.Log("S.Desired: " + desiredHeading + "S.Current: " +currentHeading);

        /*
		if (correction > 0.5f) {
			backLeftThruster.EmitThrust ();
			frontRightThruster.EmitThrust ();
		} else if (correction < -0.5f) {
			backRightThruster.EmitThrust ();
			frontLeftThruster.EmitThrust ();
		}
        */
		float thrust = Mathf.Clamp (correction, -rotateThrust, rotateThrust);
        if (fuel <= 0) { thrust = thrust / 3; }
		ship.AddTorque (thrust);
        fuel -= Mathf.Abs(thrust);
	}

    // Throttle should be 0 to 2, but get's clamped anyway!
    public void ThrustForward(float throttle) {

        if (fuel <= 0f) { throttle = emptyThrottle; }

        if(throttle > thrusterThreshold) { mainThruster.EmitThrust(throttle); }

        throttle = Mathf.Clamp(throttle, 0f, 2f);
        float speedRatio = Time.deltaTime * mainThrust* throttle;
        gameObject.GetComponent<Rigidbody2D>().AddRelativeForce(new Vector2 (0f,speedRatio));

        DeductFuel(speedRatio, throttle);

        gameObject.GetComponent<Rigidbody2D>().mass -= (speedRatio/100000);        
    }

    public void ThrustBackward(float throttle){
        if (fuel <= 0f) { throttle = emptyThrottle; }
        throttle = Mathf.Clamp(throttle, 0f, 2f);
        float speedRatio = Time.deltaTime * secondaryThrust * throttle;
		this.GetComponent<Rigidbody2D>().AddRelativeForce(new Vector2 (0f,-speedRatio));
        if (throttle > thrusterThreshold)
        {
            frontThruster1.EmitThrust(throttle);
            frontThruster2.EmitThrust(throttle);
        }

        DeductFuel(speedRatio, throttle);

        this.GetComponent<Rigidbody2D>().mass -= (speedRatio / 100000);
    }

    public void ThrustLeft(float throttle){
        if (fuel <= 0f) { throttle = emptyThrottle; }
        throttle = Mathf.Clamp(throttle, 0f, 2f);

        float speedRatio = Time.deltaTime * secondaryThrust * throttle;
		this.GetComponent<Rigidbody2D>().AddRelativeForce (new Vector2 (-speedRatio,0f));

        if (throttle > thrusterThreshold)
        {
            backRightThruster.EmitThrust(throttle);
            frontRightThruster.EmitThrust(throttle);
        }

        DeductFuel(speedRatio, throttle);

        this.GetComponent<Rigidbody2D>().mass -= (speedRatio / 100000);
    }

    public void ThrustRight(float throttle){
        if (fuel <= 0f) { throttle = emptyThrottle; }

        throttle = Mathf.Clamp(throttle, 0f, 2f);
        float speedRatio = Time.deltaTime * secondaryThrust * throttle;
		this.GetComponent<Rigidbody2D>().AddRelativeForce(new Vector2(speedRatio,0f));

        if (throttle > thrusterThreshold)
        {
            backLeftThruster.EmitThrust(throttle);
            frontLeftThruster.EmitThrust(throttle);
        }

        DeductFuel(speedRatio, throttle);

        this.GetComponent<Rigidbody2D>().mass -= (speedRatio / 100000);
    }

    public void Stop()
    {
        Vector2 velocity = gameObject.transform.InverseTransformVector(
            gameObject.GetComponent<Rigidbody2D>().velocity
            );
        if(velocity.x < -0.01f)
        {
            ThrustRight(Mathf.Abs(velocity.x) / GlobalMaxSpeed * 2f);
        } else if (velocity.x > 0.01f)
        {
            ThrustLeft(velocity.x / GlobalMaxSpeed * 2f);
        }

        if (velocity.y < -0.01f)
        {
            ThrustForward(Mathf.Abs(velocity.y) / GlobalMaxSpeed * 2f);
        }
        else if (velocity.y > 0.01f)
        {
            ThrustBackward(velocity.y / GlobalMaxSpeed * 2f);
        }
    }

    private void DeductFuel(float ratio, float throttle)
    {
        // Use more fuel when throttle above 1
        if (throttle > 0 && throttle <= 1)
        {
            fuel -= ratio;
        }
        else
        {
            fuel -= ratio * throttle;
        }
    }

    public List<RadarController.Bogie> ReportBogies() 
    {       
        return radar.Getbogies();
    }

    public float GetHealth()
    {
        return health / initialHealth;
    }    

    void Die(){
		AudioSource.PlayClipAtPoint(playerDeathAudio,transform.position,0.8f);

		ParticleSystem blast = Instantiate (deathBlast, gameObject.transform.position, Quaternion.identity) as ParticleSystem;
        float x = Random.Range(-170f, 170f);
        float y = Random.Range(-170f, 170f);
        float z = Random.Range(-170f, 170f);

        blast.gameObject.transform.eulerAngles = new Vector3(x, y, z);

        // Destroy(gameObject);

        if (gameObject.GetComponent<PlayerController>())
        {
            gameObject.GetComponent<PlayerController>().Death();
        }
        else if (gameObject.GetComponent<VesselAI>())
        {
            gameObject.GetComponent<VesselAI>().Death();            
        }        
        else {
            Destroy(gameObject, 0.1f);
        }
	}

    public void SelfDestruct() {
        Die();
    }

    public void ToggleRadar() {
        radar.ToggleRadar();
    }

    public void SetNearestTarget() {
        radar.GetNearestTarget();
    }

    public void SetBoreTarget()
    {
        radar.GetBoresightTarget();
    }

    public RadarController.RadarState GetRadarState() {
        return radar.radarState;
    }

   

}
