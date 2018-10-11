using UnityEngine;
using System.Collections;

public class BoundaryScript : MonoBehaviour {

	public float boundaryBounce = 0.2f;

	// Use this for initialization
	void OnTriggerEnter2D (Collider2D collider){
		GameObject escaper = collider.gameObject;

		if (escaper.tag == "Projectile") {
			Destroy (escaper);
		} else if (escaper.tag == "Player" || escaper.tag == "Vessel") {
			Vector2 velocity = escaper.GetComponent<Rigidbody2D>().velocity;
			// Debug.Log (velocity.x + velocity.y);
			escaper.GetComponent<Rigidbody2D>().velocity = new Vector2(-velocity.x*boundaryBounce,-velocity.y*boundaryBounce);
		}

	}
	
	// Update is called once per frame
	void Update () {
	
	}
}
