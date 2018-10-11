using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class RAMP
{
    // The number of the attack type is important - the higher the number, the bolder the attack
    public enum AttackTypeT { ambush, flank, charge }
    public enum AttackTypeF { sentry, bracket, charge }
    enum Direction { CW, CCW };

    public class Waypoint
    {
        public GameObject sentryTarget;
        public Vector2 wpCoordinates;
        public float wpRadius;
        public float reccommendedSpeed;
        public bool movingWP = false;
        public bool temporary = false;

        public Waypoint(Vector2 coord, float radius, bool movingwp, bool temp)
        {
            wpCoordinates = coord;
            wpRadius = radius;
            reccommendedSpeed = 0f;
            movingWP = movingwp;
            temporary = temp;
        }

        public Waypoint(GameObject SentryTarget, Vector2 coord, float radius, bool movingwp, bool temp)
        {
            sentryTarget = SentryTarget;
            wpCoordinates = coord;
            wpRadius = radius;
            reccommendedSpeed = 0f;
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

    public class Route
    {
        public List<RAMP.Waypoint> wpList;

        public Route()
        {
            wpList = new List<Waypoint>(10);
        }

        public Route(List<RAMP.Waypoint> Waypoints)
        {
            wpList = Waypoints;
        }

        public Route(int wpCount)
        {
            wpList = new List<Waypoint>(wpCount);
        }
    }

    public class AttackPlanF
    {
        public bool planSet = false;
        public AttackTypeF type;

        // For Bracket and charge
        public List<Route> routes = new List<Route>(2);             // only for the lead and rest half of the flight, so two is good

        // For sentry
        public Vector3 offsetFromEnemy;

        public AttackPlanF(bool set, AttackTypeF Type, List<Route> Routes)
        {
            planSet = set;
            type = Type;
            routes = Routes;
        }

        public AttackPlanF(bool set, AttackTypeF Type, Vector3 offset)
        {
            planSet = set;
            type = Type;
            offsetFromEnemy = offset;
        }

        public AttackPlanF(bool set)
        {
            planSet = set;
        }

    }

    public class AttackPlanT
    {
        public List<List<RAMP.Waypoint>> attackRoutes;
        public List<GameObject> targetList = new List<GameObject>(50);

        bool targetEngaged = false;
        bool targetDestroyed = false;
        bool forceDestroyed = false;

        // Success/Fail/Retreat conditions
        float minimumForcesRatio; // enemies / friendlies - retreat/regroup condition
        List<GameObject> friendlyForce = new List<GameObject>(50);

        public AttackPlanT(
            List<List<RAMP.Waypoint>> routes,
            float forceRatio,
            List<GameObject> FriendlyForce,
            List<GameObject> TargetList
            )
        {
            attackRoutes = routes;
            minimumForcesRatio = forceRatio;
            friendlyForce = FriendlyForce;
            targetList = TargetList;
        }
    }

    public static Route GetEscortableRoute(Vector3 currentPos, Vector3 Destination, Vector3 enemyPos)
    {
        Route route = new Route(2);

        route.wpList.Add(new Waypoint(
            new Vector2(Destination.x, Destination.y),
            10f,
            false,
            false
            ));

        Vector2 interpoint = new Vector2(
            -enemyPos.x / 2,
            -enemyPos.y / 2
            );

        route.wpList.Add(new Waypoint(
            interpoint,
            10f,
            false,
            false
            ));


        return route;
    }

    public static AttackPlanF GetAttackPlanF(AttackTypeF type, Vector3 leadPos, Vector3 enemyPos, List<Vector3> nearbyFriendlies)
    {
        AttackPlanF attackPlan = new AttackPlanF(true);



        switch (type)
        {
            case AttackTypeF.sentry:
                float SQRSep = 900f;
                float sentryRange = 30f;
                float searchDegrees = 30f;

                Vector3 offset = (enemyPos - leadPos).normalized * sentryRange;
                Vector3 sentryPos = enemyPos + offset;
                List<Vector3> nbfSentryPos = new List<Vector3>(nearbyFriendlies.Count);

                foreach (Vector3 fv in nearbyFriendlies)
                {
                    Vector3 fspos = enemyPos + (fv - enemyPos).normalized * sentryRange;
                    nbfSentryPos.Add(fspos);
                }

                bool clear = true;
                int failsafe = 20, i = 1;

                while (!clear)
                {
                    foreach (Vector3 fspos in nbfSentryPos)
                    {
                        float SQRDist = (fspos - sentryPos).sqrMagnitude;
                        if (SQRDist < SQRSep)
                        {
                            clear = false;
                        }
                    }

                    if (!clear)
                    {
                        float direction = 1f;
                        if ((i + 1) % 2 == 0) { direction = 1; }
                        else { direction = -1; }

                        direction = direction * i * searchDegrees;

                        offset = NTools.RotateVector3(offset, searchDegrees);
                        sentryPos = enemyPos + offset;
                    }

                    i++;
                    if (i >= failsafe) { clear = true; }
                }

                break;
            case AttackTypeF.bracket:

                break;
            case AttackTypeF.charge:

                break;
            default:
                break;
        }

        return attackPlan;
    }

    // returns Vector3 pointing to intercept point if the magnitude is used for velocity
    public static Vector3 GetLeadVector(GameObject interceptor, GameObject target, float intendedSpeed) {
        Vector3 interceptVector = new Vector3(0f, 0f, 0f);

        Vector2 targetV = target.GetComponent<Rigidbody2D>().velocity;

        float targetSpeed = targetV.magnitude;

        float Vi = intendedSpeed + targetSpeed;
        Vector2 Si = new Vector2(interceptor.transform.position.x, interceptor.transform.position.y);
        Vector2 St = new Vector2(target.transform.position.x, target.transform.position.y);
        Vector2 Vt = target.GetComponent<Rigidbody2D>().velocity;

        float a = Mathf.Pow(Vt.magnitude, 2) - Vi * Vi;
        float b = (St - Si).magnitude;
        float c = b * b;

        float t1 = (-b + Mathf.Sqrt(c - 4 * a * c)) / 2 * a;
        float t2 = (-b - Mathf.Sqrt(c - 4 * a * c)) / 2 * a;
        float t = 0;

        if (t1 < 0)
        {
            if (t2 > 0) {
                t = t2;
            }
            else
            {
                t = (St - Si).magnitude / Vi;
                Debug.Log("Fallback lead calculation");
            }
        }
        else if (t1 > 0 && t2 < 0)
        {
            t = t1;
        }
        else {
            Debug.Log("Fallback lead calculation");
            t = (St - Si).magnitude / Vi;
        }

        Vector3 interceptPoint = target.transform.position + new Vector3(targetV.x, targetV.y, 0f).normalized * t;

        interceptVector = interceptPoint - interceptor.transform.position;

        return interceptVector;
    }

    public static Vector2 GetLeadPoint(Vector2 Si, Vector2 St, Vector2 Vt, Vector2 V2i, float Vi)
    {
        Vector2 leadPoint = new Vector2(0, 0);

        float a = Mathf.Pow(Vt.magnitude, 2) - Vi * Vi;
        float b = (St - Si).magnitude;
        float c = b * b;

        float t1 = (-b + Mathf.Sqrt(c - 4 * a * c)) / (2 * a);
        float t2 = (-b - Mathf.Sqrt(c - 4 * a * c)) / (2 * a);
        float t = 0;

        if (t1 < 0)
        {
            if (t2 > 0)
            {
                t = t2;
            }
            else
            {
                t = (St - Si).magnitude / Vi;
                Debug.Log("Fallback lead calculation");
            }
        }
        else if (t1 > 0 && t2 < 0)
        {
            t = t1;
        }
        else
        {
            Debug.Log("Fallback lead calculation");
            t = (St - Si).magnitude / Vi;
        }

        V2i = -V2i;

        leadPoint = St + Vt * t + V2i * t;

        return leadPoint;
    }

    public static List<Route> PlanDestroyVessels(Vector3 target, List<GameObject> attackers)
    {
        float spread = 30f;
        float spreadDist = 0.66f;
        float mapLimiter = (float)MissionPlanner.mapRadius - 20;

        List<Route> routes = new List<Route>(attackers.Count);

        float normalSpeed = attackers[0].GetComponent<VesselAI>().interceptSpeed;
        float normalTime = (target - attackers[0].gameObject.transform.position).magnitude / normalSpeed;

        Vector3 V3toTarget = target - attackers[0].gameObject.transform.position;
        Vector3 V3toMidway = new Vector3(V3toTarget.x * spreadDist, V3toTarget.y * spreadDist, 0f);
        Vector3 targetPerpNorm = new Vector3(-V3toTarget.y, V3toTarget.x, 0f).normalized;
        Vector3 interWP;



        for (int i = 0; i < attackers.Count; i++)
        {
            float offset = 0f;

            Route attackRoute = new Route(2);

            attackRoute.wpList.Add(new Waypoint(new Vector2(target.x, target.y), normalSpeed, 10f, false, false));

            if (i == 0) { offset = 0f; }
            else if (i == 1) { offset = 1f; }
            else if (i == 2) { offset = -1f; }
            else if (i == 3) { offset = 1f; }
            else if (i == 4) { offset = -1f; }
            /*else
            {
                if(i % 2 == 0) { offset = -1f; }
                else { offset = 1; }
            }*/

            interWP = attackers[i].transform.position + V3toMidway + targetPerpNorm * spread * offset;

            // Make sure that the WP is inside the map boarder!!!
            interWP.x = Mathf.Clamp(interWP.x, -mapLimiter, mapLimiter);
            interWP.y = Mathf.Clamp(interWP.y, -mapLimiter, mapLimiter);

            // Debug.Log("InterWP " + interWP);

            attackRoute.wpList.Add(new Waypoint(new Vector2(interWP.x, interWP.y), normalSpeed, 10f, false, false));
            float routeLenght = GetRouteLength(attackRoute.wpList, attackers[i].transform.position);

            foreach (Waypoint wp in attackRoute.wpList)
            {
                wp.reccommendedSpeed = routeLenght / normalTime;
            }

            routes.Add(attackRoute);

            for (int j = 0; j < routes.Count; j++)
            {
                // Debug.Log("Route " + j + ": " + routes[j].wpList[0].wpCoordinates + routes[j].wpList[1].wpCoordinates);
            }
        }

        // Debug.Log("RL count: " + routes.Count);

        return routes;

    }

    // basic, but costly in compute power
    public static float GetRouteLength(List<RAMP.Waypoint> waypoints, Vector3 start)
    {
        float length = 0f;

        for (int i = 0; i < waypoints.Count; i++)
        {
            if (i < waypoints.Count - 1)
            {
                length += (waypoints[i].wpCoordinates - waypoints[i + 1].wpCoordinates).magnitude;
            } else
            {
                length += (waypoints[i].wpCoordinates - new Vector2(start.x, start.y)).magnitude;
            }
        }

        return length;
    }

    // The first member of the return vector is the heading for burn and second member dV required
    public static Vector2 GetHitBurn(Transform target, Transform seeker, float fuel, float mass) {
        Vector2 hitBurn = new Vector2(0f, 0f);

        float potentialdV = fuel / mass;

        Vector2 targetVector = target.GetComponent<Rigidbody2D>().velocity;
        Vector2 seekerVector = target.GetComponent<Rigidbody2D>().velocity;
        Vector2 relativeSpeedVector = seekerVector - targetVector;
        Vector2 directVector = new Vector2(target.position.x - seeker.position.x, target.position.y - seeker.position.y);

        float relativeSpeed = relativeSpeedVector.magnitude;


        // If the missile has not enough fuel to overcome the relative velocity already present, it will not accelerate or turn
        if (relativeSpeed >= potentialdV) {
            return new Vector2(seeker.eulerAngles.z, 0f);
        }

        // the divider 2 basically dictates that for this maneuver we use only half the fuel available AFTER correcting relative speed
        float dVforBurn = (potentialdV - relativeSpeed) / 10f + relativeSpeed;
        float staticTime = directVector.magnitude / ((potentialdV - relativeSpeed) / 2);

        Vector2 targetEndpoint = new Vector2(target.position.x + targetVector.x * staticTime, target.position.y + targetVector.y * staticTime);
        Vector2 seekerEndpoint = new Vector2(seeker.position.x + seekerVector.x * staticTime, seeker.position.y + seekerVector.y * staticTime);
        Vector2 hitVectorNormal = new Vector2(targetEndpoint.x - seekerEndpoint.x, targetEndpoint.y - seekerEndpoint.y).normalized;

        hitBurn = new Vector2(HeadingFromPositions(seekerEndpoint, targetEndpoint), dVforBurn);

        return hitBurn;

    }

    public static Route PlanPatrols(Vector3 defendable, Vector3 enemyPos, float layerWidth, int nrOfFlight, bool CW) {
        
        Route route = new Route(3);       

        float patrolWpRadius = 5f;

        Vector2 defObj = new Vector2(defendable.x, defendable.y);
        Vector2 vToCenter = new Vector2(0f, 0f) - defObj;
        Vector2 vToCenterPerp = new Vector2(-vToCenter.y, vToCenter.x).normalized * layerWidth * (float)nrOfFlight;
        Vector2 perpReverse = new Vector2(-vToCenterPerp.x, -vToCenterPerp.y).normalized * layerWidth * (float)nrOfFlight;

        Vector2 vToCentNorm = vToCenter.normalized * layerWidth * (float)nrOfFlight;

        Vector2 wp1 = new Vector2(20f,1f);        
        Vector2 wp2 = new Vector2(1f,20f);
        Vector2 wp3 = new Vector2(20f,1f);

        if (CW)
        {
            wp2 = defObj + vToCentNorm;
            wp3 = defObj + vToCentNorm + vToCenterPerp;
            wp1 = defObj + vToCentNorm + perpReverse;
        }
        else
        {
            wp2 = defObj + vToCentNorm;
            wp1 = defObj + vToCentNorm + vToCenterPerp;
            wp3 = defObj + vToCentNorm + perpReverse;
        }       

        route.wpList.Add(new Waypoint(wp1, patrolWpRadius, false, false));
        route.wpList.Add(new Waypoint(wp2, patrolWpRadius, false, false));
        route.wpList.Add(new Waypoint(wp3, patrolWpRadius, false, false));
        
        return route;
    }

    public static List<Vector3> GetSentryPositions(Vector3 defendable, Vector3 enemyPos, int nrOfFlights, bool moving)
    {
        List<Vector3> defencePositions = new List<Vector3>(nrOfFlights);

        float defencePosDistance = 20f;

        Vector3 vectorToenemy = (enemyPos - defendable).normalized * defencePosDistance;
        Vector3 tempVector = new Vector3(0f, 0f, 0f);

        if (moving)
        {
            for (int i = 0; i < nrOfFlights; i++)
            {
                if (i == 0) { tempVector = vectorToenemy; }
                else if (i == 1) { tempVector = new Vector3(-vectorToenemy.x, -vectorToenemy.y, 0f); }
                else if (i == 2) { tempVector = new Vector3(0f, -25f, 0f); }                            // behind
                else if (i == 3) { tempVector = new Vector3(0f, 25f, 0f); }                             // in front

                defencePositions.Add(tempVector);
            }
        }
        else
        {
            for (int i = 0; i < nrOfFlights; i++)
            {
                if (i == 0) { tempVector = vectorToenemy; }
                else if (i == 1) { tempVector = new Vector3(-vectorToenemy.x, -vectorToenemy.y, 0f); }
                else if (i == 2) { tempVector = new Vector3(0f, -25f, 0f); }                            // behind
                else if (i == 3) { tempVector = new Vector3(0f, 25f, 0f); }                             // in front

                defencePositions.Add(tempVector);
            }
        }    


        return defencePositions;
    }

    // returns the counterclockwise 0-360 heading from position1 towards position2
    public static float HeadingFromPositions(Vector2 position1, Vector2 position2) {
        

        float hypotenuse = Mathf.Abs(Vector2.Distance(position1, position2));


        // float interceptHeading = Mathf.Atan2(dynamicTargetPosition.y - seeker.position.y, dynamicTargetPosition.x - seeker.position.x) * 180 / Mathf.PI;

        // First Quadrant i.e NE
        if (position2.x > position1.x && position2.y > position1.y)
        {
            float deltaX = position2.x - position1.x;
            return 270 + Mathf.Acos(deltaX / hypotenuse) * Mathf.Rad2Deg;
        }

        // Second Quadrant i.e SE
        else if (position2.x > position1.x && position2.y < position1.y)
        {
            float deltaX = position2.x - position1.x;
            return 270 - Mathf.Acos(deltaX / hypotenuse) * Mathf.Rad2Deg;
        }

        // Third Quadrant i.e SW
        else if (position2.x < position1.x && position2.y < position1.y)
        {
            float deltaX = position1.x - position2.x;
            return 90 + Mathf.Acos(deltaX / hypotenuse) * Mathf.Rad2Deg;
        }

        // Fourth Quadrant i.e NW
        else if (position2.x < position1.x && position2.y > position1.y)
        {
            float deltaX = position1.x - position2.x;
            return 90 - Mathf.Acos(deltaX / hypotenuse) * Mathf.Rad2Deg;
        }
        else {
            return 0f;
        }       
    }

    // This checks if two objects are threatened to collide in the future
    public static bool WillCollide(Vector3 object1, Vector2 velocity1, Vector3 object2, Vector2 velocity2)    {
        bool willCollide = false;



        return willCollide;
    }
}

