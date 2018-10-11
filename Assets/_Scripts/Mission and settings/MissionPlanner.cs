using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MissionPlanner : MonoBehaviour {

    public enum MissionType {
        destroyEnemyVessels,
        defendBase,
        attackBase,        
        escort
    }
    public enum AsteroidDistribution {
        none,
        gradient,
        uniform
    }

    public enum StartLocation
    {
        N,NE,E,SE,S,SW,W,NW
    }

    public static int mapRadius;
    public static int smallRadius = 200;
    public static int mediumRadius = 400;
    public static int largeRadius = 600;
    


    // Statics set by the mission generator:
    public static MissionType missionType;
    public static AsteroidDistribution asteroidDistribution;    

    public Text missionDescription;
    public Text settingDescription;

    public TextAsset attackBase;
    public TextAsset defendbase;
    public TextAsset destroyFighters;
    public TextAsset escortMilitary;
    public TextAsset escortCivlian;

    public TextAsset noAsteroids;
    public TextAsset uniformAsteroids;
    public TextAsset gradientAsteroids;

    // Team start locations
    public static StartLocation greenStart;
    public static StartLocation redStart;
    public static Vector2 greenPosition;
    public static Vector2 redPosition;
    
    void Start () {
        LevelManager.isPaused = false; // This should always happen before the game level is loaded, which is essential

        SetMissionType();

        SetAsteroidDistribution();
        
        // This is a bad method, does also map resizing ... 
        DisplayMissionAndAsteroidText();

        DetermineTeamPositions();        
	}    

    void SetMissionType()
    {
        // Whoa, here we get the amount of types in the enum and set the static missionType by random
        int amountOptions = System.Enum.GetNames(typeof(MissionType)).Length;
        missionType = (MissionType)UnityEngine.Random.Range(0, amountOptions);
        // Debug.Log("Mission type: " + missionType);
    
    }

    

    void SetAsteroidDistribution() {
        int amountTypes = System.Enum.GetNames(typeof(AsteroidDistribution)).Length;
        asteroidDistribution = (AsteroidDistribution)UnityEngine.Random.Range(0, amountTypes);
        // Debug.Log("Steroidbution: " + asteroidDistribution);

    }


    // Seems ok, but is not optimal, does too many things
    void DisplayMissionAndAsteroidText() {
        switch (missionType) {
            case MissionType.attackBase:
                missionDescription.text = attackBase.text;
                mapRadius = largeRadius;
                break;

            case MissionType.defendBase:
                missionDescription.text = defendbase.text;
                mapRadius = smallRadius;
                break;

            case MissionType.destroyEnemyVessels:
                missionDescription.text = destroyFighters.text;
                mapRadius = smallRadius;
                break;

            case MissionType.escort:
                missionDescription.text = escortCivlian.text;
                mapRadius = largeRadius;
                break;            
        }
        Debug.Log("MapSize: " + mapRadius);

        switch (asteroidDistribution) {
            case AsteroidDistribution.gradient:
                settingDescription.text = gradientAsteroids.text;
                break;
            case AsteroidDistribution.none:
                settingDescription.text = noAsteroids.text;
                break;

            case AsteroidDistribution.uniform:
                settingDescription.text = uniformAsteroids.text;
                break;
        }

    }

    // Pretty Solid
    void DetermineTeamPositions()
    {
        int amountPositions = System.Enum.GetNames(typeof(StartLocation)).Length;
        int greenSector = UnityEngine.Random.Range(0, amountPositions);
        int redSector = 0;
        if (greenSector < 4) { redSector = greenSector + UnityEngine.Random.Range(2, 3); }
        if (greenSector > 3) { redSector = greenSector - UnityEngine.Random.Range(2, 3); }

        greenStart = (StartLocation)greenSector;
        redStart = (StartLocation)redSector;
        //Debug.Log("Green sector: " + greenSector + "Red sector: " + redSector);

        greenPosition = DetermineCoordinates(greenStart);
        redPosition = DetermineCoordinates(redStart);

        //Debug.Log("Green coord: " + greenPosition + "Red sector" + redPosition);

    }

    // Pretty Solid
    Vector2 DetermineCoordinates(StartLocation startLocation)
    {
        Vector2 finalPos2D = new Vector2(0f,0f);
        switch (startLocation)
        {
            case StartLocation.N:
                finalPos2D = new Vector2(
                    (float)mapRadius * UnityEngine.Random.Range(-0.15f,0.15f),
                    (float)mapRadius * UnityEngine.Random.Range(0.6f, 0.8f)
                    );
                break;
            case StartLocation.NE:
                finalPos2D = new Vector2(
                    (float)mapRadius * UnityEngine.Random.Range(0.6f, 0.8f),
                    (float)mapRadius * UnityEngine.Random.Range(0.6f, 0.8f)
                    );
                break;
            case StartLocation.E:
                finalPos2D = new Vector2(
                    (float)mapRadius * UnityEngine.Random.Range(0.6f, 0.8f),
                    (float)mapRadius * UnityEngine.Random.Range(-0.15f, 0.15f)
                    );
                break;
            case StartLocation.SE:
                finalPos2D = new Vector2(
                    (float)mapRadius * UnityEngine.Random.Range(0.6f, 0.8f),
                    (float)mapRadius * UnityEngine.Random.Range(-0.6f, -0.8f)
                    );
                break;
            case StartLocation.S:
                finalPos2D = new Vector2(
                    (float)mapRadius * UnityEngine.Random.Range(-0.15f, 0.15f),
                    (float)mapRadius * UnityEngine.Random.Range(-0.6f,-0.8f)
                    );
                break;
            case StartLocation.SW:
                finalPos2D = new Vector2(
                    (float)mapRadius * UnityEngine.Random.Range(-0.6f, -0.8f),
                    (float)mapRadius * UnityEngine.Random.Range(-0.6f, -0.8f)
                    );
                break;
            case StartLocation.W:
                finalPos2D = new Vector2(
                    (float)mapRadius * UnityEngine.Random.Range(-0.6f, -0.8f),
                    (float)mapRadius * UnityEngine.Random.Range(-0.15f, 0.15f)
                    );
                break;
            case StartLocation.NW:
                finalPos2D = new Vector2(
                    (float)mapRadius * UnityEngine.Random.Range(-0.6f, -0.8f),
                    (float)mapRadius * UnityEngine.Random.Range(0.6f, 0.8f)
                    );
                break;

        }
        return finalPos2D;
    }

}
