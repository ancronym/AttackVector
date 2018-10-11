using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CivWpScript : MonoBehaviour {

    public CircleCollider2D wpCollider;

	// Use this for initialization
	void Start () {
		
	}

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.GetComponent<VesselAI>())
        {
            if(collision.gameObject.GetComponent<ShipController>().shipType == ShipController.ShipType.civilian)
            {
                collision.gameObject.GetComponent<VesselAI>().EnteredCivWp();
                Destroy(gameObject, 1f);
            }            
        }         
    }

    // Update is called once per frame
    void Update () {
		
	}
}
