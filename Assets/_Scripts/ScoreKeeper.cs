using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class ScoreKeeper : MonoBehaviour {
	public static int score = 0;
    public static int kills = 0;
    public static int playerKills = 0;
    public static int losses = 0;
    public static float hitPercentage;
    public static int hits;
    public static int shots;
	public Text scoreText;

	// Use this for initialization
	void Start () {
		Reset ();
	}
	
	// Update is called once per frame
	public void addScore(){
		score++;
		scoreText.text = "SCORE: " + score;
	}

    public static float GetHitPercentage()
    {
        return (hits / shots) * 100f;
    }

	public static void Reset(){
		score = 0;
	}
}
