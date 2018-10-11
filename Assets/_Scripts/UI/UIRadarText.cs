using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIRadarText : MonoBehaviour {

    Text radarText;

	// Use this for initialization
	void Start () {
        radarText = gameObject.GetComponent<Text>();
        radarText.text = "Radar off";
	}

    public void SetUIRadarText(string state) {
        radarText.text = "Radar: " + state;
    }
}
