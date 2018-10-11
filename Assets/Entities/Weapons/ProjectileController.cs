using UnityEngine;
using System.Collections;

public class ProjectileController : MonoBehaviour {

    public bool penetrator;
	public float projectileDamage;
    float initialDamage;
    public float lifetime;       

	public ParticleSystem blast;
    public Light pointLight;
    SpriteRenderer renderer;

    public GameObject owner;
    

    // used for alpha channel setting:
    float startTime;    
    float newAlpha;

    // Light management
    public Light haloLight;
    float initialIntensity;

    // Use this for initialization
    void Start () {
        startTime = Time.timeSinceLevelLoad;       
        renderer = gameObject.GetComponent<SpriteRenderer>();
        initialIntensity = haloLight.intensity;
        initialDamage = projectileDamage;        
	}

    void Update() {
        float travelTime = Time.timeSinceLevelLoad - startTime;

        projectileDamage = initialDamage * (1 - travelTime / lifetime);

        if (Time.timeSinceLevelLoad > lifetime + startTime) {
            // Debug.Log("Lifetime: " + lifetime);
            Destroy(gameObject);
        }
        
        newAlpha = (1f - (Time.timeSinceLevelLoad - startTime)) / lifetime;
        newAlpha = Mathf.Clamp(newAlpha, 0.5f, 1f);
        renderer.color = new Color(1f, 1f, 1f, newAlpha);
        
        if(haloLight != null)
        {
            
            haloLight.intensity = initialIntensity * (1 - travelTime/lifetime);            
        }
    }	

    public void AddDamage()
    {
        ParticleSystem deathBlast = Instantiate(blast, gameObject.transform.position, Quaternion.identity) as ParticleSystem;
        float x = Random.Range(-170f, 170f);
        float y = Random.Range(-170f, 170f);
        float z = Random.Range(-170f, 170f);

        deathBlast.gameObject.transform.eulerAngles = new Vector3(x, y, z);

        Destroy(gameObject);
    }
	
	public void Hit(float targetHealth, GameObject target){

        Vector2 velocity = new Vector2(0, 0);
        float Z;

       

        owner.GetComponent<WepContr>().ReportHit();

        if(targetHealth <= 0 && target.tag == "Vessel")
        {
            owner.GetComponent<WepContr>().ReportKill(target);
        }

		
        ParticleSystem deathBlast = Instantiate(blast, gameObject.transform.position, Quaternion.identity) as ParticleSystem;
        float x = Random.Range(-170f, 170f);
        float y = Random.Range(-170f, 170f);
        float z = Random.Range(-170f, 170f);

        deathBlast.gameObject.transform.eulerAngles = new Vector3(x, y, z);
        
        Destroy(gameObject);      
	}
}
