using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AsteroidScript : MonoBehaviour {

    public float health;
    public int rubbleAmount;
    public float rubbleSpeed;
    public float rubbleSpeedVariance;
    public float spawnSpeed;
    public float spawnRotation;
    public bool hasLifetime;
    public float lifetime;
    public bool collisionStartDelay;
    public float delayTime;

    public ParticleSystem dustCloud;
    public GameObject[] smallRubble;    
    public GameObject[] mediumRubble;

	// Use this for initialization
	void Start () {
        if (collisionStartDelay)
        {
            gameObject.GetComponent<PolygonCollider2D>().enabled = false;
            Invoke("EnableCollision",delayTime);
        }

        float speed = Random.Range(-spawnSpeed, spawnSpeed);	
        Vector2 speedVector = new Vector2(Random.Range(-1f,1f),Random.Range(-1f,1f));
        gameObject.GetComponent<Rigidbody2D>().velocity = speedVector * speed;

        float mass = gameObject.GetComponent<Rigidbody2D>().mass;
        gameObject.GetComponent<Rigidbody2D>().AddTorque(Random.Range(- spawnRotation * mass, spawnRotation * mass));
        if (hasLifetime)
        {
            Destroy(gameObject, UnityEngine.Random.Range(lifetime, 2 * lifetime));
        }
	}

    void EnableCollision()
    {
        gameObject.GetComponent<PolygonCollider2D>().enabled = true;
    }

    void OnCollisionEnter2D(Collision2D collision) {
        if(collision.gameObject.tag == "Projectile" 
            || collision.gameObject.tag == "Vessel"
            || collision.gameObject.tag == "Scenery")
        {
            if (collision.gameObject.tag == "Projectile"){
                collision.gameObject.GetComponent<ProjectileController>().Hit(1f, gameObject);
                health -= collision.gameObject.GetComponent<ProjectileController>().projectileDamage;
            }            
            health -= collision.relativeVelocity.magnitude * collision.gameObject.GetComponent<Rigidbody2D>().mass;
        }
        

        // the impulse is taken from health for damage
        
        if (health < 0) {
            Vector3 dustPos = new Vector3(
                gameObject.transform.position.x,
                gameObject.transform.position.y,
                -1f
                );
            Instantiate(dustCloud, dustPos, Quaternion.identity);
            if (rubbleAmount != 0)
            {
                gameObject.GetComponent<PolygonCollider2D>().enabled = false;
                SpawnRubble();
            }
            Die();
            
        }
    }

    public void AddDamage(float damage)
    {
        health -= damage;

        if (health < 0)
        {
            Vector3 dustPos = new Vector3(
                gameObject.transform.position.x,
                gameObject.transform.position.y,
                -1f
                );
            Instantiate(dustCloud, dustPos, Quaternion.identity);
            if (rubbleAmount != 0)
            {
                gameObject.GetComponent<PolygonCollider2D>().enabled = false;
                SpawnRubble();
            }
            Die();

        }
    }

    void SpawnRubble() {
        // float probability = Random.Range(0f, 1f);
        

        if (mediumRubble.Length > 0) {
            for (int i = 1; i <= rubbleAmount; i++) {
                Vector3 spawnDirection = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f).normalized;
                float spawnDistance = Random.Range(1.5f, 2.5f);
                Vector3 spawnPosition = gameObject.transform.position + spawnDirection * spawnDistance;

                // int rubbleIndex = UnityEngine.Random.Range(0, smallRubble.Length);

                GameObject fragment = Instantiate(
                    mediumRubble[Random.Range(0,mediumRubble.Length)], 
                    spawnPosition, 
                    Quaternion.identity) as GameObject;

                float newZ = UnityEngine.Random.Range(-180f, 180f);
                fragment.transform.eulerAngles = new Vector3(0f, 0f, newZ);

                fragment.GetComponent<Rigidbody2D>().velocity = 
                    new Vector2(spawnDirection.x * rubbleSpeed * 2, spawnDirection.y * rubbleSpeed * 2);
                fragment.transform.parent = gameObject.transform.parent;
                    
            }

        }
        if (smallRubble.Length > 0) {
            for (int i = 1; i <= rubbleAmount * 4; i++)
            {
                Vector3 spawnDirection = new Vector3(Random.Range(-2f, 2f), Random.Range(-2f, 2f), 0f).normalized;
                float spawnDistance = Random.Range(0f, 1f);
                Vector3 spawnPosition = gameObject.transform.position + spawnDirection * spawnDistance;

                int rubbleIndex = UnityEngine.Random.Range(0, smallRubble.Length);
                
                GameObject fragment = Instantiate(
                    smallRubble[rubbleIndex],
                    spawnPosition,
                    Quaternion.identity) as GameObject;

                float newZ = UnityEngine.Random.Range(-180f, 180f);
                fragment.transform.eulerAngles = new Vector3(0f, 0f, newZ);

                fragment.GetComponent<Rigidbody2D>().AddTorque(UnityEngine.Random.Range(-30, 30));

                float speed = fragment.GetComponent<AsteroidScript>().spawnSpeed;

                fragment.GetComponent<Rigidbody2D>().velocity =
                    new Vector2(spawnDirection.x * speed, spawnDirection.y * speed);
                fragment.transform.parent = gameObject.transform.parent;

            }

        }

    }
    public void Die() {
        Destroy(gameObject);
    }




}
