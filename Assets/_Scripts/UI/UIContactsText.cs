using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIContactsText : MonoBehaviour {

    Text ContactsText;

	// Use this for initialization
	void Start () {
        ContactsText = gameObject.GetComponent<Text>();
        ContactsText.text = "Team Contacts: 0 / Own Contacts: 0";

    }
	
	public void SetContactsText(int team, int own)
    {
        ContactsText.text = "Team Contacts: " + team + " / Own Contacts: " + own;
    }
}
