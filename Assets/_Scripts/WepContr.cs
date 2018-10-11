using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WepContr : MonoBehaviour {

    public enum ProjectileType { projectile, missile};
    public ProjectileType projectileType;

    public enum MissileMode { Scout, Mine };
    public MissileMode missileLaunchMode;

    public enum WeapCat { closeRange, Ranged, missile}
    public WeapCat weaponCategory;

    public GameObject projectilePrefab;

    public string weaponName;
    
    public bool repeatingWeapon; public float ROF = 0.1f; public float cooldown = 1f; float fireTime;
    public float projectileSpeed, range, lifetime;
    public float weaponDispersion = 0f;    
    public bool usesAmmo; public int ammo;

    public enum Barrels { single, dual }
    public Barrels barrels;
    public float barrelOffset = 0f;
    bool leftSide; float firingSide = 0f;

    public AudioClip weaponAudio; public float volume;  

    public ShipController owningShip;
    public GameObject owningObject;

	void Start () {

        owningObject = gameObject.transform.parent.gameObject;
        
        owningShip = owningObject.GetComponent<ShipController>();

        fireTime = Time.timeSinceLevelLoad;
        if(projectileType != ProjectileType.missile)
        {
            range = projectileSpeed * lifetime;
        }
        else
        {
            range = 10000000f;
        }
        
        
    }

    void OnDrawGizmos()
    {
            Gizmos.DrawWireSphere(transform.position, 0.1f);
    }


    void Update () {
		
	}

    public void Fire(bool firing)
    {
        if (repeatingWeapon)
        {
            if (firing)
            {
                InvokeRepeating("FireWeapon", 0.0001f, ROF);
            }
            else
            {
                CancelInvoke("FireWeapon");
            }
        } else if(fireTime < Time.timeSinceLevelLoad - cooldown)
        {
            FireWeapon();
            fireTime = Time.timeSinceLevelLoad;
        }
    }

    void FireWeapon()   {


        if (usesAmmo)
        {            
            if(ammo <= 0) { ammo = 0; return; }
            ammo--;
        }

        switch (projectileType) {

            case ProjectileType.projectile:
                //Siwtching firing side:
                if (barrels == Barrels.dual)
                {
                    if (leftSide)
                    {
                        firingSide = barrelOffset;
                        leftSide = false;
                    }
                    else
                    {
                        firingSide = -barrelOffset;
                        leftSide = true;
                    }
                }

                // setting shot dispersion:
                float dispersion = Random.Range(-weaponDispersion, weaponDispersion);

                GameObject projectile = Instantiate(projectilePrefab, owningObject.transform.position, Quaternion.identity) as GameObject;
                ReportShot();
                projectile.GetComponent<ProjectileController>().owner = gameObject;             
                                           
                float newZ = owningObject.transform.eulerAngles.z;
                projectile.transform.eulerAngles = new Vector3(0f, 0f, newZ);

                //The Green vector
                Vector3 forwardUnit = owningObject.transform.up.normalized;
                projectile.transform.parent = gameObject.transform;
                projectile.transform.localPosition =  new Vector3(firingSide, 0f, 0f);
                projectile.transform.parent = projectile.transform;                              

                if (projectile)
                {
                    float boltX = owningObject.GetComponent<Rigidbody2D>().velocity.x + forwardUnit.x * projectileSpeed;
                    float boltY = owningObject.GetComponent<Rigidbody2D>().velocity.y + forwardUnit.y * projectileSpeed;
                    projectile.GetComponent<Rigidbody2D>().velocity = new Vector2(boltX + dispersion, boltY + dispersion);

                    AudioSource.PlayClipAtPoint(weaponAudio, gameObject.transform.position, volume);
                    // Debug.Log ("Fire!");
                }
                break;


            case ProjectileType.missile:

                GameObject missile = Instantiate(projectilePrefab, gameObject.transform.position, Quaternion.identity) as GameObject;
                ReportShot();
                MissileController controller = missile.GetComponent<MissileController>();

                float Z = gameObject.transform.eulerAngles.z;
                missile.transform.eulerAngles = new Vector3(0f, 0f, Z);

                missile.transform.parent = missile.transform;

                // setting up missile's radar and target                
                missile.GetComponentInChildren<RadarController>().teamSide = owningShip.radar.teamSide;
                controller.desiredHeading = Z;

                // Adding ship velocity to missile
                Vector3 forward = gameObject.transform.up.normalized;
                float missileX = owningObject.GetComponent<Rigidbody2D>().velocity.x + forward.x * projectileSpeed;
                float missileY = owningObject.GetComponent<Rigidbody2D>().velocity.y + forward.y * projectileSpeed;
                missile.GetComponent<Rigidbody2D>().velocity = new Vector2(missileX, missileY);

                if (owningShip.radar.target != null)
                {
                    missile.GetComponentInChildren<RadarController>().target = owningShip.radar.target;
                    controller.targetObject = owningShip.radar.target;
                    controller.intendedState = MissileController.MissileState.locked;
                }
                else
                {
                    switch (missileLaunchMode)
                    {
                        case MissileMode.Mine:
                            controller.intendedState = MissileController.MissileState.mine;
                            break;
                        case MissileMode.Scout:
                            controller.intendedState = MissileController.MissileState.scouting;
                            break;
                    }
                    
                }
                controller.parentShip = owningObject.transform;
                missile.GetComponent<ProjectileController>().owner = gameObject;
                missile.GetComponent<MissileController>().owner = gameObject.GetComponent<WepContr>();

                AudioSource.PlayClipAtPoint(weaponAudio, gameObject.transform.position, volume);
                break;
        }
    }

    public void ToggleFireMode()
    {
        switch (projectileType)
        {
            case ProjectileType.projectile:
                // Feature creep, do not implement yet! You crazy bastard.
                break;
            case ProjectileType.missile:
                if(missileLaunchMode == MissileMode.Mine) { missileLaunchMode = MissileMode.Scout; }
                else if (missileLaunchMode == MissileMode.Scout) { missileLaunchMode = MissileMode.Mine; }
                break;
        }

    }

    public void ReportKill(GameObject target)
    {
        bool validkill = false;

        if(owningObject.GetComponent<VesselAI>() != null)
        {
            if(owningObject.GetComponent<VesselAI>().teamSide != target.GetComponent<VesselAI>().teamSide)
            {
                validkill = true;
            }
        } else if (owningObject.GetComponent<PlayerController>() != null)
        {
            if (owningObject.GetComponent<PlayerController>().teamSide != target.GetComponent<VesselAI>().teamSide)
            {
                validkill = true;
            }
        }

        if (validkill)
        {
            owningShip.ReportKill(target);
        }
        
    }
  

    public void ReportBogies(List<RadarController.Bogie> bogies)
    {
        if (owningObject.GetComponent<VesselAI>() != null)
        {
            owningObject.GetComponent<VesselAI>().ReportBogiesToTM(bogies);
        }
        else if (owningObject.GetComponent<PlayerController>() != null)
        {
            owningObject.GetComponent<PlayerController>().ReportBogiesToTM(bogies);
        }
    }

    public void ReportHit()
    {
        if (owningObject.GetComponent<VesselAI>() != null)
        {
           
        }
        else if (owningObject.GetComponent<PlayerController>() != null)
        {
            owningObject.GetComponent<PlayerController>().ReportHit();
        }
    }

    private void ReportShot()
    {
        if (owningObject.GetComponent<VesselAI>() != null)
        {

        }
        else if (owningObject.GetComponent<PlayerController>() != null)
        {
            owningObject.GetComponent<PlayerController>().ReportShot();
        }
    }

}

