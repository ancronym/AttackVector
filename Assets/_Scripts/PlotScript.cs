using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlotScript : MonoBehaviour {

    GameObject target;
    float duration, iniTime;

    public float inverseScale = 20f;

    // Use this for initialization
    void Start () {
        float scale = (float)MissionPlanner.mapRadius / inverseScale;
        gameObject.transform.localScale = new Vector3(scale, scale, scale);

        if (MissionPlanner.mapRadius < 100)
        {
            gameObject.transform.localScale = new Vector3(10f, 10f, 10f);
        }

        iniTime = Time.timeSinceLevelLoad;
    }

    public void SetUpPlot(GameObject Target, float Duration)
    {
        target = Target;
        duration = Duration;
        
    }
	
	// Update is called once per frame
	void Update () {
        if (target != null)
        {
            gameObject.transform.position = target.transform.position;
        }
        else
        {
            Destroy(gameObject);
        }

        if(iniTime + duration < Time.timeSinceLevelLoad)
        {
            Destroy(gameObject);
        }

	}
}
