using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MinimapCameraController : MonoBehaviour {

    public Camera minimapCamera;
    int screenHeight;
    

	// Use this for initialization
	void Start () {
        screenHeight = Screen.currentResolution.height;

        minimapCamera.aspect = 1f;
        minimapCamera.orthographicSize = MissionPlanner.mapRadius;

        if(MissionPlanner.mapRadius < 100)
        {
            minimapCamera.orthographicSize = 200f;
        }

        InvokeRepeating("LongUpdate", 2f, 3f);
	}
	
	void LongUpdate()
    {
        screenHeight = Screen.currentResolution.height;


    }
}
