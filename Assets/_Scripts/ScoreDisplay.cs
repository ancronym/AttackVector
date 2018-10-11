using UnityEngine;
using UnityEngine.UI;	
using System.Collections;

public class ScoreDisplay : MonoBehaviour {

	// Use this for initialization
	void Start () {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = true;

        Text myText = GetComponent<Text> ();
        myText.text = "Kills: " + ScoreKeeper.kills
            + "\nLosses: " + ScoreKeeper.losses
            + "\nPlayer kills: " + ScoreKeeper.playerKills
            + "\nHit percentage: " + ScoreKeeper.GetHitPercentage() 
            ;
		ScoreKeeper.Reset ();	
	}
}
