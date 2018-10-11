using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TeamManager : MonoBehaviour {
    // Debugging stuff
    static Vector3 GreenPosUpdated;
    static Vector3 RedPosUpdated;

    public bool debug; bool debugOn;
    public GameObject debugWPPreset; GameObject debugWP = null;
    public GameObject debugTMPreset; GameObject debugTM = null;
    public GameObject enemyPosPrefab; GameObject enemyyPosIndicator;
    public GameObject redPlotPrefab;
    public GameObject BlueWpPrefab;
    public GameObject EscortFormationPrefab; GameObject escortFormation = null;

    public Text objectiveText;
    public Text healthText;
    public Text statusText;
    public Text rocText;

    public enum TeamSide        {       green,          red    }    
    public enum TeamMood        {       noContact,      offensive,  cautious,           defensive       }   // translates to danger tolerance often
    public enum TeamState       {       Scouting,       Attacking,  MaintainPerimeter,  Regrouping      }
    public enum MissionState    {       ongoing,        won,        lost    }    

    public TeamSide side;
    public MissionState missionState;
    bool changingState = true;

    // ---------------- Decision and flow parameters
    public TeamMood teamMood;
    private TeamState teamState;
    public float balanceROC; // negative means, things are going down hill and vice versa
    private List<float> balanceLog = new List<float>(10); int BLOGIndex = 0;
    public int fighterKills, fighterLosses;
    public int capitalKills, capitalLosses;
    private float perimeterIntegrity;
    public MissionPlanner.MissionType missionType = MissionPlanner.missionType;
    public Vector3 likelyHostilePos = new Vector3(0f, 0f, 0f);

    public LFAI.TMAssets teamAssets;
    public LFAI.TMObjective objective;
    public Vector3 escortDestination;

    public List<GameObject> players = new List<GameObject>(4);
    public List<GameObject> capitalShips = new List<GameObject>(5);
    //public List<GameObject> FormationLeads = new List<GameObject>(20);
    public List<LFAI.Formation> formations = new List<LFAI.Formation>(20);
    public List<LFAI.Formation> unAssignedFormations = new List<LFAI.Formation>(20);

    public List<LFAI.TMProblem> newProblems = new List<LFAI.TMProblem>(25);
    public List<LFAI.TMProblem> problemsBeingSolved = new List<LFAI.TMProblem>(25);

    public Vector3 enemyGeneralPosition;
    public List<RadarController.Bogie> bogies = new List<RadarController.Bogie>(100); public float bogieTimeout = 5f;
    public List<Vector3> bogieHistory = new List<Vector3>(1000);
    public float sectorSize = 30f;
    private LFAI.Perimeter perimeter;    

    float updateRepeat;

    void Start () {
        if (side == TeamSide.red)               {            TeamManager.RedPosUpdated = gameObject.transform.position;        }
        else if (side == TeamSide.green)        {            TeamManager.GreenPosUpdated = gameObject.transform.position;      }

        ScoreKeeper.kills = 0;
        ScoreKeeper.losses = 0;

        debug = (SettingsManager.GetDebug() == 1);

        debugOn = false;

        perimeter = new LFAI.Perimeter(new Vector2(0, 0), 0, sectorSize);
        perimeter.upToDate = false;

        missionState = MissionState.ongoing;        
        teamMood = TeamMood.noContact;

        fighterKills = 0; fighterLosses = 0; capitalKills = 0; capitalLosses = 0;
        
        if(side == TeamSide.green)
        {
            updateRepeat = MissionMaker.difficulty / 2;
        }else if ( side == TeamSide.red)
        {
            updateRepeat = MissionMaker.difficulty / 2;
        }        

        // Random is used so that different team managers would not run this at the same time

        updateRepeat = Mathf.Clamp(updateRepeat, 0.5f, 2f) + UnityEngine.Random.Range(-0.2f,0.2f);

        // Delay to let the MissionMaker finish adding to Formations etc
        Invoke("FirstRun", 2f);
    }

    private void FirstRun()
    {
        if(side == TeamSide.green)
        {
            enemyyPosIndicator = Instantiate(enemyPosPrefab, gameObject.transform.position, Quaternion.identity);
            enemyyPosIndicator.transform.position = likelyHostilePos;
        } 
        
        

        GetAndProcessReports();

        if (!objective.objectiveSet)
        {
            float initialHealth = 0f;

            foreach(LFAI.Formation formation in formations)
            {
                initialHealth += formation.health;
            }

            foreach(GameObject capital in capitalShips)
            {
                initialHealth += capital.GetComponent<ShipController>().health;
            }

            objective.initialHealth = initialHealth;
        }



        InvokeRepeating("UpdateTeam", 2f, updateRepeat);
        InvokeRepeating("MissionTypeFlow", 2f, updateRepeat);
        InvokeRepeating("RepeatingChecks", 2f, updateRepeat + 0.5f + UnityEngine.Random.Range(-0.2f, 0.2f));

        InvokeRepeating("BogieListRemoveOld", 5f, 2f + UnityEngine.Random.Range(-1f, 1f));

        // Problem solving repeating calls
        InvokeRepeating("SolveTopProblem", 3f, updateRepeat + UnityEngine.Random.Range(-0.1f, 0.1f));
        InvokeRepeating("SortNewProblemList", 10f, updateRepeat + 5f + UnityEngine.Random.Range(-2f, 2f));
        InvokeRepeating("CheckProblemsBeingSolved", 12f, updateRepeat + 10f + UnityEngine.Random.Range(-2f, 2f));
    }

    void Update() {
        // Update the TM position and heading
        if (perimeter.perimeterAnchor != null)
        {
            Vector3 offset = perimeter.perimeterAnchor.transform.position - new Vector3(0, 0, 0);
            offset = offset.normalized * 10f;
            gameObject.transform.position = perimeter.perimeterAnchor.transform.position + offset;
        }
        gameObject.transform.rotation.eulerAngles.Set(0f, 0f, perimeter.headingFloat);
       

        // Check for stale bogies i.e timeout radar contacts
        for (int i = 0; i < bogies.Count; i++)
        {
            if(bogies[i].bogieObject == null)
            {
                bogies.RemoveAt(i);
            }
        }

        if (debug) {
            if (!debugOn)
            {
                debugTM = Instantiate(debugTMPreset, gameObject.transform.position, Quaternion.identity) as GameObject;

                debugOn = true;
            }

            debugTM.transform.position = gameObject.transform.position;
        } else if (!debug)
        {
            if (debugOn)
            {
                Destroy(debugTM);
                if (debugWP)
                {
                    Destroy(debugWP);
                }

                debugOn = false;
            }
        }

        // Debug text management
        if (debug && side == TeamSide.green) {
            if (objectiveText.enabled)
            {
                UpdateDebugTexts();
            }
            else
            {
                objectiveText.enabled = true;
                healthText.enabled = true;
                statusText.enabled = true;
                rocText.enabled = true;
            }    
        }
        else if(!debug && side == TeamSide.green)
        {
            if (objectiveText.enabled)
            {
                objectiveText.enabled = false;
                healthText.enabled = false;
                statusText.enabled = false;
                rocText.enabled = false;
            }
        }
        else { }

    }

    //------------------------------------------------------------------------------------------------------------------------------------
    #region Repeating Checks
    void RepeatingChecks()
    {      
        if(side == TeamSide.red)
        {
           // Debug.Log("Red team mood:" + teamMood + " Bogies: " + bogies.Count);            
            // Debug.Log("Red team mission: " + missionType);
        }

        GetAndProcessReports();

        CheckObjective();

        CheckProgress();        

        CheckPerimeter();

        UpdateLikelyPos();

        PaintBogies();
    }   

    void PaintBogies()
    {
        if (side == TeamManager.TeamSide.green)
        {
            foreach (RadarController.Bogie b in bogies)
            {
                if (b.bogieObject != null)
                {
                    GameObject plot = Instantiate(redPlotPrefab, gameObject.transform.position, Quaternion.identity) as GameObject;
                    plot.GetComponent<PlotScript>().SetUpPlot(b.bogieObject, updateRepeat * 3f);
                }
            }
        }
    }

    // Appears to work
    void CheckObjective()
    {
        // if there ever was an objective, check if it is alive and if the game is won/lost
        if (objective.objectiveSet) 
        {
            if (objective.objectiveObject != null)
            {
                if (objective.objectiveObject.GetComponent<ShipController>() != null)
                {
                    objective.healthNormalized = objective.objectiveObject.GetComponent<ShipController>().health / objective.initialHealth;
                } else if (objective.objectiveObject.GetComponent<StationAI>() != null)
                {
                    objective.healthNormalized = objective.objectiveObject.GetComponent<StationAI>().health / objective.initialHealth;
                }
            }
            else
            {
                if (objective.objectiveIsFriendly)
                {
                    missionState = MissionState.lost;
                } else if (!objective.objectiveIsFriendly)
                {
                    missionState = MissionState.won;
                }
            }
        }
        else
        {
            if(formations.Count == 0 && capitalShips.Count == 0)
            {
                missionState = MissionState.lost;
            }
            else
            {
                float health = 0f;

                foreach (LFAI.Formation formation in formations)
                {
                    health += formation.health;
                }

                foreach (GameObject capital in capitalShips)
                {
                    health += capital.GetComponent<ShipController>().health;
                }

                objective.healthNormalized = health / objective.initialHealth;
            }


        }        
    }

    // Updates balanceROC float
    void CheckProgress()
    {
        BLOGIndex++;
        if(BLOGIndex >= balanceLog.Capacity - 1) { BLOGIndex = 0; }


        if(fighterLosses == 0 && fighterKills == 0)
        {
            balanceLog.Add(0f);
            return;
        } else if(fighterLosses != 0 && fighterKills == 0)
        {
            balanceLog.Add( -1f);
        } else if(fighterLosses == 0 && fighterKills != 0)
        {
            balanceLog.Add(1f);
        }
        else
        {
            balanceLog.Add(fighterKills / fighterLosses);
        }

        float average = 0f;

        for(int i = 0; i < balanceLog.Count; i++)
        {
            average += balanceLog[i];
        }
        average = average / balanceLog.Count;

        float deviation = 0f;
        for (int i = 0; i < balanceLog.Count; i++)
        {
            deviation += Mathf.Abs(average - balanceLog[i]);
        }
        deviation = deviation / balanceLog.Count;

        balanceROC = average;
       
    }

    void CheckPerimeter()
    {
        if (!perimeter.upToDate) {
            perimeter.upToDate = ReassignSectorPriorities();
            if (!perimeter.upToDate)
            {
                Debug.Log("Reassigning sector priorities failed.");
            }
        }

        List<GameObject> bogieGOs = new List<GameObject>(bogies.Capacity);
        NTools.Disposition enemyDisposition;       

        // determine enemy heading range etc i.e disposition
        if (bogies.Count != 0)
        {          

            for (int i = 0; i < bogies.Count; i++)
            {
                if (bogies[i].bogieObject != null)
                {
                    bogieGOs.Add(bogies[i].bogieObject);
                }               
            }


            enemyDisposition = NTools.AnalyseGroupDisposition(
                perimeter.center,
                perimeter.headingFloat,
                bogieGOs
                );

            enemyGeneralPosition = enemyDisposition.centerOfMass;
        }
        else
        {
            enemyGeneralPosition = new Vector3(0f, 0f, 0f);
        }


        //TODO Optimize - determines perimeter anchor, heading2D and headingFloat. Is gud.
        if (objective.objectiveSet)
        {
            // If this happens, the game is lost or won, which will be caught by CheckObjective method
            if (!objective.objectiveObject) { return; }

            if (objective.objectiveIsFriendly)
            {
                perimeter.perimeterAnchor = objective.objectiveObject;

                perimeter.heading2D = new Vector2(
                    enemyGeneralPosition.x - perimeter.perimeterAnchor.transform.position.x,
                    enemyGeneralPosition.y - perimeter.perimeterAnchor.transform.position.y
                    );
                perimeter.headingFloat = NTools.HeadingFromVector(perimeter.heading2D);
            } else if (!objective.objectiveIsFriendly)
            {
                perimeter.perimeterAnchor = DeterminePerimeterAnchor();
                if (perimeter.perimeterAnchor != null)
                {
                    perimeter.heading2D = new Vector2(
                       enemyGeneralPosition.x - perimeter.perimeterAnchor.transform.position.x,
                       enemyGeneralPosition.y - perimeter.perimeterAnchor.transform.position.y
                       );
                    perimeter.headingFloat = NTools.HeadingFromVector(perimeter.heading2D);
                }
                else
                {
                    missionState = MissionState.lost;
                }
            }
        }
        else
        {
            perimeter.perimeterAnchor = DeterminePerimeterAnchor();
            if (perimeter.perimeterAnchor != null)
            {
                perimeter.heading2D = new Vector2(
                       enemyGeneralPosition.x - perimeter.perimeterAnchor.transform.position.x,
                       enemyGeneralPosition.y - perimeter.perimeterAnchor.transform.position.y
                       );
                perimeter.headingFloat = NTools.HeadingFromVector(perimeter.heading2D);
            }
            else
            {
                missionState = MissionState.lost;
            }
        }



    }

    // Will not look for objective objects, only checks which capital or fighter is nr. 1
    private GameObject DeterminePerimeterAnchor()
    {
        if (capitalShips.Count != 0)        {
            
            return capitalShips[0];
        }
        else
        {
            // this parses all formations and if noone carries TM, will set the first available
            // if there is a TM carrier, sets others via implied logic to not carry TM
            if (formations.Count != 0)
            {
                if (!formations[0].carriesTM)
                {                 
                    formations[0].carriesTM = true;
                }

                return formations[0].Lead;
            }    else if (players.Count != 0)
            {
                return players[0];
            }        
        }

        return null;
    }

    void UpdateLikelyPos()
    {
        float rndX = Random.Range(-20f, 20f);
        float rndY = Random.Range(-20f, 20f);

        switch (side)
        {
            case TeamSide.green:
                TeamManager.GreenPosUpdated = gameObject.transform.position;
                likelyHostilePos = RedPosUpdated + new Vector3(rndX, rndY, 0f);
                enemyyPosIndicator.transform.position = likelyHostilePos;
                break;
            case TeamSide.red:
                TeamManager.RedPosUpdated = gameObject.transform.position;
                break;
        }


    }

    // untested ...  Preetty basic approach, will get the job done initially
    bool ReassignSectorPriorities()
    {
        if (perimeter.Sectors.Count != 49) {
            Debug.LogError("Perimeter sectors wrong amount: " + perimeter.Sectors.Count);
            return false;
        }

        // Normalizing team values from 0 to 3 and reversing the value
        // So 00 ie no contact scouting means full forwardness and max spread
        float forwardness = 1f - (float)teamMood/3;
        float spread = 1f - (float)teamState/3;

        float xPos, xImportance;
        float yPos, yImportance;
        float sectorImportance;
        

        foreach(LFAI.TMSector sector in perimeter.Sectors)
        {
            // getting normalized local position from center.
            xPos = (float)sector.ID.x / (float)perimeter.perimeterRadius;
            yPos = (float)sector.ID.y / (float)perimeter.perimeterRadius;

            xImportance = (1 - Mathf.Abs(xPos)) * spread;
            yImportance = (1 - Mathf.Abs(yPos)) + yPos * forwardness;

            // Just in case, clamping both values
            xImportance = Mathf.Clamp(xImportance, 0f, 1f);
            yImportance = Mathf.Clamp(yImportance, 0f, 1f);
            
            sectorImportance = (xImportance + yImportance) / 2f;
        }

        return true;
    }

    #endregion

    #region Objective Flow

    private void MissionTypeFlow()
    {
        switch (missionType)
        {
            case MissionPlanner.MissionType.attackBase:
                AttackBase();
                break;

            case MissionPlanner.MissionType.defendBase:
                DefendBase();
                break;

            case MissionPlanner.MissionType.destroyEnemyVessels:
                DestroyVessels();
                break;

            case MissionPlanner.MissionType.escort:
                Escort();
                break;
        }
    }

    private void AttackBase()
    {
        switch (teamMood)
        {
            case TeamMood.noContact:
                if (capitalShips.Count == 0)
                {
                    bool allHaveAWP = true;

                    // Check if all flights have intercept WPs
                    foreach (LFAI.Formation f in formations)
                    {
                        if (!f.hasWPs)
                        {
                            allHaveAWP = false;
                        }
                    }

                    // if there's no contact and even one of the flights has no intercept order, give new to all
                    if (!allHaveAWP)
                    {
                        // Create list of Flight lead GO-s
                        List<GameObject> leads = new List<GameObject>(formations.Count);
                        foreach (LFAI.Formation f in formations) { leads.Add(f.Lead); }

                        List<RAMP.Route> routes = RAMP.PlanDestroyVessels(likelyHostilePos, leads);

                        for (int i = 0; i < formations.Count; i++)
                        {
                            formations[i].Lead.GetComponent<VesselAI>().GiveOrder(new LFAI.Order(LFAI.OrderType.intercept, routes[i]));
                        }
                    }
                }
                else
                {
                    bool hasWP = true;

                    if (capitalShips[0].GetComponent<VesselAI>().wpList.Count == 0) { hasWP = false; }

                    if (!hasWP)
                    {
                        RAMP.Route route = new RAMP.Route(1);

                        RAMP.Waypoint wp = new RAMP.Waypoint(
                            new Vector2(likelyHostilePos.x, likelyHostilePos.y),
                            20f,
                            false,
                            false
                            );

                        route.wpList.Add(wp);

                        capitalShips[0].GetComponent<VesselAI>().GiveOrder(new LFAI.Order(LFAI.OrderType.intercept, route));

                        // Instantiate the escort formation and attach it to the escortable, if it has none
                        if (escortFormation == null)
                        {
                            escortFormation = Instantiate(EscortFormationPrefab, capitalShips[0].transform.position, Quaternion.identity) as GameObject;
                            escortFormation.transform.parent = capitalShips[0].transform;
                            escortFormation.transform.localPosition = new Vector3(0f, 0f, 0f);
                            escortFormation.transform.localEulerAngles = new Vector3(0f, 0f, 0f);
                        }

                        foreach (LFAI.Formation formation in formations)
                        {
                            if (formation.Lead.GetComponent<VesselAI>().ReportFlightStatus() != VesselAI.FlightStatus.sentry)
                            {
                                GameObject orderObj = null;

                                for (int i = 0; i < escortFormation.GetComponent<FormationScript>().positions.Count; i++)
                                {
                                    if (!escortFormation.GetComponent<FormationScript>().positions[i].GetComponent<Position>().posOccupied)
                                    {
                                        orderObj = escortFormation.GetComponent<FormationScript>().positions[i];
                                        escortFormation.GetComponent<FormationScript>().positions[i].GetComponent<Position>().posOccupied = true;
                                        escortFormation.GetComponent<FormationScript>().positions[i].GetComponent<Position>().wpAssignedTo = formation.Lead;
                                        break;
                                    }
                                }

                                LFAI.Order order = new LFAI.Order(LFAI.OrderType.Sentry, orderObj);
                                formation.Lead.GetComponent<VesselAI>().GiveOrder(order);
                            }
                        }

                    }
                }
                break;
            case TeamMood.offensive:

                break;

            case TeamMood.cautious:

                break;

            case TeamMood.defensive:

                break;
        }
    }

    private void DefendBase()
    {
        
        if(teamMood == TeamMood.noContact)
        {
            bool allHaveAWP = true;

            // Check if all flights have intercept WPs
            foreach (LFAI.Formation f in formations)
            {
                if (!f.hasWPs)
                {
                    allHaveAWP = false;
                }
            }

            // if there's no contact and even one of the flights has no intercept order, give new to all
            if (!allHaveAWP)
            {

                Debug.Log("All don't have WP");

                // List<RAMP.Route> routes = new List<RAMP.Route>(formations.Count);

                Vector3 objectivePos = new Vector3(0f,0f,0f);

                if (objective.objectiveObject)
                {
                    objectivePos = objective.objectiveObject.transform.position;
                }

                bool CW = false;
                for(int j = 0; j < formations.Count; j++)
                {
                    LFAI.Order order = new LFAI.Order(
                        LFAI.OrderType.patrol, 
                        RAMP.PlanPatrols(
                            objectivePos, 
                            likelyHostilePos,
                            50f,
                            j+1,
                            CW
                        ));

                    CW = !CW;
                                        
                    formations[j].Lead.GetComponent<VesselAI>().GiveOrder(order);
                    formations[j].hasWPs = formations[j].Lead.GetComponent<VesselAI>().ReportHasWp();

                    /*
                    for (int i = 0; i < order.route.wpList.Count; i++)
                    {
                        
                        Vector3 debugPoint = new Vector3(order.route.wpList[i].wpCoordinates.x, order.route.wpList[i].wpCoordinates.y,0f);
                        GameObject bug = Instantiate(debugWPPreset, debugPoint, Quaternion.identity) as GameObject;
                        bug.transform.parent = bug.transform;

                        Debug.Log("WP painted:  " + debugPoint);                 
                    }
                    */
                }
            }
        }
    }

    private void DestroyVessels()
    {
        if(teamMood == TeamMood.noContact)
        {
            if(capitalShips.Count == 0)
            {
                bool allHaveAWP = true;

                // Check if all flights have intercept WPs
                foreach(LFAI.Formation f in formations)
                {
                    if(!f.hasWPs)
                    {
                        allHaveAWP = false;
                    }
                }

                

                // if there's no contact and even one of the flights has no intercept order, give new to all
                if (!allHaveAWP)
                {
                    // Create list of Flight lead GO-s
                    List<GameObject> leads = new List<GameObject>(formations.Count);
                    foreach (LFAI.Formation f in formations) { leads.Add(f.Lead);       }
                    // foreach (GameObject p in players) { leads.Add(p);                   }

                    // Get list of routes i.e wplists                    
                    List<RAMP.Route> routes = new List<RAMP.Route>(formations.Count);
                    routes = RAMP.PlanDestroyVessels(likelyHostilePos, leads);

                    for (int i = 0; i < formations.Count; i++)
                    {
                        formations[i].Lead.GetComponent<VesselAI>().GiveOrder(new LFAI.Order(LFAI.OrderType.intercept, routes[i]));
                        //Debug.Log(i.ToString() + routes[i].route[0].wpCoordinates + " " + routes[i].route[1].wpCoordinates);
                    }
                }
            } else
            {
                bool hasWP = true;

                if(capitalShips[0].GetComponent<VesselAI>().wpList.Count == 0) { hasWP = false; }

                if (!hasWP)
                {
                    RAMP.Route route = new RAMP.Route(1);

                    RAMP.Waypoint wp = new RAMP.Waypoint(
                        new Vector2(likelyHostilePos.x, likelyHostilePos.y),
                        20f,
                        false,
                        false
                        );

                    route.wpList.Add(wp);

                    capitalShips[0].GetComponent<VesselAI>().GiveOrder(new LFAI.Order(LFAI.OrderType.intercept, route));
                }
            }


        }
        else
        {
            
        }
    }

    private void Escort()
    {
        Debug.Log("Escort");
        switch (teamMood)
        {
            case TeamMood.noContact:
        
            if (objective.objectiveObject)
            {
                    // Give waypoints to the escortable
                if (!objective.objectiveObject.GetComponent<VesselAI>().ReportHasWp())
                {
                    objective.objectiveObject.GetComponent<VesselAI>().teamManager = gameObject.GetComponent<TeamManager>();

                    GameObject civWp = Instantiate(BlueWpPrefab, escortDestination, Quaternion.identity) as GameObject;
                    civWp.transform.parent = civWp.transform;                    

                    RAMP.Route route =  RAMP.GetEscortableRoute(objective.objectiveObject.transform.position, escortDestination, likelyHostilePos);
                    

                    LFAI.Order order = new LFAI.Order(LFAI.OrderType.patrol, route);

                    objective.objectiveObject.GetComponent<VesselAI>().GiveOrder(order);
                }

                // Instantiate the escort formation and attach it to the escortable, if it has none
                if(escortFormation == null)
                    {
                        escortFormation = Instantiate(EscortFormationPrefab, objective.objectiveObject.transform.position, Quaternion.identity) as GameObject;
                        escortFormation.transform.parent = objective.objectiveObject.transform;
                        escortFormation.transform.localPosition = new Vector3(0f, 0f, 0f);
                    }                

                foreach(LFAI.Formation formation in formations)
                    {
                        if(formation.Lead.GetComponent<VesselAI>().ReportFlightStatus() != VesselAI.FlightStatus.sentry)
                        {
                            GameObject orderObj = null;

                            for(int i = 0; i < escortFormation.GetComponent<FormationScript>().positions.Count; i++)
                            {
                                if (!escortFormation.GetComponent<FormationScript>().positions[i].GetComponent<Position>().posOccupied)
                                {
                                    orderObj = escortFormation.GetComponent<FormationScript>().positions[i];
                                    escortFormation.GetComponent<FormationScript>().positions[i].GetComponent<Position>().posOccupied = true;
                                    escortFormation.GetComponent<FormationScript>().positions[i].GetComponent<Position>().wpAssignedTo = formation.Lead;
                                    break;
                                }
                            }

                            LFAI.Order order = new LFAI.Order(LFAI.OrderType.Sentry, orderObj);
                            formation.Lead.GetComponent<VesselAI>().GiveOrder(order);
                        }
                    }
            }

                break;
            case TeamMood.offensive:

                break;

            case TeamMood.cautious:

                break;

            case TeamMood.defensive:

                break;
        }
    }


    #endregion

    //------------------------is this even relevant ...-----------------------------------------------------------------------------------
    #region TeamState flow // is this even relevant ... might be
    void UpdateTeam(){

        // Passes all team bogies to all formations and players and Capitals
        ShareBogies();

        switch (teamState)
        {
            case TeamState.Scouting:
                if (changingState)
                {
                    perimeter.upToDate = false;
                    changingState = false;
                }
                Scout();
                break;
            case TeamState.Attacking:
                if (changingState)
                {
                    perimeter.upToDate = false;
                    changingState = false;
                }
                Attack();
                break;
            case TeamState.MaintainPerimeter:
                if (changingState)
                {
                    perimeter.upToDate = false;
                    changingState = false;
                }
                MaintainPerimeter();
                break;
            case TeamState.Regrouping:
                if (changingState)
                {
                    perimeter.upToDate = false;
                    changingState = false;
                }
                Regroup();
                break;
        }
    }   

    void Scout()
    {

    }

    void Attack()
    {

    }

    void MaintainPerimeter()
    {

    }

    void Regroup()
    {

    }

    #endregion

    //------------------------------------------------------------------------------------------------------------------------------------
    #region Problem Solving

        /* Information The method accesses from globals:
         *      Bogies list
         *      Formations
         *      Objective info
         *      BROC
         * 
         * Should be used for the TM to decide if a general attack is reasonable
         */
    private bool ShouldIAttack()
    {
        bool ShouldAttack = false;



        return ShouldAttack;
    }

        // The problem with the lowest priority will have index 0
        // highest priority will be top of the list i.e highest index
    void SortNewProblemList()
    {
        if (newProblems.Count == 0) { return; }

        newProblems.Sort();
    }

    void CheckProblemsBeingSolved()
    {
        if (problemsBeingSolved.Count == 0) { return; }

    }

    void SolveTopProblem()
    {        
        if(newProblems.Count == 0) { return; }

        LFAI.TMProblem problem = newProblems[newProblems.Count - 1];
        float solutionParameter;

        switch (problem.ProbType)
        {
            case LFAI.TMProbType.flightBingo:
                solutionParameter = SolveFlightBingo(problem);
                break;
            case LFAI.TMProbType.flightInDanger:
                solutionParameter = SolveFlightDanger(problem);
                break;            
            case LFAI.TMProbType.perimeter:
                solutionParameter = SolvePerimeter(problem);
                break;

            case LFAI.TMProbType.objectiveAttack:
                solutionParameter = SolveObjectiveAttack(problem);
                break;
            case LFAI.TMProbType.objectiveDefence:
                solutionParameter = SolveObjectiveDefence(problem);
                break;
        }


        problemsBeingSolved.Add(problem);
        newProblems.RemoveAt(newProblems.Count - 1);

    }

    float SolveFlightBingo(LFAI.TMProblem problem) {
        float solutionParameter = 0f;

        // determine sector and its priority

        // evaluate if the flight can leave

        // if the flight can leave, parse available fuelers for closest

        // give move order to closest fueler
            // if not found, ignore

        // if the flight cant leave look for closest fueler
            // if found, put flight into refuel list
            // if not found, ignore




        return solutionParameter;
    }

    float SolveFlightDanger(LFAI.TMProblem problem)
    {
        float solutionParameter = 0f;






        return solutionParameter;
    }    
    
    float SolvePerimeter(LFAI.TMProblem problem)
    {
        float solutionParameter = 0f;






        return solutionParameter;
    }

    float SolveObjectiveAttack(LFAI.TMProblem problem)
    {
        float solutionParameter = 0f;






        return solutionParameter;
    }

    float SolveObjectiveDefence(LFAI.TMProblem problem)
    {
        float solutionParameter = 0f;






        return solutionParameter;
    }


    #endregion

    #region Helper and Tool methods

    // untested
    // determine sector by world coordinates, returning 401 means out of perimeter, 404 generic error
    private int DetermineSector(GameObject plot)    {
        
        Vector3 plotLocal = gameObject.transform.InverseTransformPoint(plot.transform.position);
        

        float perimeterRange = perimeter.sectorSize * perimeter.perimeterRadius + 0.5f * perimeter.sectorSize;
        if ( Vector3.SqrMagnitude(plotLocal) > perimeterRange * perimeterRange)
        {
            return 401; // out of perimeter
        }

        for (int id = 0; id < perimeter.Sectors.Count; id++)
        {
           if (plotLocal.x < perimeter.Sectors[id].xMax && plotLocal.x > perimeter.Sectors[id].xMin)
            {
                if(plotLocal.y > perimeter.Sectors[id].xMin && plotLocal.y < perimeter.Sectors[id].yMax)
                {
                    return id;
                }
            }
        }

        return 404;
    }

    private void ComputePerimeterIntegrity()
    {
        // Go through formations, 
            // determine sector 
            // get formation ship count, add to sector

        // go through bogies
            // determine sector
            // add +1 to sector red count

    }


    #endregion

    //------------------------------------------------------------------------------------------------------------------------------------
    #region Communication with other objects and other maintenance 
    public void NewFormationLead(GameObject previousLead, GameObject newLead)
    {
        fighterLosses++;

        if(formations.Count == 0) { Debug.LogWarning("Error with formations list"); return; }
        foreach(LFAI.Formation formation in formations)
        {
            if(formation.Lead == previousLead)
            {
                formation.Lead = newLead;
                formation.formationSize--;

                break;
            }
        }
    }

    public void FormationDestroyed(GameObject lead)
    {
        fighterLosses++;
        if (side == TeamSide.green)
        {
            ScoreKeeper.losses++;
        }

        if (formations.Count == 0) { Debug.LogWarning("Error with formations list"); return; }
        for(int i = 0; i < formations.Count; i++)
        {
            if(formations[i].Lead == lead)
            {
                formations.RemoveAt(i);
                Debug.Log("Formation died.");
            }
        }
            
    }

    public void LostWingman()
    {
        fighterLosses++;
        if (side == TeamSide.green)
        {
            ScoreKeeper.losses++;
        }
            
    }

    public void ReportKill(GameObject target)      
    {
        if(side == TeamSide.green)
        {
            ScoreKeeper.kills++;
        }
        

        ShipController.ShipType targetType = target.GetComponent<ShipController>().shipType;

        switch (targetType)
        {
            case ShipController.ShipType.capital:
                capitalKills++;
                break;
            case ShipController.ShipType.fighter:
                fighterKills++;
                break;
            case ShipController.ShipType.bomber:
                fighterKills++;
                break;
            case ShipController.ShipType.scout:
                fighterKills++;
                break;
            case ShipController.ShipType.support:
                fighterKills++;
                break;
        }
    }

    public void ReportCivWpReached()
    {
        missionState = MissionState.won;
    }

    public void ReportPlayerDeath(GameObject player)
    {
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i] == player)
            {
                players.RemoveAt(i);
            }
        }

        if (players.Count == 0)
        {
            missionState = MissionState.lost;
        }
    }

    public float GetTeamHeading()
    {
        return perimeter.headingFloat;
    }

    // untested, This gets and handles reports from all flights
    private void GetAndProcessReports()
    {
        LFAI.FormationReport report;

        // problem prioritization parameters, computed every time for being up to date
        float bingoPriority = 0.5f;
        float dangerTolerance = (float)teamMood / 3;

        foreach(GameObject player in players)
        {
            report = player.GetComponent<PlayerController>().ReportRequest();

            foreach (RadarController.Bogie bogie in report.bogies)
            {
                bool bogieListed = false;

                for (int i = 0; i < bogies.Count; i++)
                {
                    if (bogie.bogieObject == bogies[i].bogieObject)
                    {
                        bogieListed = true;
                        break;
                    }
                }

                if (!bogieListed)
                {
                    bogies.Add(bogie);
                }
            }
        }

        foreach(LFAI.Formation formation in formations)
        {
            report = formation.Lead.GetComponent<VesselAI>().ReportRequest();

            // update formation info based on it's report
            formation.health = report.health;
            formation.nrHostiles = report.nrHostiles;
            formation.formationSize = report.formationSize;
            formation.fuelIsBingo = report.fuelIsBingo;
            formation.fuelPercentage = report.fuelPercentage;
            formation.hasWPs = report.hasWp;

            float danger = EvaluateDanger(report.formationSize, report.nrHostiles);

            foreach(RadarController.Bogie bogie in report.bogies)
            {
                bool bogieListed = false;

                for(int i = 0; i < bogies.Count; i++)
                {
                    if(bogie.bogieObject == bogies[i].bogieObject)
                    {
                        bogieListed = true;
                        break;
                    }
                }

                if (!bogieListed)
                {
                    bogies.Add(bogie);
                }
            }

            // Add detected problems to problems list
            if (report.fuelIsBingo)
            {
                if (!CheckProblemDupe(LFAI.TMProbType.flightBingo, formation))
                {
                    newProblems.Add(new LFAI.TMProblem(
                        LFAI.TMProbType.flightBingo,
                        bingoPriority,
                        90,              // if fuel is again over 90% average for flight, problem is solved
                        formation
                        ));
                }
            }            

            if (danger > dangerTolerance)
            {
                if (!CheckProblemDupe(LFAI.TMProbType.flightInDanger, formation))
                {
                    newProblems.Add(new LFAI.TMProblem(
                        LFAI.TMProbType.flightInDanger,
                        1f - dangerTolerance,       // the higher the threat tolerance, the lower the priority and vice versa
                        dangerTolerance,            // problem will be solved, if the threat level is lower than tolerance
                        formation
                        ));
                }
            }

        }

        if(teamMood == TeamMood.noContact && bogies.Count != 0)
        {
            teamMood = TeamMood.offensive;            
        }

        DetermineUnassignedFormations();
    }

    void DetermineUnassignedFormations()
    {
        unAssignedFormations.Clear();

        for (int i = 0; i < formations.Count; i++)
        {
            if (formations[i].order == LFAI.OrderType.unassigned)
            {
                unAssignedFormations.Add(formations[i]);
            }
        }
    }

    // untested, returns true, if problem already exists in either new problems or ones being solved
    bool CheckProblemDupe(LFAI.TMProbType newType, LFAI.Formation formation)
    {
        foreach(LFAI.TMProblem problem in newProblems)
        {
            if(problem.problemFormation == formation && problem.ProbType == newType)
            {
                return true;                
            }
        }

        foreach (LFAI.TMProblem problem in problemsBeingSolved)
        {
            if (problem.problemFormation == formation && problem.ProbType == newType)
            {
                return true;
            }
        }

        return false;
    }

    // untested, returns danger between 0 and 1. 0 being no danger, 1 being three times or more baddies as friendlies.
    private float EvaluateDanger(int formationSize, int nrHostiles)
    {
        float danger = 0f;

        danger = ((float)nrHostiles / (float)formationSize) * 0.33f; 
        // if nrs are even, then danger is 0.33
        // max danger is 1, when there are three times as many hostiles than friendlies
       
        return Mathf.Clamp(danger,0f,1f);
    }

    // If bogie is new, add to TMs bogie list, else replace preexisting with new data
    // Upon first spot, if the spotted vessel is capital ship, the team goes into defence    
    // UNUSED
    public void BogieSpotted(RadarController.Bogie bogie) {
        if(teamMood == TeamMood.noContact) {
            if (bogie.bogieObject.GetComponent<ShipController>().shipType == ShipController.ShipType.capital)
            {
                teamMood = TeamMood.cautious;
            }
            else
            {
                teamMood = TeamMood.offensive;
            }
        }

        // Check if the gameObject is already described as a plot, if not, add to list of known contacts
        bool addToList = true;

        if (bogies.Count != 0)
        {
            for (int i =0; i < bogies.Count; i++)
            {
                // Debug.Log("Bogie count: " + bogies.Count + " i: " + i);

                if (bogie.bogieObject == bogies[i].bogieObject)
                {
                    bogies[i].timeOfContact = bogie.timeOfContact;
                    addToList = false;
                }
            }
        }

        if (addToList) { bogies.Add(bogie); }
    }


    // Passes all team bogies to all flights
    private void ShareBogies()
    {
        foreach(LFAI.Formation f in formations)
        {
            if(f.Lead != null)
            {
                f.Lead.GetComponent<VesselAI>().ShareBogies(bogies);
            }            
        }

        foreach (GameObject p in players)
        {
            if (p != null)
            {
                p.GetComponent<PlayerController>().ShareBogies(bogies);
            }
        }

        foreach(GameObject c in capitalShips)
        {
            if(c != null)
            {
                c.GetComponent<VesselAI>().ShareBogies(bogies);
            }
        }
    }

    public GameObject GetObjectiveObject()
    {
        if(objective.objectiveObject != null)
        {
            return objective.objectiveObject;
        }else
        {
            return null;
        }
    
    }
    
    // Removes bogies when they have haven't been spotted in a while
    void BogieListRemoveOld()
    {
        if (bogies.Count == 0) { return; }

        float currentTime = Time.timeSinceLevelLoad;

        for (int i = 0; i < bogies.Count; i++)
        {
            if(bogies[i].timeOfContact < currentTime - bogieTimeout)
            {
                if (bogies[i].bogieObject)
                {
                    bogieHistory.Add(bogies[i].bogieObject.transform.position);
                }                
                bogies.RemoveAt(i);                
            }
        }
    }

    public void OrderRequest(GameObject requester, LFAI.FormationReport report)
    {

    }

    public List<GameObject> FriendlyLeadsRequest(GameObject requester)
    {
        List<GameObject> friendlies = new List<GameObject>(20);
        float sqrsep = 1600f;

        foreach(LFAI.Formation f in formations)
        {
            if (f.Lead != requester)
            {
                if((requester.transform.position - f.Lead.transform.position).sqrMagnitude < sqrsep)
                {
                    friendlies.Add(f.Lead);
                }                
            }
        }

        foreach (GameObject p in players)
        {
            if (p != requester)
            {
                if ((requester.transform.position - p.transform.position).sqrMagnitude < sqrsep)
                {
                    friendlies.Add(p);
                }
            }
        }

        return friendlies;
    }
    #endregion

    #region Debugging stuff

    private void UpdateDebugTexts()
    {
        string state = "";
        switch (missionState)
        {
            case MissionState.ongoing:
                state = "Ongoing";
                break;
            case MissionState.won:
                state = "Won";
                break;
            case MissionState.lost:
                state = "Lost";
                break;
        }

        if (objective.objectiveObject) {
            objectiveText.text = "Objective name: " + objective.objectiveObject.name + " State: " + state;
        }
        else
        {
            objectiveText.text = "Formations: " + formations.Count + " State: " + state;
        }

        healthText.text = " Object health: " + objective.healthNormalized + "TM pos: " + gameObject.transform.position.ToString() + "TM hdg: " + perimeter.headingFloat;

        statusText.text = "Mood: " + teamMood.ToString() + " status: " + teamState.ToString() + "Bogie count: " + bogies.Count;

        float KD = 0;
        if (fighterLosses != 0)
        {
            KD = fighterKills / fighterLosses;
        }
        rocText.text = "KD: " + KD.ToString() + " ROC: " + balanceROC.ToString();

    }

    #endregion
}
