using UnityEngine;
using System.Collections;

public class GridCreator : MonoBehaviour {

    public GameObject gridLine;

    public GameObject bouncerPrefab;
    public GameObject wallPrefab;
    float mapRadFloat;
    int mapRadInt;

    Vector3 wallPos, bouncerPos, wallRotation;

    // Use this for initialization
    void Start () {
        mapRadInt = MissionPlanner.mapRadius;
        mapRadFloat = (float)mapRadInt;

        DrawGrid();        
        PlaceWalls();
	}

    void DrawGrid()
    {  
        for (int i = -mapRadInt; i <= mapRadInt; i += 20)
        {
            float loopFloat = (float)i;

            Vector3 horizontalZero = new Vector3(-mapRadFloat, loopFloat, 10f);
            Vector3 horizontalOne = new Vector3(mapRadFloat, loopFloat, 10f);
            Vector3 verticalZero = new Vector3(loopFloat, mapRadFloat, 10f);
            Vector3 verticalOne = new Vector3(loopFloat, -mapRadFloat, 10f);

            GameObject line = Instantiate(gridLine, gameObject.transform.position, Quaternion.identity) as GameObject;

            line.transform.parent = gameObject.transform;
            line.GetComponent<LineRenderer>().SetPosition(0, horizontalZero);
            line.GetComponent<LineRenderer>().SetPosition(1, horizontalOne);

            line = Instantiate(gridLine, gameObject.transform.position, Quaternion.identity) as GameObject;
            line.transform.parent = gameObject.transform;
            line.GetComponent<LineRenderer>().SetPosition(0, verticalZero);
            line.GetComponent<LineRenderer>().SetPosition(1, verticalOne);
        }
    }

    void PlaceWalls()
    {
        for(int x = -1; x <=1; x++)
        {
            for(int y = -1; y <=1; y++)
            {
                if(x == -1 && y == 0) {
                    InstantiateWall(-1f, 0f);
                }
                else if (x == 1 && y == 0) {
                    InstantiateWall(1f, 0f);
                }
                else if (x == 0 && y == -1) {
                    InstantiateWall(0f, -1f);
                }
                else if (x == 0 && y == 1) {
                    InstantiateWall(0f, 1f);
                }

            }
        }
    }

    void InstantiateWall(float xPos, float yPos)
    {
        // the wall is one unit further away than the bouncer
        wallPos = new Vector3(xPos * (mapRadFloat + 1f), yPos  * (mapRadFloat + 1f), 0f);
        bouncerPos = new Vector3(xPos * mapRadFloat, yPos * mapRadFloat, 0f);

        Vector2 wallSize = new Vector2((2 * Mathf.Abs(yPos) * mapRadFloat) + 1f, (Mathf.Abs(xPos) * mapRadFloat * 2) + 1);

        GameObject wall = Instantiate(wallPrefab, wallPos, Quaternion.identity) as GameObject;
        GameObject bouncer = Instantiate(bouncerPrefab, bouncerPos, Quaternion.identity) as GameObject;
       
        wall.GetComponent<BoxCollider2D>().size = wallSize;
        bouncer.GetComponent<BoxCollider2D>().size = wallSize;
    }
}
