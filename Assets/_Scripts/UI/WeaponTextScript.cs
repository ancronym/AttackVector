using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;

public class WeaponTextScript : MonoBehaviour {

    public Text selectedWeaponText;

	// Use this for initialization
	void Start () {
		
	}
	
    public void SetUIWeapontext(string weaponName)
    {
        selectedWeaponText.text = "Weapon: " + weaponName;
    }
}
