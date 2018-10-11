using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LockIndicatorScript : MonoBehaviour {

    public enum LineType { RWR, Lock };
    LineType type;

    GameObject player;
    Vector3 source;
    public LineRenderer line;
    public Color lockColor;
    public Color rwrColor;
    Color initialColor;
    public float indiD1 = 10f, indiD2 = 4f;


    float iniTime;
    public float lineDuration = 1f; float newAlpha;

    Vector3 normToLocker; Vector3 lineStart; Vector3 lineEnd;

	// Use this for initialization
	void Start () {
        iniTime = Time.timeSinceLevelLoad;
        initialColor = line.endColor;
	}
	
	// Update is called once per frame
	void Update () {
        normToLocker = (source - player.transform.position).normalized;
        lineStart = player.transform.position + normToLocker * indiD1;
        lineEnd = player.transform.position + normToLocker * indiD2;

        line.SetPosition(0, lineStart);
        line.SetPosition(1, lineEnd);

		

        newAlpha = 1 - (Time.timeSinceLevelLoad - iniTime) / lineDuration;

        switch (type)
        {
            case LineType.Lock:
                line.startColor = new Color(lockColor.r, lockColor.g, lockColor.b, newAlpha);
                line.endColor = new Color(lockColor.r, lockColor.g, lockColor.b, newAlpha);

                break;
            case LineType.RWR:
                line.startColor = new Color(rwrColor.r, rwrColor.g, rwrColor.b, newAlpha);
                line.endColor = new Color(rwrColor.r, rwrColor.g, rwrColor.b, newAlpha);
                break;
        }

        if (iniTime + lineDuration < Time.timeSinceLevelLoad)
        {
            Destroy(gameObject);
        }

    }

    public void SetLockLineParams(GameObject Player, Vector3 Source, LineType Type)
    {
        player = Player;
        source = Source;
        type = Type;
    }
}
