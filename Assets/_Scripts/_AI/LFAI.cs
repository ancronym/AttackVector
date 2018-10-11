using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;

public class LFAI : MonoBehaviour {

    public enum OrderType { unassigned, patrol, intercept, Sentry, regroup }
    public enum TMProbType { flightBingo, flightInDanger, perimeter, objectiveAttack, objectiveDefence }

    public class TMOldBogie
    {
        public Vector3 position;
        public Vector2 lastHeading;
        public float lastContact;

        TMOldBogie(Vector3 Position, float LastContact)
        {
            position = Position;
            lastContact = LastContact;
        }

        TMOldBogie(Vector3 Position, Vector2 LastHeading,  float LastContact)
        {
            position = Position;
            lastContact = LastContact;
            lastHeading = LastHeading;
        }
    }
    

    public class TMProblem : IComparable<TMProblem>
    {
        public float timeOfDeclaration;
        public TMProbType ProbType;
        public float ProbPriority; // The higher the number, the higher the priority
        public LFAI.Formation problemFormation; // if the problem is associated with a flight, reference GameObject

        // important value, that should always be checked, else the problem could not be solved.
        // the value should be set according to the problem nature and used in repeating solution checking 
        public float solutionParameter; 

        public TMProblem(TMProbType probType, float priority, float solutionValue)
        {
            ProbType = probType;    ProbPriority = priority; solutionParameter = solutionValue;

            timeOfDeclaration = Time.timeSinceLevelLoad;
        }

        public TMProblem(TMProbType probType, float priority, float solutionValue, LFAI.Formation probFormation)
        {
            ProbType = probType; ProbPriority = priority;   problemFormation = probFormation; solutionParameter = solutionValue;

            timeOfDeclaration = Time.timeSinceLevelLoad;
        }

        public int CompareTo(TMProblem other)        {

            return this.ProbPriority.CompareTo(other.ProbPriority);
        }
    }

    public struct FormationReport
    {
        public bool hasWp;
        public List<RadarController.Bogie> bogies;
        public int nrHostiles;        
        public int formationSize;
        public float health;
        public bool fuelIsBingo;
        public float fuelPercentage;
        public VesselAI.FlightStatus flightStatus;

        public FormationReport(
            List<RadarController.Bogie> formationBogies,
            bool wpHas, int hostiles, int flightSize, float formationHealth, 
            bool bingo,float fuelpercentage, VesselAI.FlightStatus status
            )
        {
            hasWp = wpHas; bogies = formationBogies; nrHostiles = hostiles; formationSize = flightSize; health = formationHealth;
            fuelIsBingo = bingo; fuelPercentage = fuelpercentage; flightStatus = status;
        }
    }

    public class Formation
    {
        public GameObject Lead;
        public int nrHostiles;
        public int formationSize;
        public float health;
        public bool fuelIsBingo;
        public float fuelPercentage;
        public OrderType order = OrderType.unassigned;
        public bool carriesTM = false;
        public bool hasWPs = false;
        public GameObject sentryTarget;

        public Formation(GameObject Flead, int hostiles, int flightSize, bool bingo, float fuelpercentage, OrderType Order, bool WPs)
        {
            Lead = Flead; nrHostiles = hostiles; formationSize = flightSize; fuelIsBingo = bingo; fuelPercentage = fuelpercentage; order = Order;
            hasWPs = WPs;
        }

        public Formation(GameObject Flead, int hostiles, int flightSize, bool bingo, float fuelpercentage, OrderType Order, bool WPs, GameObject SentryTarget)
        {
            Lead = Flead; nrHostiles = hostiles; formationSize = flightSize; fuelIsBingo = bingo; fuelPercentage = fuelpercentage; order = Order;
            hasWPs = WPs;
            sentryTarget = SentryTarget;
        }

        public Formation(GameObject Flead)
        {
            Lead = Flead;
        }
    }

    public struct TMAssets
    {
        public int scoutAmount;
        public int fighterAmount;
        public int bomberAmount;
    }

    public class TMObjective
    {
        public bool objectiveSet;

        public GameObject objectiveObject;        
        public bool objectiveIsFriendly;
        public bool isAlive;
        public bool isMoving;
        public float initialHealth;
        public float healthNormalized;

        public TMObjective(GameObject objective, bool friendly, bool alive, bool moving)
        {
            objectiveObject = objective; objectiveIsFriendly = friendly; isAlive = alive; isMoving = moving;

            objectiveSet = true;

            if (objective.GetComponent<ShipController>() != null)
            {
                initialHealth = objective.GetComponent<ShipController>().health;
            } else if (objective.GetComponent<StationAI>() != null)
            {
                initialHealth = objective.GetComponent<StationAI>().health;
            }
            healthNormalized = 1;
        }
        
        public TMObjective()
        {
            objectiveSet = false;
        }
    }
   
    public class Order
    {     
        public OrderType orderType;                
        public RAMP.Route route;
        public GameObject targetObject;

        public Order(OrderType type, RAMP.Route Route)
        {
            orderType = type; route = Route;
        }

        public Order(OrderType type, RAMP.Route Route, GameObject orderObject)
        {
            orderType = type; route = Route; targetObject = orderObject;
        }

        public Order(OrderType type, GameObject orderObject)
        {
            orderType = type; targetObject = orderObject;
        }
    }

    public class Perimeter
    {
        public Vector3 center; // in world space
        public Vector2 heading2D; public float headingFloat; // also in world space, heading2D carries distance info
        public GameObject perimeterAnchor;                
        public float sectorSize;
        public int perimeterRadius = 3;            
        public bool upToDate = false;

        public List<TMSector> Sectors;

        public Perimeter(Vector2 centerCoords, float heading, float SectorSize, GameObject anchor)
        {
            perimeterAnchor = anchor;
            center = centerCoords; headingFloat = heading;
            
            sectorSize = SectorSize;

            InitializeSectors();
        }

        public Perimeter(Vector2 centerCoords, float heading, float SectorSize)
        {
            center = centerCoords; headingFloat = heading;
            
            sectorSize = SectorSize;

            InitializeSectors();
        }        

        void InitializeSectors()
        {
            int amountOfSectors = ((perimeterRadius * 2) + 1) * ((perimeterRadius * 2) + 1);
            Sectors = new List<TMSector>(amountOfSectors);
            float xMax, xMin, yMax, yMin;

            // Sectpr 0,0 will be the left lowest sector
            for (int y = -perimeterRadius; y <= perimeterRadius; y++)
            {
                for (int x = -perimeterRadius; x <= perimeterRadius; x++)
                {
                    // sector x bound computation
                    if (x == 0)
                    {
                        xMax = sectorSize / 2f;
                        xMin = -sectorSize / 2f;
                    }
                    else
                    {
                        xMax = x * sectorSize + sectorSize / 2f;
                        xMin = x * sectorSize - sectorSize / 2f;
                    }

                    // sector y bound computation
                    if (y == 0)
                    {
                        yMax = sectorSize / 2f;
                        yMin = -sectorSize / 2f;
                    }
                    else
                    {
                        yMax = y * sectorSize + sectorSize / 2f;
                        yMin = y * sectorSize - sectorSize / 2f;
                    }

                    Sectors.Add(new TMSector(x, y, xMax,xMin,yMax,yMin));

                }
            }

            // Debug.Log("Sector initialized:" + Sectors.Count);
        }       
    }  

    public class TMSector
    {
        public TMSectorID ID;          
        public int greenCount = 0;
        public int redCount = 0;
        public float importance = 0f;
        public float importanceMet = 0f;

        public float xMax, xMin, yMax, yMin;

        public TMSector(int X, int Y, float xmax, float xmin, float ymax, float ymin)
        {
            ID.x = X;
            ID.y = Y;
            xMax = xmax; xMin = xmin; yMax = ymax; yMin = ymin;
        }
    }

    // Basically just a Vector2D with integer values
    public struct TMSectorID
    {
        public int x;
        public int y;

        public TMSectorID(int X, int Y)
        {
            x = X;
            y = Y;
        }
    }

    public static int GetSectorIndexByIDs(int x, int y, int radius)
    {
        // Translating x and y to 0 base
        x = x + radius;
        y = y + radius;

        return y * (radius * 2 + 1) + x;
    }
}
