using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SplashScript : MonoBehaviour {

    public LevelManager levelManager;
    private float startTime;
    public float splashDuration = 1f;

	void Start () {
        startTime = Time.timeSinceLevelLoad;        
	}
	
	void Update () {
        if(splashDuration < Time.timeSinceLevelLoad - startTime)
        {
            levelManager.LoadNextLevel();
        }
		
	}
}
