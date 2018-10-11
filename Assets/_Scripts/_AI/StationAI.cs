using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StationAI : MonoBehaviour {

    public ParticleSystem deathBlast;
    public AudioClip playerDeathAudio;
    public AudioSource audioSource;

    public RadarController radar;
    // For incoming locks
    List<GameObject> lockSources = new List<GameObject>(50);

    public float health = 30f; private float initialHealth;
    public float detectability = 2f;

    public TeamManager.TeamSide teamSide;

    //Communications:
    public TeamManager teamManager;

    // Use this for initialization
    void Start () {
        initialHealth = health;
        StartCoroutine(ClearSpaceAroundShip(false, 0.1f));

        radar.SetRadarOn();
        radar.teamSide = teamSide;

        radar.GetComponent<CapsuleCollider2D>().offset = new Vector2(0f, 0f);
        radar.GetComponent<CapsuleCollider2D>().size = new Vector2(200f, 200f);
    }
	
	// Update is called once per frame
	void Update () {
		
	}

    IEnumerator ClearSpaceAroundShip(bool status, float delaytime)
    {

        yield return new WaitForSeconds(delaytime);
        SpaceClearer.ClearScenery(gameObject.transform.position, 10f);
    }



    void OnCollisionEnter2D(Collision2D collision)
    {
        ProjectileController projectile = collision.gameObject.GetComponent<ProjectileController>();

        if (projectile && projectile.gameObject.tag == "Projectile")
        {
            health -= projectile.projectileDamage;
            projectile.Hit(health, gameObject);
        }
        // the impulse is taken from health for damage
        if (collision.gameObject.tag == "Projectile"
            || collision.gameObject.tag == "Vessel"
            || collision.gameObject.tag == "Scenery")
        {
            health -= collision.relativeVelocity.magnitude * collision.gameObject.GetComponent<Rigidbody2D>().mass / 20;
        }

        if (health <= 0f)
        {
            Die();
        }

    }

    void Die()
    {
        AudioSource.PlayClipAtPoint(playerDeathAudio, transform.position, 0.8f);

        ParticleSystem blast = Instantiate(deathBlast, gameObject.transform.position, Quaternion.identity) as ParticleSystem;
        float x = Random.Range(-170f, 170f);
        float y = Random.Range(-170f, 170f);
        float z = Random.Range(-170f, 170f);

        blast.gameObject.transform.eulerAngles = new Vector3(x, y, z);

        // Destroy(gameObject);
        Destroy(gameObject, 0.1f);
        
    }

    #region Communications

    public void IncomingLock(GameObject LockSource)
    {
        lockSources.Add(LockSource);
    }

    public List<RadarController.Bogie> ReportBogies()
    {
        return radar.Getbogies();
    }

    public float GetHealth()
    {
        return health / initialHealth;
    }

    public void ReportKill(GameObject target)
    {
        if (radar.target == target)
        {
            radar.target = null;
        }

        if (teamManager != null)
        {
            teamManager.ReportKill(target);
        }
    }

    #endregion
}
