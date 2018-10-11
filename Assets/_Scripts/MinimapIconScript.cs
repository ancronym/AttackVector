using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MinimapIconScript : MonoBehaviour {

    // inversly proportional to the map size
    public float inverseScale = 20f;

	// Use this for initialization
	void Start () {
        float scale = (float)MissionPlanner.mapRadius / inverseScale;
        gameObject.transform.localScale = new Vector3(scale, scale, scale);

        if(MissionPlanner.mapRadius < 100)
        {
            gameObject.transform.localScale = new Vector3(10f, 10f, 10f);
        }
	}
	
	// Update is called once per frame
	void Update () {
		
	}
}
