using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NTools : MonoBehaviour {
      

    // Instance of this class maintains the Vector2 coordinates and float radius of one waypoint
    public class Waypoint {

        public Vector2 wpCoordinates;
        public float wpRadius;
        public float reccommendedSpeed;
        public bool movingWP;
        public bool temporary;

        public Waypoint(Vector2 coord, float radius, bool movingwp, bool temp) {
            wpCoordinates = coord;
            wpRadius = radius;
            movingWP = movingwp;
            temporary = temp;
        }

        public Waypoint(Vector2 coord, float radius, float speed, bool movingwp, bool temp)
        {
            wpCoordinates = coord;
            wpRadius = radius;
            reccommendedSpeed = speed;
            movingWP = movingwp;
            temporary = temp;
        }
    }

    public class CollisionThreat
    {
        public Vector2 threatCoordinates;
        public Vector2 threatCoordinates2;
        public Vector2 threatVelocity;

        // ------------- Floats for sorting threats:----------------
        
        // Negative for left, positive for right, should be calculated by crossproduct so it also gives a heading basically
        public float leftRightOfHeading = 0;
        public float sqrDistance = 0;

        public CollisionThreat(Vector2 threatCoords, Vector2 threatCoords2, Vector2 threatVel, float leftOrRight, float distanceSQR)
        {
            threatCoordinates = threatCoords;
            threatCoordinates2 = threatCoords2;
            threatVelocity = threatVel;
            leftRightOfHeading = leftOrRight;
            sqrDistance = distanceSQR;
        }
    }

    public static Vector2 RotateVector2(Vector2 v, float degrees)
    {
        float sin = Mathf.Sin(degrees * Mathf.Deg2Rad);
        float cos = Mathf.Cos(degrees * Mathf.Deg2Rad);

     
        return new Vector2((cos * v.x) - (sin * v.y), (sin * v.x) + (cos * v.y));
    }

    public static Vector3 RotateVector3(Vector3 v, float degrees)
    {
        float sin = Mathf.Sin(degrees * Mathf.Deg2Rad);
        float cos = Mathf.Cos(degrees * Mathf.Deg2Rad);


        return new Vector3((cos * v.x) - (sin * v.y), (sin * v.x) + (cos * v.y),0f);
    }

    public class CollisionThreatsSorted
    {
        public bool realThreatsPresent = false;
        
        public List<NTools.CollisionThreat> realThreatsLeft = new List<NTools.CollisionThreat>(20);
        public List<NTools.CollisionThreat> realThreatsRight = new List<NTools.CollisionThreat>(20);

        public CollisionThreatsSorted(
            bool bthreats, 
            List<NTools.CollisionThreat> leftRealThreats, 
            List<NTools.CollisionThreat> rightRealThreats
            )
        {
            realThreatsPresent = bthreats;           
            realThreatsLeft = leftRealThreats;
            realThreatsRight = rightRealThreats;

        }
    }

    public static float HeadingFromPositions(Vector2 position1, Vector2 position2)
    {
        return HeadingFromVector(new Vector2(
            position2.x-position1.x,
            position2.y -position1.y
            ));        
    }

    public static float HeadingFromVector(Vector2 vector)
    {
        float heading = (float)((Mathf.Atan2(vector.x,vector.y)/Mathf.PI)*180f);
        if (heading < 0) { heading += 360f; }
        return heading = 360 - heading;     
    }

    // Disregards the Z component!!!
    public static float HeadingFromVector(Vector3 vector)
    {
        float heading = (float)((Mathf.Atan2(vector.x, vector.y) / Mathf.PI) * 180f);
        if (heading < 0) { heading += 360f; }
        return heading = 360 - heading;
    }

    public static Vector3 GetCenterOfObjects3D(List<GameObject> objectList)
    {
        Vector3 center = new Vector3(0,0,0);

        // sanity check
        if ( objectList.Count == 0) { return center; }

        float xSum = 0f, ySum = 0f, zSum = 0f;

        for (int i = 0; i < objectList.Count; i++)
        {
            if (objectList[i] != null)
            {
                xSum += objectList[i].transform.position.x;
                ySum += objectList[i].transform.position.y;
                zSum += objectList[i].transform.position.z;
            }
        }

        center.x = xSum / objectList.Count;
        center.y = ySum / objectList.Count;
        center.z = zSum / objectList.Count;

        return center;
    }

    public static Vector3 GetCenterOfBogies3D(List<RadarController.Bogie> bogieList)
    {
        Vector3 center = new Vector3(0, 0, 0);

        // sanity check
        if (bogieList.Count == 0) { return center; }

        float xSum = 0f, ySum = 0f, zSum = 0f;

        int bogieCount = bogieList.Count;

        for (int i = 0; i < bogieList.Count; i++)
        {
            if (bogieList[i].bogieObject != null)
            {
                xSum += bogieList[i].bogieObject.transform.position.x;
                ySum += bogieList[i].bogieObject.transform.position.y;
                zSum += bogieList[i].bogieObject.transform.position.z;
            }
            else
            {                
                bogieCount--;
            }
        }
        if (bogieCount != 0)
        {
            center.x = xSum / bogieCount;
            center.y = ySum / bogieCount;
            center.z = zSum / bogieCount;
        }

        return center;
    }

    // used for describing between which bearings something resides
    public class Disposition
    {
        public Vector3 sliceOrigin;
        public float originHeading;
        public float leftmostBearing;
        public float rightmostBearing;
        public float bearingRange;       
        public float stdDeviation;
        public Vector3 centerOfMass;
        public Vector2 globalHeading;


        public Disposition(
            Vector3 sliceorigin,
            float heading,
            float leftbearing,
            float rightbearing,
            float groupspread,
            float dispersn,
            Vector3 centerofmass,
            Vector2 GlobalHeading
            )
        {
            sliceOrigin = sliceorigin;
            originHeading = heading;
            leftmostBearing = leftbearing;
            rightmostBearing = rightbearing;
            bearingRange = groupspread;
            stdDeviation = dispersn;
            centerOfMass = centerofmass;
            globalHeading = GlobalHeading;
        }
    }

    // Used to determine for example in which direction the enemy lies and how widespread they are.
    // works in CCW mode
    public static NTools.Disposition AnalyseGroupDisposition(Vector3 originPos, float originHeading, List<GameObject> objects)
    {
        NTools.Disposition slice = new NTools.Disposition(originPos, originHeading, 0, 0, 0, 0, new Vector3(0, 0, 0), new Vector2(0,0));
        
        // sanity check, no objects means return of 000000 !!!
        if (objects.Count == 0)        {            return slice;        }

        float tempBear, leftBear = 0, rightBear = 0;

        // statistical analysis floats:
        float meanbearing = 0f, sumDiffFromMean = 0f;
        float[] bearings = new float[objects.Count];

        slice.centerOfMass = NTools.GetCenterOfObjects3D(objects);

        // overall vector computation floats
        float headingX = 0, headingY = 0;

        for (int i = 0; i < objects.Count; i++)
        {
            // left bearing is positive, right bearing is negative, clamped between -180 to 180
            tempBear = Mathf.Clamp(
                NTools.GetBearingFromHeadings(
                    originHeading, 
                    NTools.HeadingFromVector(objects[i].transform.position - originPos)
                ), -180f,180f);

            // storing for later analysis
            bearings[i] = tempBear;

            // with the first heading set both left and right bearings
            if(i == 0) { leftBear = tempBear; rightBear = tempBear; }
            else
            {
                if (tempBear > leftBear) { leftBear = tempBear;  }
                else if ( tempBear < rightBear) { rightBear = tempBear;  }
            }

            // determining overall heading of the group of objects
            headingX = headingX + objects[i].GetComponent<Rigidbody2D>().velocity.x;
            headingY = headingY + objects[i].GetComponent<Rigidbody2D>().velocity.y;
        }

        slice.globalHeading.x = headingX / objects.Count;
        slice.globalHeading.y = headingY / objects.Count;


        // disperison computation
        for (int i = 0; i < bearings.Length; i++)
        {
            meanbearing += bearings[i];
        }
        meanbearing = meanbearing / bearings.Length;

        for (int i = 0; i < bearings.Length; i++)
        {
            sumDiffFromMean += bearings[i] - meanbearing;
        }


        // Filling the slice !!!
        slice.leftmostBearing = leftBear;
        slice.rightmostBearing = rightBear;
        slice.bearingRange = leftBear - rightBear;
        slice.stdDeviation = Mathf.Sqrt(  Mathf.Pow(sumDiffFromMean , 2f) / (bearings.Length - 1) );

        return slice;
    }
    

    // works CCW, does it work the other way around? Yes, it does!!!
    public static float GetBearingFromHeadings(float ahead, float headingToObject)
    {
        float bearing = 0f;

        if (ahead < headingToObject && (headingToObject - ahead) > 180f)
        {
            bearing = headingToObject - 360 - ahead;

        }
        else if (ahead < headingToObject && (headingToObject - ahead) < 180f)
        {
            bearing = headingToObject - ahead;
        }
        else if (headingToObject < ahead && (ahead - headingToObject) > 180f)
        {
            bearing = headingToObject + 360 - ahead;
        }
        else if (headingToObject < ahead && (ahead - headingToObject) < 180f)
        {
            bearing = headingToObject - ahead;
        }

        return bearing;
    }

}
