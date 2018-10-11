using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FuelText : MonoBehaviour {

    Text fuelText;

    // Use this for initialization
    void Start()
    {
        fuelText = gameObject.GetComponent<Text>();
        fuelText.text = "Fuel: ";
    }

    public void UpdateFuelText(float fuel, float mass)
    {
        fuelText.text = "Fuel/Mass: " + fuel.ToString() + " " + mass.ToString();
    }
}

