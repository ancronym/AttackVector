using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using UnityEngine.UI;

public class VesselAI : MonoBehaviour {
    // ---------- ----------
    #region Debugging stuff    
    LineRenderer waypointLine;
    LineRenderer waypointLine2;
    public GameObject DebugLine2D;
    public TextMesh statusText;
    #endregion
    //-------------------------

    // -------------------------
    #region Ship and other gameObject stuff
    ShipController ship;
    LineRenderer velocityVector;
    

    #endregion
    // -------------------------

    // --------- AI parameters, use values from 0.0f to 1.0f
    float accuracy = 0.5f;
    float decisionLag = 0.3f;
    float agression = 0.5f;
    float wit = 0.5f;
    float repeatingCheckRate = 0.2f;
    // -----------------------------------------------

    /* -----------------------------------------------
     * The waypoint class instance stores a Vector2 coordinates, a float radius and a movingWp bool
     * Usually only use of 3 waypoints is planned, but a ten slot list has buffer for objectavoidance waypoints
     ----------------------------------------------- */
    public List<RAMP.Waypoint> wpList = new List<RAMP.Waypoint>(10);
    int wpIndex;


    // -------------------------------
    #region Movement and collision avoidance stuff

    enum MoveType { simple, firing};
    MoveType moveType;
    float headingToWaypoint;    
    float desiredLookHeading; Vector2 desiredVector;
    float desiredSpeed;

    // Between 0f and 0.3 f, will be added to final thrust command to ship
    float rightTCASThrust = 0f; float leftTCASThrust = 0f; float fwdTCASThrust = 0f; float backTCASThrust = 0f;
    float intendedThrust = 0f; float currentThrust; float patrolThrust = 0.3f; float dangerThrust = 2f; float combatThust = 1.5f;
    float lateralCorrectionThrust = 1f;   

    public float patrolSpeedMax = 3f, combatSpeedMax = 5f, interceptSpeed = 4f, emergencySpeedMax = 10f, asteroidSpeed = 0.5f;
    
    
    
    public List<NTools.CollisionThreat> collisionThreatList = new List<NTools.CollisionThreat>(50);
    public List<NTools.CollisionThreat> TCASThreatList = new List<NTools.CollisionThreat>(10);

    Vector2 TCASthreatPosition; Vector2 TCASthreatVelocity; // in order to save creating new ones in TCASCheck
    Vector2 collisonThreatPosition; Vector2 collisionThreatVelocity; // in order to save creating new ones in collisionAvoidance

    public float minSeparation = 2f; // Taken into account in CollisionAvoidance()    

    CircleCollider2D visionCollider;
    CapsuleCollider2D tcas;
    LayerMask TCASLayerMask;
    public string[] TCASLayers;
    #endregion
    

    // -----------------------------------
    #region Communication with TeamManager
        
    // These will be set by MissionMaker
    public TeamManager.TeamSide teamSide;
    public TeamManager teamManager;

    #endregion
    // -----------------------------------


    #region AI local states and parameters
    GameObject sentryTarget;


    public enum StatusInFormation { lead, Position2, Position3, Position4};
    public StatusInFormation statusInFormation;

    public enum WingmanState { inFormation, followingOrder };
    public WingmanState wingmanState = WingmanState.inFormation;

    public enum FlightStatus { patrolling, intercepting, sentry, engaging, retreating};
    public FlightStatus flightStatus;
    bool flightStatusIsChanging; // used to check if resetting movement parameters etc is required

    // Loiter parameters
    float loiterStart;  bool loiterOngoing = false;

    #endregion

    // --------------------------------------
    #region Fighting Parameters and info
    public GameObject currentTarget = null; float headingToTarget = 0f; float leadToTarget = 0f; float targetDistance;
    bool clearToFire = false; bool headingClearSet; public LayerMask raycastLayers;
    private List<Vector3> WepRangeAmmo = new List<Vector3>(4);
    // For incoming locks
    List<GameObject> lockSources = new List<GameObject>(50);

    bool firing = false;

    RAMP.AttackTypeF attackType;
    RAMP.AttackPlanF attackPlan = new RAMP.AttackPlanF(false);

    public List<RadarController.Bogie> formationBogies = new List<RadarController.Bogie>(50);
    private List<GameObject> friendlyLeadsNearby = new List<GameObject>(20);
    public LFAI.FormationReport formationReport;

    #endregion
    

    // --------------------------------
    #region FORMATION STUFF 
    public GameObject formationLead;
    public GameObject objectToDefend;
    public List<GameObject> wingmen = new List<GameObject>(3);
    PIDController pIDControllerX;
    PIDController pIDControllerY;

    
    #endregion
    /* ------------------------------------------------------------------------------------------------------------------------------- */

    // Use this for initialization
    void Start () {        
        ship = gameObject.GetComponent<ShipController>();
        ship.ownerAI = gameObject.GetComponent<VesselAI>();

        moveType = MoveType.simple;
        headingClearSet = false;

        if (teamManager != null)
        {
            ship.radar.teamSide = teamSide;
        }        
        
        velocityVector = gameObject.transform.Find("VelocityVector").GetComponent<LineRenderer>();
        waypointLine = gameObject.transform.Find("WaypointLine").GetComponent<LineRenderer>();
        waypointLine2 = gameObject.transform.Find("WaypointLine2").GetComponent<LineRenderer>();
        visionCollider = gameObject.transform.Find("VisionField").GetComponent<CircleCollider2D>();

        tcas = gameObject.transform.Find("TCAS").GetComponent<CapsuleCollider2D>();
        TCASLayerMask = LayerMask.GetMask(TCASLayers);

        // For the dynamic wp maintaining correction
        pIDControllerX = gameObject.AddComponent<PIDController>() as PIDController;
        pIDControllerY = gameObject.AddComponent<PIDController>() as PIDController;
        pIDControllerX.pGain = 0.15f; pIDControllerX.iGain = 0.15f; pIDControllerX.dGain = 0.15f;
        pIDControllerY.pGain = 0.15f; pIDControllerY.iGain = 0.15f; pIDControllerY.dGain = 0.15f;
        
        ClearWaypoints();
        
        //Debug.Log("My name is: " + gameObject.name);
        
        desiredSpeed = patrolSpeedMax;
        currentThrust = patrolThrust;      

        // Calculating AI parameters based on the difficulty float which, ranges from 0 to 2.
        // maximum accuraccy is 1 and minimum 0
        accuracy = MissionMaker.difficulty / 2;                     // higher is more accurate, values: 1 - 0
        decisionLag = 0.6f - (MissionMaker.difficulty / 4);        // lower is better, values: 0,1f - 0,6f. Transaltes to seconds
        agression = MissionMaker.difficulty / 2;                  // lower is wussier, values: 1 - 0

        InvokeRepeating("RepeatingChecks", 0.5f, (repeatingCheckRate + decisionLag + UnityEngine.Random.Range(-0.1f,0.1f)));

        InvokeRepeating("TCASCollisionCheck", 0.4f, decisionLag + UnityEngine.Random.Range(-0.1f, 0.1f));

        if (ship.shipType != ShipController.ShipType.civilian)
        {
            InvokeRepeating("HeadingClear", 2f, 0.2f + Random.Range(-0.1f, 0.1f));
        }

        OrderAllRadarOn();
        ship.radar.SetRadarOn();

        PopulateWeaponsRangesList();
    }

    // Update is called once per frame
    void Update() {

        // Checks if there are baddies, in which case all movement is done with firing
        if (currentTarget != null && ship.shipType != ShipController.ShipType.civilian)
        {
            moveType = MoveType.firing;
            Fire();
        }
        else
        {
            moveType = MoveType.simple;            
        }

        if (ship.radar == null)
        {
            Debug.Log("Vessel has no radar: " + gameObject.name);
        }

        if (ship.shipType == ShipController.ShipType.capital){            
            ExecuteBehaviour(flightStatus);
        }
        else {
            ExecuteBehaviour(flightStatus);
        }

        // statusText.text = NTools.HeadingFromVector(gameObject.GetComponent<Rigidbody2D>().velocity).ToString();

        UpdateVelocityVectorAndTCAS();
        UpdateWaypointLine();
        UpdateLeadToTarget();
        
        statusText.text = "Bogies: " + ship.radar.radarBogies.Count +  " WPs: " + wpList.Count + " Mod: " + flightStatus.ToString();

        if (currentTarget != null)
        {
            statusText.text = statusText.text + "\nTGT: " + currentTarget.name + "CLR:" + clearToFire;
        }
    }

    // appears to work
    void UpdateLeadToTarget()
    {
        if (currentTarget != null)
        {
            leadToTarget = NTools.HeadingFromVector(RAMP.GetLeadPoint(
                gameObject.GetComponent<Rigidbody2D>().position,
                currentTarget.GetComponent<Rigidbody2D>().position,
                currentTarget.GetComponent<Rigidbody2D>().velocity,
                gameObject.GetComponent<Rigidbody2D>().velocity,
                ship.activeWeapon.GetComponent<WepContr>().projectileSpeed
                )- gameObject.GetComponent<Rigidbody2D>().position
                );
        }
    }
   
    // Works quite well
    void Move() {

        if(wpList.Count == 0) { return; }
        wpIndex = wpList.Count - 1;

        // Determine desired heading
        switch (moveType)
        {
            case MoveType.simple:
                if(statusInFormation == StatusInFormation.lead && flightStatus != FlightStatus.sentry)
                {
                    desiredLookHeading = headingToWaypoint;
                }
                else if( statusInFormation == StatusInFormation.lead && flightStatus == FlightStatus.sentry)
                {
                    desiredLookHeading = headingToWaypoint;
                }
                else if(statusInFormation != StatusInFormation.lead && wingmanState == WingmanState.inFormation)
                { 
                    desiredLookHeading = formationLead.GetComponent<VesselAI>().ReportHeading();
                }
                else if(statusInFormation != StatusInFormation.lead && wingmanState == WingmanState.followingOrder)
                {
                    desiredLookHeading = headingToWaypoint;
                }
                
                break;
            case MoveType.firing:
                desiredLookHeading = leadToTarget;

                break;
        }        

        // if the wp is a moving one, try to maintain that position
        // if sucessful, don't do normal move
        // that means, that if the moving wp is too far away, the normal move continues
        if (wpIndex < wpList.Count)
        {
            if (wpList[wpIndex].movingWP)
            {
                // Is true and exits Move if MoveWithWp results in FormationPositionCorrection
                if (MoveWithWP(desiredLookHeading))
                {
                    return;
                }
                else
                {
                    if(moveType == MoveType.simple && statusInFormation != StatusInFormation.lead)
                    {
                        desiredLookHeading = NTools.HeadingFromPositions(
                            gameObject.GetComponent<Rigidbody2D>().position,
                            wpList[wpIndex].wpCoordinates
                            );
                    }
                }
            }
        }

        ship.Rotate(desiredLookHeading, Time.deltaTime);

        switch (moveType)
        {
            case MoveType.simple:
                // Lateral correction neccessity determination
                Vector3 crossProduct = Vector3.Cross(
                    gameObject.transform.up,
                    new Vector3(gameObject.GetComponent<Rigidbody2D>().velocity.x, gameObject.GetComponent<Rigidbody2D>().velocity.y, 0f)
                    );

                // Lateral Correction
                // Check for TCAS, if not significant, correct left or right drift
                if (rightTCASThrust < 0.02f && leftTCASThrust < 0.02f)
                {
                    if (crossProduct.z > 0.1f)
                    {
                        ship.ThrustRight(lateralCorrectionThrust);
                    }
                    else if (crossProduct.z < -0.1f)
                    {
                        ship.ThrustLeft(lateralCorrectionThrust);
                    }
                }
                else if (rightTCASThrust > leftTCASThrust)
                {
                    ship.ThrustRight(rightTCASThrust);
                }
                else if (leftTCASThrust > rightTCASThrust)
                {
                    ship.ThrustLeft(leftTCASThrust);
                }

                // Speed correction check - should I speed up or slow down
                if (Mathf.Abs(desiredLookHeading - gameObject.transform.eulerAngles.z) < 5f)
                {
                    // check if the ship is not travelling backwards
                    if (transform.InverseTransformVector(gameObject.GetComponent<Rigidbody2D>().velocity).y > 0)
                    {
                        if (gameObject.GetComponent<Rigidbody2D>().velocity.magnitude < desiredSpeed * asteroidSpeed)
                        {
                            intendedThrust = currentThrust;
                        }
                        else if (gameObject.GetComponent<Rigidbody2D>().velocity.magnitude > (desiredSpeed * asteroidSpeed + 1f))
                        {
                            intendedThrust = -currentThrust;
                        }
                        else { intendedThrust = 0f; }
                    }
                    else
                    {
                        intendedThrust = currentThrust; // the ship is travelling backwards, correct
                    }
                }

                // Check for TCAS, if not significant, (de)accelerate to intended cruising speed
                if (fwdTCASThrust < 0.02f && backTCASThrust < 0.02f)
                {
                    if (intendedThrust < 0f)
                    {
                        intendedThrust = Mathf.Abs(intendedThrust);
                        ship.ThrustBackward(intendedThrust);
                    }
                    else if (intendedThrust > 0f)
                    {
                        intendedThrust = Mathf.Abs(intendedThrust);
                        ship.ThrustForward(intendedThrust);
                    }
                }
                else if (fwdTCASThrust > backTCASThrust)
                {
                    ship.ThrustForward(fwdTCASThrust);
                }
                else if (backTCASThrust > fwdTCASThrust)
                {
                    ship.ThrustBackward(backTCASThrust);
                }
                else { }

                if (SettingsStatic.debug) { statusText.text = "Fuel: " + (int)ship.fuelPercentage; }

                break;
            case MoveType.firing:

                desiredVector = (wpList[wpIndex].wpCoordinates - gameObject.GetComponent<Rigidbody2D>().position).normalized * desiredSpeed;
                Vector2 desVelLoc = gameObject.transform.InverseTransformVector(desiredVector);
                Vector2 velLoc = gameObject.transform.InverseTransformVector(gameObject.GetComponent<Rigidbody2D>().velocity);            

                if( desVelLoc.x > velLoc.x + 0.1f && leftTCASThrust < rightTCASThrust)
                {
                    ship.ThrustRight(currentThrust);                    
                    
                } else if ( desVelLoc.x > velLoc.x + 0.1f && leftTCASThrust > rightTCASThrust)
                {
                    ship.ThrustLeft(leftTCASThrust);
                } else if (desVelLoc.x < velLoc.x - 0.1f && rightTCASThrust < leftTCASThrust)
                {
                    ship.ThrustLeft(currentThrust);
                } else if(desVelLoc.x < velLoc.x - 0.1f && rightTCASThrust > leftTCASThrust)
                {
                    ship.ThrustRight(rightTCASThrust);
                }

                if (desVelLoc.y > velLoc.y + 0.1f && backTCASThrust < fwdTCASThrust)
                {
                    ship.ThrustForward(currentThrust);

                }
                else if (desVelLoc.y > velLoc.y + 0.1f && backTCASThrust > fwdTCASThrust)
                {
                    ship.ThrustBackward(backTCASThrust);
                }
                else if (desVelLoc.y < velLoc.y - 0.1f && fwdTCASThrust < backTCASThrust)
                {
                    ship.ThrustBackward(currentThrust);
                }
                else if (desVelLoc.y < velLoc.y - 0.1f && fwdTCASThrust > backTCASThrust)
                {
                    ship.ThrustForward(fwdTCASThrust);
                }



                break;
        }
       
        
    }

    GameObject GetFormationPosObject(StatusInFormation status)
    {
        string pos = statusInFormation.ToString();
        GameObject positionObject = formationLead.transform.Find("Formation4(Clone)/" + pos).gameObject;

        return positionObject;
    }

    // Works pretty well
    void MaintainFormation(GameObject posObject)
    {
        bool movingWP = true;
        bool temporary = false;
        float wpRadius = 2f;
        
        Vector3 posCoords = posObject.transform.position;
        Vector2 wpCoordinates = new Vector2(posCoords.x, posCoords.y);

        RAMP.Waypoint formationWP = new RAMP.Waypoint(posObject, wpCoordinates, wpRadius, movingWP, temporary);
        if (wpList.Count == 0)
        {
            wpList.Add(formationWP);
        } else 
        {
            ClearWaypoints();
            wpList.Add(formationWP);
        } 

        Vector3 wpLocalCoords = gameObject.transform.InverseTransformPoint(posCoords);
        float sqrDistanceToPos = Vector2.SqrMagnitude(wpLocalCoords);

        
        // ------ Speed adjustment, based on distance from position in formation
        if (sqrDistanceToPos < 121f) { desiredSpeed = patrolSpeedMax + 0.5f; }
        if (sqrDistanceToPos > 121f && sqrDistanceToPos < 400f) { desiredSpeed = patrolSpeedMax + 2f; }
        if (sqrDistanceToPos > 400f) { desiredSpeed = emergencySpeedMax; }

        
        Move(); 
    }

    // returns true, if close enough to FormationPositionCorrection
    bool MoveWithWP(float DesiredHeading) {
        bool formateExecuted = false;

        Vector3 wpVector3 = new Vector3(wpList[wpIndex].wpCoordinates.x, wpList[wpIndex].wpCoordinates.y, 0f);
        Vector3 wpLocalCoords = gameObject.transform.InverseTransformPoint(wpVector3);
        float sqrDistToWp = Vector3.SqrMagnitude(wpLocalCoords);


        // if close enough to the WP FormationPositionCorrection is called
        if (wpLocalCoords.x < wpList[wpIndex].wpRadius * 3f && wpLocalCoords.y < wpList[wpIndex].wpRadius * 3f)
        {
            if ( sentryTarget != null && moveType == MoveType.simple)
            {
                DesiredHeading = sentryTarget.transform.eulerAngles.z;
            }         
            FormationPositionCorrection(wpLocalCoords, DesiredHeading);            
            formateExecuted = true;            
        }

        return formateExecuted;
    }

    // Quite ok, needs PID tuning
    void FormationPositionCorrection(Vector3 wpLocalCoords, float DesiredHeading)
    {
        ship.Rotate(DesiredHeading, Time.deltaTime);

        float xCorrection = pIDControllerX.Correction(0f, wpLocalCoords.x, Time.deltaTime);
        float yCorrection = pIDControllerY.Correction(0f, wpLocalCoords.y, Time.deltaTime);

        if (xCorrection > 0.02f && rightTCASThrust < 0.01f) { ship.ThrustLeft(Mathf.Abs(xCorrection)); }
        else { ship.ThrustRight(rightTCASThrust); }

        if (xCorrection < -0.02f && leftTCASThrust < 0.01f) { ship.ThrustRight(Mathf.Abs(xCorrection)); }
        else { ship.ThrustLeft(leftTCASThrust); }

        if (yCorrection > 0.02f && fwdTCASThrust < 0.01f) { ship.ThrustBackward(Mathf.Abs(yCorrection)); }
        else { ship.ThrustBackward(backTCASThrust); }

        if (yCorrection < -0.02f && backTCASThrust < 0.01f) { ship.ThrustForward(Mathf.Abs(yCorrection)); }
        else { ship.ThrustForward(fwdTCASThrust); }

        
    }

    void Stop() {

        // Rotating the stopped vessel to a "reasonable" heading
        if(teamManager != null)
        {
            desiredLookHeading = NTools.HeadingFromPositions(
                    new Vector2(
                    teamManager.transform.position.x,
                    teamManager.transform.position.y),
                    new Vector2(
                    gameObject.transform.position.x,
                    gameObject.transform.position.y)
                    );

        }

        ship.Rotate(desiredLookHeading, Time.deltaTime);

        // Lateral correction neccessity determination
        Vector3 crossProduct = Vector3.Cross(
            gameObject.transform.up,
            new Vector3(gameObject.GetComponent<Rigidbody2D>().velocity.x, gameObject.GetComponent<Rigidbody2D>().velocity.y, 0f)
            );

        // Check for TCAS, if not significant, correct left or right drift
        if (rightTCASThrust < 0.05f && leftTCASThrust < 0.05f)
        {
            if (crossProduct.z > 0.1f)
            {
                ship.ThrustRight(lateralCorrectionThrust);
            }
            else if (crossProduct.z < -0.1f)
            {
                ship.ThrustLeft(lateralCorrectionThrust);
            }
        }
        else if (rightTCASThrust > leftTCASThrust)
        {
            ship.ThrustRight(rightTCASThrust);
        }
        else if (leftTCASThrust > rightTCASThrust)
        {
            ship.ThrustLeft(leftTCASThrust);
        }

        // Speed correction check - should I speed up or slow down
        
            // check if the ship is not travelling backwards
            if (transform.InverseTransformVector(gameObject.GetComponent<Rigidbody2D>().velocity).y > 0)
            {
                if (gameObject.GetComponent<Rigidbody2D>().velocity.sqrMagnitude < 0.1f)
                {
                    intendedThrust = 0f;
                }
                else if (gameObject.GetComponent<Rigidbody2D>().velocity.sqrMagnitude > 0.1f)
                {
                    intendedThrust = -currentThrust;
                }
                else { intendedThrust = 0f; }
            }
            else
            {
                intendedThrust = currentThrust; // the ship is travelling backwards, correct
            }
        

        // Check for TCAS, if not significant, (de)accelerate to intended cruising speed
        if (fwdTCASThrust < 0.05f && backTCASThrust < 0.05f)
        {
            if (intendedThrust < 0f)
            {
                intendedThrust = Mathf.Abs(intendedThrust);
                ship.ThrustBackward(intendedThrust);
            }
            else if (intendedThrust > 0f)
            {
                intendedThrust = Mathf.Abs(intendedThrust);
                ship.ThrustForward(intendedThrust);
            }
        }
        else if (fwdTCASThrust > backTCASThrust)
        {
            ship.ThrustForward(fwdTCASThrust);
        }
        else if (backTCASThrust > fwdTCASThrust)
        {
            ship.ThrustBackward(backTCASThrust);
        }
        else { }

    }

    //----------------------------------------
    #region Behaviours, behaviours, behaviours

    void ExecuteBehaviour(FlightStatus behaviour) {        

        switch (behaviour)
        {
            case FlightStatus.patrolling:
                if (flightStatusIsChanging)
                {
                    
                    OrderAllRadarOn();
                    ship.radar.SetRadarOn();

                    desiredSpeed = patrolSpeedMax;
                    currentThrust = patrolThrust;
                    
                    flightStatusIsChanging = false;
                }
                else
                {
                    Patrol();
                }
                break;
            case FlightStatus.intercepting:
                if (flightStatusIsChanging)
                {
                    OrderAllRadarOn();
                    ship.radar.SetRadarOn();

                    currentThrust = dangerThrust;
                    desiredSpeed = interceptSpeed;
                    flightStatusIsChanging = false;
                }
                else
                {
                    
                    Intercept();
                }
                break;
            case FlightStatus.sentry:
                if (flightStatusIsChanging)
                {
                    desiredSpeed = patrolSpeedMax;
                    currentThrust = patrolThrust;
                    flightStatusIsChanging = false;
                }
                else
                {
                    Sentry();
                }
                break;
            case FlightStatus.engaging:
                if (flightStatusIsChanging)
                {
                    OrderAllRadarOn();
                    ship.radar.SetRadarOn();

                    currentThrust = dangerThrust;
                    desiredSpeed = combatSpeedMax;
                    flightStatusIsChanging = false;
                }
                else
                {
                    Engage();
                }          
                break;
            case FlightStatus.retreating:
                if (flightStatusIsChanging)
                {
                    if (wpList.Count == 0)
                    {
                        Vector2 fleePoint = new Vector2(-gameObject.transform.position.x, -gameObject.transform.position.y);
                        wpList.Add(new RAMP.Waypoint(fleePoint, 10f, false, false));
                    }

                    currentThrust = dangerThrust;
                    desiredSpeed = emergencySpeedMax;
                    flightStatusIsChanging = false;
                }
                else
                {
                    Flee();
                }
                break;      
        }
    }

    
    void Patrol() {

        if (wpList.Count == 0 || wpIndex < 0)
        {
            ClearWaypoints();
            Debug.Log("No WP");
            Stop();
        }
        else
        {
            Move();
        }

        // Debug.Log("Patrolling");

        if(ship.radar.radarBogies.Count != 0)
        {
            flightStatus = FlightStatus.engaging;
            flightStatusIsChanging = true;
        }
    }

    void Intercept() {        

        // it will change state once it has some targets, so simple is logical?!
        Move();

        if (formationBogies.Count != 0)
            {
                flightStatus = FlightStatus.engaging;
                flightStatusIsChanging = true;
            }


        // If the flight reaches the intercept point and still has no targets, it will switch to patrolling
        if (wpList.Count == 0 || wpIndex < 0)
        {
            ClearWaypoints();
            Debug.Log("No WP");
            flightStatus = FlightStatus.patrolling;
            flightStatusIsChanging = true;
        }
    }
    
    void Sentry() {

        if (statusInFormation == StatusInFormation.lead)
        {
            if(sentryTarget != null)
            {
                // float distance = (gameObject.transform.position - sentryTarget.transform.position).magnitude
                // Debug.Log("WP: " + wpList[wpIndex].wpCoordinates + " ST Name: " + sentryTarget.name + " Dist: " + distance);

                MaintainFormation(sentryTarget);
            }
            else
            {
                flightStatus = FlightStatus.patrolling;
                flightStatusIsChanging = true;
            }
        }
        else
        {
            if (sentryTarget == null)
            {
                sentryTarget = GetFormationPosObject(statusInFormation);
                if(sentryTarget == null) { Debug.Log("Failed to get sentry pos for wingman"); }
            }
            else
            {
                MaintainFormation(sentryTarget);
            }           
        }          
    }

    void Engage() {        

        if (statusInFormation == StatusInFormation.lead)
        {          
            if (!attackPlan.planSet)
            {
                int attackNr = DecideAttack();
                if (attackNr < 0)
                {
                    flightStatus = FlightStatus.retreating;
                    flightStatusIsChanging = true;
                }
                else
                {
                    attackType = (RAMP.AttackTypeF)attackNr;

                    List<Vector3> fLeads = new List<Vector3>(friendlyLeadsNearby.Count);

                    foreach (GameObject fLead in friendlyLeadsNearby)
                    {
                        fLeads.Add(fLead.gameObject.transform.position);
                    }

                    attackPlan = RAMP.GetAttackPlanF(
                        attackType,
                        gameObject.transform.position,
                        NTools.GetCenterOfBogies3D(formationBogies),
                        fLeads
                        );
                }
            }
            // If the attack plan is set
            else
            {
                switch (attackPlan.type)
                {
                    case RAMP.AttackTypeF.sentry:
                        if(wpList.Count == 0)
                        {
                            Vector3 desiredPos = NTools.GetCenterOfBogies3D(formationBogies) + attackPlan.offsetFromEnemy;

                            wpList.Add(new RAMP.Waypoint(
                                new Vector2(desiredPos.x, desiredPos.y),
                                5f,
                                true,
                                false
                                ));
                        }

                        break;
                    case RAMP.AttackTypeF.bracket:

                        break;
                    case RAMP.AttackTypeF.charge:

                        break;
                }
            }
        }

        if(currentTarget != null)
        {
            Move();
            Fire();
        } else
        {
            Move();
        }

        
    }

    private void PopulateWeaponsRangesList()
    {

    }

    // Called from Hostile presence check
    // TODO make actually abstract
    int SelectWeaponByIndex()
    {
        int wepIndex = 0;
       
        /*
        for (int i = 0; i < ship.weapons.Count; i++)
        {
            if (targetDistance < ship.weapons[i].GetComponent<WepContr>().range)
            {
                if(ship.weapons[i].GetComponent<WepContr>().ammo != 0)
                {
                    wepIndex = i;
                }                
            }
        }       
        */

        if(currentTarget.GetComponent<ShipController>().shipType == ShipController.ShipType.missile)
        {
            return 0;
        }

        if(targetDistance < ship.weapons[0].GetComponent<WepContr>().range)
        {
            wepIndex = 0;
        } else if(targetDistance > ship.weapons[0].GetComponent<WepContr>().range)
        {
            if (flightStatus == FlightStatus.sentry && ship.weapons[2].GetComponent<WepContr>().ammo != 0)
            {
                wepIndex = 2;
            } else if( flightStatus != FlightStatus.sentry && ship.weapons[1].GetComponent<WepContr>().ammo != 0)
            {
                wepIndex = 1;
            }
            else
            {
               if(ship.weapons[1].GetComponent<WepContr>().ammo != 0 && ship.weapons[2].GetComponent<WepContr>().ammo == 0)
                {
                    wepIndex = 1;
                } else if(ship.weapons[1].GetComponent<WepContr>().ammo == 0 && ship.weapons[2].GetComponent<WepContr>().ammo != 0)
                {
                    wepIndex = 2;
                }
                else
                {
                    wepIndex = 0;
                }                    
            }
        }

        return wepIndex;
    }

   
    private void HeadingClear()
    {
        float offset = ship.activeWeapon.transform.localPosition.y + 1f;        

        Vector3 transformUp = gameObject.transform.up.normalized;
        Vector2 castDirectionNorm = new Vector2(transformUp.x, transformUp.y);
        Vector2 castFromPos = gameObject.GetComponent<Rigidbody2D>().position + castDirectionNorm * offset;

        

        RaycastHit2D hit2D = Physics2D.Raycast(
            castFromPos,
            castDirectionNorm,
            ship.activeWeapon.GetComponent<WepContr>().range,
            raycastLayers
            );

        
        

        if (hit2D.collider != null)
        {
            

            if (hit2D.collider.gameObject.GetComponent<VesselAI>())
            {
                if (hit2D.collider.gameObject.GetComponent<VesselAI>().teamSide == teamSide)
                {
                    clearToFire = false;
                            Debug.DrawRay(
                    new Vector3(castFromPos.x, castFromPos.y, 0f),
                    new Vector3(castDirectionNorm.x, castDirectionNorm.y, 0f) * 50f,
                    Color.red,
                    0.11f
                    );
                }
            }
            else if (hit2D.collider.gameObject.GetComponent<StationAI>()) { 
                if(hit2D.collider.gameObject.GetComponent<StationAI>().teamSide == teamSide)
                {
                    clearToFire = false;
                            Debug.DrawRay(
                    new Vector3(castFromPos.x, castFromPos.y, 0f),
                    new Vector3(castDirectionNorm.x, castDirectionNorm.y, 0f) * 50f,
                    Color.red,
                    0.11f
                    );
                }
                
            }else if (hit2D.collider.gameObject.GetComponent<PlayerController>())
            {
                if(hit2D.collider.gameObject.GetComponent<PlayerController>().teamSide == teamSide) { clearToFire = false; }
            }
            else
            {
                clearToFire = true;
                    Debug.DrawRay(
                new Vector3(castFromPos.x, castFromPos.y, 0f),
                new Vector3(castDirectionNorm.x, castDirectionNorm.y, 0f) * 50f,
                Color.cyan,
                0.11f
                );
            }
        }

        
    }


    void Fire()
    {       
        if(ship.weapons.Count == 0) { return; }

        float aimOffset = leadToTarget - gameObject.transform.eulerAngles.z;

        float weaponRange = ship.activeWeapon.GetComponent<WepContr>().range;

        switch (ship.activeWeapon.GetComponent<WepContr>().weaponCategory)
        {
            case WepContr.WeapCat.closeRange:
                
                if (aimOffset < 10f && aimOffset > -10f && targetDistance < weaponRange )
                {
                    if (!firing)
                    {
                        ship.FireWeapon(true);
                        firing = true;
                    }                    
                }
                else
                {
                    ship.FireWeapon(false);
                    firing = false;
                }
                break;
            case WepContr.WeapCat.Ranged:                

                if (aimOffset < 1f && aimOffset > -1f && targetDistance < weaponRange )
                {
                    if (!firing)
                    {
                        ship.FireWeapon(true);
                        firing = true;
                    }
                }
                else
                {
                    ship.FireWeapon(false);
                    firing = false;
                }
                break;

            case WepContr.WeapCat.missile:
                
                if (!firing)
                {
                    ship.FireWeapon(true);
                    firing = true;
                }
                else
                {
                    ship.FireWeapon(false);
                    firing = false;
                }
                break;
        }        
    }


    // Returns attack nr with proper index for use in enum, -1 means retreat
    // TODO needs tuning, but returns a result, which is usable
    // TODO Add check for friendly nearby flights
    private int DecideAttack()
    {
        int attackNr = -1;

        float agressivness = 0f;
        int attackIndex = System.Enum.GetValues(typeof(RAMP.AttackTypeF)).Length - 1;         // the first type should be 1, the last n

        // fleeing parameter, initially half of the most conservative attack
        float retreatAgro = 0.5f;

        // Agressivness base parameters
        float forceBalance;
        if (formationBogies.Count == 0) { forceBalance = 1; }
        else { forceBalance = (1 + wingmen.Count) / (formationBogies.Count); }    
        float mood = 0f; float roc = 0;
        if (teamManager != null) {
            mood = 1 - ((float)teamManager.teamMood + 1) / 4;
            roc = teamManager.balanceROC;
        } 
        else { mood = 1f; }
       

        // Flight fuel check                               0 to 1
        float fuelPercentage = ReportFuel();
        if (wingmen.Count != 0)
        {
            for (int i = 0; i < wingmen.Count; i++)
            {
                fuelPercentage += wingmen[i].GetComponent<VesselAI>().ReportFuel();
            }
        }
        fuelPercentage = fuelPercentage / (1 + wingmen.Count);

        // Flight health check                              0 to 1
        float healthPercentage = ReportHealth();
        foreach (GameObject wingman in wingmen)
        {
            healthPercentage += wingman.GetComponent<VesselAI>().ReportHealth();
        }
        healthPercentage = healthPercentage / (1 + wingmen.Count);

        // taking all previous parameters together, averaging
        agressivness = (agression + forceBalance + mood + roc + fuelPercentage / 100 + healthPercentage) / 6;
        
        // translating average to enum AttackType lenght
        agressivness = agressivness * (float)attackIndex;

        if( agressivness < retreatAgro) {
            attackNr = -1;
        } else if (agressivness > retreatAgro && agressivness < (float)attackIndex )
        {
            attackNr = (int)agressivness;
        } else if( agressivness > (float)attackIndex)
        {
            attackNr = attackIndex;
        } else
        {
            attackNr = -1;
        }
                

        

        if (teamSide == TeamManager.TeamSide.red)
        {
            // Debug.Log("Agressivness: " + agressivness + " Attacknr: " + attackNr + " of " + attackIndex);            
            // Debug.Log("A " + agression + " FB " + forceBalance + " TM " + teamMood + " roc " + roc + " F " + fuelPercentage/100 + " H " + healthPercentage);
        }

        return attackNr;
    }

    

    void Flee() {
        if (statusInFormation == StatusInFormation.lead)
        {

        }
        else
        {

        }

        Move();
    }
    

    #endregion
    // -------------------------------------

    void RepeatingChecks() {
        bool collisionThreatDetected = false;

        if (wpList.Count != 0)
        {
            WaypointCheck();

            int threatCount = CollisionAvoidancePrepare();

            if (threatCount > 1)
            {
                
                collisionThreatDetected = CollisionAvoidance();
            }
        }

        HostilePresenceCheck();

        UpdateFriendlyLeadsNearby();
        
        // Remove dead wingmen
        for (int i = 0; i < wingmen.Count; i++)
        {
            if(wingmen[i] == null) { wingmen.RemoveAt(i); }
        }
    }

    // --------------------------------------
    #region Collision Avoidance - both TCAS and MTCD
    void TCASCollisionCheck() {
        // Debug.Log("Check called");
        float ModifierX = 2f;
        float ModifierY = 1f;


        TCASThreatList.Clear();

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(TCASLayerMask);   
        Collider2D[] contacts = new Collider2D[10]; 

        int tcasObjects = tcas.OverlapCollider(filter, contacts);
        

        if(tcasObjects == 0) {
            rightTCASThrust = 0f;
            leftTCASThrust = 0f;
            fwdTCASThrust = 0f;
            backTCASThrust = 0f;
            return;
        }
        // Debug.Log("TCAS contacts " + (tcasObjects - 1) + " " + contacts.Length);
        // Put all valid threats into CollisionThreat list

        for (int i = 0; i < tcasObjects; i++){
            if (contacts[i] && contacts[i].gameObject.name != gameObject.name) {
                // Debug.Log("TCAS contact: " + contacts[i].name);
                TCASthreatPosition = gameObject.transform.InverseTransformPoint(contacts[i].transform.position);                
                // TCASthreatVelocity = gameObject.transform.InverseTransformVector(contacts[i].GetComponent<Rigidbody2D>().velocity);                
                TCASThreatList.Add(new NTools.CollisionThreat(TCASthreatPosition, TCASthreatPosition, TCASthreatVelocity, 0f,0f));
            }
        }
        // Work through CollisonThreat list and produce TCASThrusts
        if (TCASThreatList.Count != 0)
        {
            // zeroing the previous avoiding values
            rightTCASThrust = 0f;
            leftTCASThrust = 0f;
            fwdTCASThrust = 0f;
            backTCASThrust = 0f;
            float tcasX = tcas.size.x;
            float tcasY = tcas.size.y;

            for (int i = 0; i < TCASThreatList.Count; i++)
            {
                // Debug.Log("In the TCAS final loop");
                // get inline avoidance thrust
                if (TCASThreatList[i].threatCoordinates.y > 0f)
                {
                    backTCASThrust += (tcasY - TCASThreatList[i].threatCoordinates.y) / tcasY * dangerThrust;
                }
                else if (TCASThreatList[i].threatCoordinates.y < 0f)
                {
                    fwdTCASThrust += (tcasY - TCASThreatList[i].threatCoordinates.y) / tcasY * dangerThrust;
                }

                // get lateral avoidance thrust
                if (TCASThreatList[i].threatCoordinates.x > 0f)
                {
                    leftTCASThrust += (tcasX - TCASThreatList[i].threatCoordinates.x) / tcasX * dangerThrust;
                }
                else if (TCASThreatList[i].threatCoordinates.x < 0f)
                {
                    rightTCASThrust += (tcasX - TCASThreatList[i].threatCoordinates.x) / tcasX * dangerThrust; 
                }
            }

            backTCASThrust = (backTCASThrust / TCASThreatList.Count)    * ModifierY;
            fwdTCASThrust = (fwdTCASThrust / TCASThreatList.Count)      * ModifierY;
            leftTCASThrust = (leftTCASThrust / TCASThreatList.Count)    * ModifierX;
            rightTCASThrust = (rightTCASThrust / TCASThreatList.Count)  * ModifierX;
        }        
    }

    // Works, spits out how many collision threats it sees (one is own vessel), populates appropriate list if more than one
    int CollisionAvoidancePrepare()
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(TCASLayerMask);
        Collider2D[] contacts = new Collider2D[50];

        int threatCount = visionCollider.OverlapCollider(filter, contacts);
        if( threatCount <= 1) { return threatCount; }

        collisionThreatList.Clear();
        for (int i = 0; i < threatCount; i++) {
            if (contacts[i].gameObject.name != gameObject.name && contacts[i].gameObject.GetComponent<Rigidbody2D>())
            {
                collisonThreatPosition.x = contacts[i].gameObject.transform.position.x;
                collisonThreatPosition.y = contacts[i].gameObject.transform.position.y;
                collisionThreatVelocity = gameObject.transform.InverseTransformVector(contacts[i].gameObject.GetComponent<Rigidbody2D>().velocity);
                // The position is added twice, just to fill the slot, in later processing it will be upgraded to projected position
                collisionThreatList.Add(new NTools.CollisionThreat(collisonThreatPosition, collisonThreatPosition, collisionThreatVelocity, 0f,0f));
            }
            else { }
        }
        return threatCount;
        
    }


    // returns TRUE when CA has determined a threat and sucessfully added a WP to solve conflict
    // False is returned when either CA failed or no threat was found
    bool CollisionAvoidance()
    {
        
        Vector2 shipProjectedPos = gameObject.GetComponent<Rigidbody2D>().position 
            + (wpList[wpIndex].wpCoordinates - gameObject.GetComponent<Rigidbody2D>().position).normalized * desiredSpeed;
        Vector2 shipPos = new Vector2(gameObject.GetComponent<Rigidbody2D>().position.x, gameObject.GetComponent<Rigidbody2D>().position.y);
        Vector2 headingToWP = wpList[wpIndex].wpCoordinates - shipPos;
        float distanceToWP = Mathf.Sqrt(Vector2.SqrMagnitude(wpList[wpIndex].wpCoordinates - gameObject.GetComponent<Rigidbody2D>().position));

        Vector2 closestThreat2D = new Vector2(0f,0f);
        Vector2 closestThreatVelocity2D = new Vector2(0f, 0f);
        Vector2 newHeadingVector = new Vector2(0f, 0f);

        // Variables for use in loops, to avoid repeditive declarations
        float timeToInterWP = 50f / desiredSpeed;
        float distanceToNearestThreat = 0f;
        float newHeadingLeft = 0f;
        float newHeadingRight = 0f;
        bool leftHeading = true;        
        bool solutionFound = false;
        

        // Setting the travel time, by which we set the threat movement amount        
        if (distanceToWP >= 50f)       {            timeToInterWP = 50f / desiredSpeed;                            }
        else                             {         timeToInterWP = distanceToWP/desiredSpeed;      }

        // Sorting the threats, which also fills in missing bits in the threat list
        SortCollisionThreats((wpList[wpIndex].wpCoordinates - gameObject.GetComponent<Rigidbody2D>().position), timeToInterWP);  
        // parsing the POTENTIAL threats for REAL threats, the method also returns a BOOL if there are REAL threats at all
        NTools.CollisionThreatsSorted parsedThreats = CheckHeadingClear(shipProjectedPos, wpList[wpIndex].wpCoordinates);

        // If no threats are found, exit CA, else clear temporary wps from the wp list and start meaty bit of CA
        if (!parsedThreats.realThreatsPresent) {
            return false;
        }  

        // Determine distance to closest threat and its coordinates -.-- Why do I do it?
        else if (parsedThreats.realThreatsLeft.Count != 0 && parsedThreats.realThreatsRight.Count != 0)
        {
            ClearTemporaryWaypoints();

            if (parsedThreats.realThreatsLeft[0].sqrDistance < parsedThreats.realThreatsRight[0].sqrDistance)
            {
                distanceToNearestThreat = Vector2.Distance(
                    shipPos, 
                    parsedThreats.realThreatsLeft[0].threatCoordinates
                    );
                closestThreat2D = parsedThreats.realThreatsLeft[0].threatCoordinates;
                closestThreatVelocity2D = parsedThreats.realThreatsLeft[0].threatVelocity;
            }
            else
            {
                distanceToNearestThreat = Vector2.Distance(
                       shipPos,
                       parsedThreats.realThreatsRight[0].threatCoordinates
                       );
                closestThreat2D = parsedThreats.realThreatsRight[0].threatCoordinates;
                closestThreatVelocity2D = parsedThreats.realThreatsRight[0].threatVelocity;
            }
        }
        else if (parsedThreats.realThreatsLeft.Count == 0 && parsedThreats.realThreatsRight.Count != 0)
        {
            ClearTemporaryWaypoints();

            distanceToNearestThreat = Vector2.Distance(
                       shipPos,
                       parsedThreats.realThreatsRight[0].threatCoordinates
                       );
            closestThreat2D = parsedThreats.realThreatsRight[0].threatCoordinates;
            closestThreatVelocity2D = parsedThreats.realThreatsRight[0].threatVelocity;
        }
        else if (parsedThreats.realThreatsLeft.Count != 0 && parsedThreats.realThreatsRight.Count == 0)
        {
            ClearTemporaryWaypoints();

            distanceToNearestThreat = Vector2.Distance(
                    shipPos,
                    parsedThreats.realThreatsLeft[0].threatCoordinates
                    );
            closestThreat2D = parsedThreats.realThreatsLeft[0].threatCoordinates;
            closestThreatVelocity2D = parsedThreats.realThreatsLeft[0].threatVelocity;
        }

        
        // statusText.text = vectorToThreat.ToString();

        // Ceck if the WP is closer than the threat, in that case, return and stop CA, return false
        if(distanceToWP < distanceToNearestThreat) { return false; }

        headingToWP = headingToWP.normalized * distanceToNearestThreat;             

        // parse intermittently left and right for a clear initial passage    
        int iterations = 0;
        do
        {
            switch (leftHeading)
            {
                case true:
                    newHeadingLeft -= 1;

                    parsedThreats = CheckHeadingClear(shipPos, shipPos + NTools.RotateVector2(headingToWP, newHeadingLeft));
                    if (SettingsStatic.debug) { DrawDebugLine(shipPos, shipPos + NTools.RotateVector2(headingToWP, newHeadingLeft)); }

                    leftHeading = false;
                    iterations++;
                    break;
                case false:
                    newHeadingRight += 1;

                    parsedThreats = CheckHeadingClear(shipPos, shipPos + NTools.RotateVector2(headingToWP, newHeadingRight));
                    if(SettingsStatic.debug) { DrawDebugLine(shipPos, shipPos + NTools.RotateVector2(headingToWP, newHeadingLeft).normalized);}

                    leftHeading = true;
                    iterations++;
                    break;
                    
            }
            // exit failsafes:
            if(newHeadingLeft < -90 || newHeadingRight > 90)
            {
                solutionFound = false;
                break;                
            }


        } while (parsedThreats.realThreatsPresent);

        if (newHeadingLeft > -90 && newHeadingRight < 90)
        {
            float newHeading;
            if (leftHeading) { newHeading = newHeadingRight; }
            else { newHeading = newHeadingLeft; }
            solutionFound = true;
            //Debug.Log("Clear relative heading found: " + newHeading.ToString());
        }

        if (!solutionFound) { Debug.Log("Heading not found in " + iterations + " iterations."); return false; }
        

        // Determine new direction from projected ship position
        if (leftHeading)    { newHeadingVector = NTools.RotateVector2(headingToWP, newHeadingRight); /*Debug.Log("New Heading: " + newHeadingRight);   */}
        else                { newHeadingVector = NTools.RotateVector2(headingToWP, newHeadingLeft); /*Debug.Log("New Heading: " + newHeadingLeft);    */}

        float newWPDistance = 1f;
        do
        {
            newWPDistance += 0.1f;
            parsedThreats = CheckHeadingClear(shipPos + newHeadingVector * newWPDistance, wpList[wpIndex].wpCoordinates);
            // check to limit the possible distance of the intermittent WP to the distance of the original WP
            if(newWPDistance * newHeadingVector.magnitude > distanceToWP) {
                Debug.Log("WP solution not found.");                
                break;
            }
        } while (parsedThreats.realThreatsPresent);

        if(newWPDistance < 10f) { solutionFound = true; }         


        // This happens, when the collisionAvoidance has done its job and can add a new intermediate waypoint
        // also entering this loop will return true - the method has parsed successfully and added a wp        
        if (solutionFound)
        {
            RAMP.Waypoint newInbetweenWP = new RAMP.Waypoint(shipPos + newHeadingVector * (newWPDistance - 0.2f), 4f, false, true);
            wpList.Add(newInbetweenWP);
            wpIndex = wpList.Count - 1;
            return true;
        }
        return false;
    }

    // This method sorts the global List of collision threats and adds missing values
    void SortCollisionThreats(Vector2 headingVector, float timeToInterWP)
    {
        Vector2 shipPos = gameObject.GetComponent<Rigidbody2D>().position;

        // sort lefties and righties and add projected point
        for (int i = 0; i < collisionThreatList.Count; i++)
        {
            // Calculating the endpos for threat with the time it takes the ship to fly to WP
            collisionThreatList[i].threatCoordinates2 = collisionThreatList[i].threatCoordinates + collisionThreatList[i].threatVelocity * timeToInterWP;

            // Adds the sqr distance to the appropriate contact in the list
            collisionThreatList[i].sqrDistance =
                    Vector2.SqrMagnitude(collisionThreatList[i].threatCoordinates - gameObject.GetComponent<Rigidbody2D>().position);

            //determining if threat is left or right from current heading by normalized crossproduct Z value
            collisionThreatList[i].leftRightOfHeading = Vector3.Cross(
                new Vector3(headingVector.x, headingVector.y, 0f).normalized,
                new Vector3(collisionThreatList[i].threatCoordinates.x - shipPos.x, collisionThreatList[i].threatCoordinates.y - shipPos.y, 0f).normalized
                ).z;
        }
    }

    // Determines real threats and sorts them by distance 
    NTools.CollisionThreatsSorted CheckHeadingClear(Vector2 initialPosition, Vector2 aimPoint)
    {
        
        //The ship Projected velocity is from initial position to aimpoint, NOT the real velocity vector!!!
        Vector3 shipProjectedVel = new Vector3((aimPoint - initialPosition).normalized.x, (aimPoint - initialPosition).normalized.y,0f) * desiredSpeed;
        Vector2 shipPos = gameObject.GetComponent<Rigidbody2D>().position;

        Vector3 threatLocalPos;
        
        List<NTools.CollisionThreat> realThreatsLeft = new List<NTools.CollisionThreat>(20);
        List<NTools.CollisionThreat> realThreatsRight = new List<NTools.CollisionThreat>(20);        

        NTools.CollisionThreatsSorted ctSorted = new NTools.CollisionThreatsSorted(false, realThreatsLeft, realThreatsRight);

        float separation = 0f; float separation2 = 0f;       


        // Parse for real threats and add them to ctSorted
        for (int i = 0; i < collisionThreatList.Count; i++)
        {
            threatLocalPos = gameObject.transform.InverseTransformPoint(new Vector3(
            collisionThreatList[i].threatCoordinates.x,
            collisionThreatList[i].threatCoordinates.y,
            0f
            ));

            if(threatLocalPos.y < 0) { continue; }

            // Debug.Log("Threat pos" + gameObject.transform.InverseTransformPoint(collisionThreatList[i].threatCoordinates.x, collisionThreatList[i].threatCoordinates.y,0f));

            separation = Vector3.Cross(
                new Vector3(collisionThreatList[i].threatCoordinates.x - shipPos.x, collisionThreatList[i].threatCoordinates.y - shipPos.y, 0f),
                shipProjectedVel
                ).magnitude / shipProjectedVel.magnitude;
            // Debug.Log("Sep1: " + separation);

            separation2 = Vector3.Cross(
                new Vector3(collisionThreatList[i].threatCoordinates2.x - shipPos.x, collisionThreatList[i].threatCoordinates2.y - shipPos.y, 0f),
                shipProjectedVel
                ).magnitude / shipProjectedVel.magnitude;

            // Debug.Log("Sep2: " + separation2);

            // if threat is valid push to appropriate list for side, also sorts so, that the nearest threat has lowest index
            if (separation < minSeparation || separation2 < minSeparation)
            {
                ctSorted.realThreatsPresent = true;

                if (collisionThreatList[i].leftRightOfHeading < 0f)
                {
                    if (realThreatsLeft.Count == 0)
                    {
                        realThreatsLeft.Add(collisionThreatList[i]);                        
                    }
                    else if (realThreatsLeft[realThreatsLeft.Count - 1].sqrDistance < collisionThreatList[i].sqrDistance)
                    {
                        realThreatsLeft.Add(collisionThreatList[i]);
                    }
                    else if (realThreatsLeft[realThreatsLeft.Count - 1].sqrDistance > collisionThreatList[i].sqrDistance)
                    {
                        NTools.CollisionThreat tempFromLefts = realThreatsLeft[realThreatsLeft.Count - 1];
                        realThreatsLeft.RemoveAt(realThreatsLeft.Count - 1);
                        realThreatsLeft.Add(collisionThreatList[i]);
                        realThreatsLeft.Add(tempFromLefts);
                    }
                }
                else
                {
                    if (realThreatsRight.Count == 0)
                    {
                        realThreatsRight.Add(collisionThreatList[i]);
                    }
                    else if (realThreatsRight[realThreatsRight.Count - 1].sqrDistance < collisionThreatList[i].sqrDistance)
                    {
                        realThreatsRight.Add(collisionThreatList[i]);
                    }
                    else if (realThreatsRight[realThreatsRight.Count - 1].sqrDistance > collisionThreatList[i].sqrDistance)
                    {
                        NTools.CollisionThreat tempFromLefts = realThreatsRight[realThreatsRight.Count - 1];
                        realThreatsRight.RemoveAt(realThreatsRight.Count - 1);
                        realThreatsRight.Add(collisionThreatList[i]);
                        realThreatsRight.Add(tempFromLefts);
                    }                    
                }
            }
        }       

        return ctSorted;
    }
    #endregion
    // --------------------------------------

    // TODO upgrade target selection logic
    void HostilePresenceCheck()
    {
        if(ship.shipType == ShipController.ShipType.capital)
        {
            for(int i = 0; i < ship.radar.teamBogies.Count; i++)
            {
                GameObject obj = teamManager.GetObjectiveObject();
                if (obj)
                {
                    if(ship.radar.teamBogies[i].bogieObject == obj)
                    {
                        currentTarget = obj;
                        Stop();
                        return;
                    }
                }
            }
        }

        if(ship.shipType == ShipController.ShipType.civilian) { return; }

        if (statusInFormation == StatusInFormation.lead)
        {
            if (ship.radar.radarBogies.Count != 0)
            {
                if (flightStatus == FlightStatus.patrolling || flightStatus == FlightStatus.intercepting)
                {
                    flightStatus = FlightStatus.engaging;
                    flightStatusIsChanging = true;
                }
            }
            
        }

        // Only in sentry mode is a vessel allowed to select team targets?!
        if(flightStatus == FlightStatus.sentry)
        {
            if(ship.radar.radarBogies.Count != 0 || ship.radar.teamBogies.Count != 0)
            {
                currentTarget = ship.radar.GetNearestTarget();
                ship.SelectWeaponByNumber(SelectWeaponByIndex()+1);
                if (currentTarget != null)
                {
                    targetDistance = (currentTarget.GetComponent<Rigidbody2D>().position - gameObject.GetComponent<Rigidbody2D>().position).magnitude;
                } else {
                   

                }
            }
        } else {
            if (ship.radar.radarBogies.Count != 0)
            {
                currentTarget = ship.radar.GetNearestTarget();
                ship.SelectWeaponByNumber(SelectWeaponByIndex()+1);
                if (currentTarget != null)
                {
                    targetDistance = (currentTarget.GetComponent<Rigidbody2D>().position - gameObject.GetComponent<Rigidbody2D>().position).magnitude;
                }
            }
            else
            {
                currentTarget = null;
            }
        }
    }    

    // TODO test
    private void UpdateFriendlyLeadsNearby()
    {
        if(teamManager != null) { friendlyLeadsNearby = teamManager.FriendlyLeadsRequest(gameObject); }
        
        
    }

    public float ReportFuel()
    {
        return ship.fuelPercentage;
    }

    public float ReportHeading()
    {
        return gameObject.transform.eulerAngles.z;
    }

    public float ReportHealth()
    {
        return ship.GetHealth();
    }

    #region Communications

    // passes lead to next wingman, if any available, appears working
    public void Death()
    {
        if (statusInFormation == StatusInFormation.lead)
        {
            if(ship.shipType == ShipController.ShipType.capital || ship.shipType == ShipController.ShipType.civilian)
            {
                Destroy(gameObject, 0.1f);
                return;
            }

            if (wingmen.Count == 0)
            {
                if (teamManager) { teamManager.FormationDestroyed(gameObject); }
            }
            else
            {
                //if (!gameObject.transform.Find("Formation4(Clone)").gameObject) { Debug.Log("No Formation4 on death"); return; }

                GameObject formation4 = gameObject.transform.Find("Formation4(Clone)").gameObject;

                bool leadAssigned = false;
                int pos = 0;

                
                // Set up the new formation lead before death, rawr
                if (!leadAssigned && wingmen[0])
                {
                    VesselAI newLeadAi = wingmen[0].GetComponent<VesselAI>();

                    newLeadAi.statusInFormation = StatusInFormation.lead;
                    newLeadAi.flightStatus = FlightStatus.sentry;
                    newLeadAi.teamManager = teamManager;
                    newLeadAi.formationBogies.Clear();
                    newLeadAi.formationBogies = formationBogies;

                    teamManager.NewFormationLead(gameObject, wingmen[0]);

                    formation4.transform.parent = wingmen[0].transform;
                    formation4.transform.localPosition = new Vector3(0, 0, 0);
                    formation4.transform.localRotation.SetEulerAngles(wingmen[0].transform.rotation.eulerAngles);

                    // transfer wingmen list to newLead and remove himself from it
                    newLeadAi.wingmen = wingmen;
                    newLeadAi.wingmen.RemoveAt(0);

                    if (newLeadAi.wingmen.Count != 0)
                    {
                        foreach(GameObject wingman in newLeadAi.wingmen)
                        {
                            wingman.GetComponent<VesselAI>().statusInFormation -= 1;
                            wingman.GetComponent<VesselAI>().formationLead = newLeadAi.gameObject;
                        }
                    }                    

                    leadAssigned = true;
                }
                
            }
        } else
        {
            formationLead.GetComponent<VesselAI>().WingmanDeathReport(gameObject);
        }

        Destroy(gameObject, 0.1f);
    }

    // For incoming orders, returned bool states if order was accepted
    public bool GiveOrder(LFAI.Order order)
    {
        switch (order.orderType)
        {
            case LFAI.OrderType.patrol:
                if (ship.radar.radarBogies.Count == 0 && !ship.fuelIsBingo)
                {
                    

                    wpList.Clear();

                    for (int i = 0; i < order.route.wpList.Count; i++)
                    {
                        wpList.Add(order.route.wpList[i]);
                        // Debug.Log("Wp added: " + order.route.wpList[i].wpCoordinates);
                    }

                    wpIndex = wpList.Count - 1;
                    

                    flightStatus = FlightStatus.patrolling;
                }
                else
                {
                    return false;
                }
                break;
            case LFAI.OrderType.intercept:
                if (ship.radar.radarBogies.Count == 0 && !ship.fuelIsBingo)
                { 
                    flightStatus = FlightStatus.intercepting;
                    wpList.Clear();

                    for(int i = 0; i < order.route.wpList.Count; i++)
                    {
                        wpList.Add(order.route.wpList[i]);
                    }

                }
                else
                {
                    return false;
                }

                break;
            case LFAI.OrderType.Sentry:
                
                if (ship.radar.radarBogies.Count == 0)
                {
                    flightStatus = FlightStatus.sentry;

                    if(order.targetObject != null)
                    {
                        sentryTarget = order.targetObject;
                        wpList.Clear();
                        wpList.Add(new RAMP.Waypoint(
                            sentryTarget,
                            new Vector2(sentryTarget.transform.position.x, sentryTarget.transform.position.y),
                            2f,
                            true,
                            false
                            ));
                        wpIndex = wpList.Count - 1;

                    }                    
                }
                else
                {
                    return false;
                }

                break;
            case LFAI.OrderType.regroup:
                if (flightStatus != FlightStatus.retreating)
                {
                    flightStatusIsChanging = true;
                    flightStatus = FlightStatus.retreating;

                }
                break;
        }

        return true;
    }

    public void OrderRadarOn()
    {
        if (ship != null)
        {
            if (ship.radar != null)
            {
                ship.radar.SetRadarOn();
            }
        }
        else
        {
            Debug.Log("Radar missing at time: " + Time.timeSinceLevelLoad);
        }       

        if (wingmen.Count != 0)
        {
            foreach (GameObject wingman in wingmen)
            {
                wingman.GetComponent<VesselAI>().OrderRadarOn();
            }
        }
    }

    public void OrderRadarOff()
    {
        ship.radar.SetRadarOff();

        if(wingmen.Count != 0)
        {
            foreach(GameObject wingman in wingmen)
            {
                wingman.GetComponent<VesselAI>().OrderRadarOff();
            }
        }
    }
    // no return type, since the method calls another method that will report to teamManager directly
    public void IncomingLock(GameObject LockSource)
    {
        lockSources.Add(LockSource);
    }

    public void ShareBogies(List<RadarController.Bogie> bogies)
    {
        ship.radar.PassTeamBogies(bogies);
    }

    public LFAI.FormationReport ReportRequest()
    {
        formationBogies.Clear();
        formationBogies = ship.ReportBogies();

        List<RadarController.Bogie> bogieTemp = new List<RadarController.Bogie>(100);

        // Assemble non dublicate exhaustive list of bogies:
        foreach (GameObject wingman in wingmen)
        {
            bogieTemp = wingman.GetComponent<ShipController>().ReportBogies();
            foreach(RadarController.Bogie bogie in bogieTemp)
            {
                bool bogieListed = false;
                for(int i = 0; i < formationBogies.Count; i++)
                {
                    if(bogie.bogieObject == formationBogies[i].bogieObject)
                    {
                        bogieListed = true;
                    }
                }
                if (!bogieListed)
                {
                    formationBogies.Add(bogie);
                }
            }

            bogieTemp.Clear();
        }

        // prevent wingmen from going past the formation lead
        if (statusInFormation != StatusInFormation.lead)
        {
            Debug.LogWarning("Wingman tried to access TM!!");
            return new LFAI.FormationReport(null,false, 0, 0, 0, false, 0f, 0); // flight size is null, therefore ERROR!!!
        }

        float formationHealth = ship.health;


        foreach(GameObject wingman in wingmen)
        {
            formationHealth += wingman.GetComponent<ShipController>().health;
        }

        bool fuelIsBingo = ship.fuelIsBingo;
        float fuelPercentageAverage = ship.fuelPercentage;
        bool hasWP = (wpList.Count != 0);
        int flightSize = 1 + wingmen.Count;
        foreach (GameObject wingman in wingmen)
        {
            fuelPercentageAverage += wingman.GetComponent<ShipController>().fuelPercentage;

            // if the lead was not bingo, but some wingman is, flight will be reported bingo
            if (wingman.GetComponent<ShipController>().fuelIsBingo)
            {
                fuelIsBingo = true;
            }
        }

        fuelPercentageAverage = fuelPercentageAverage / flightSize;

        formationReport = new LFAI.FormationReport(
                formationBogies,
                hasWP,
                formationBogies.Count,
                flightSize,
                formationHealth,
                fuelIsBingo,
                fuelPercentageAverage,
                flightStatus
                );

        return formationReport;
    }

    // seems to work
    public void WingmanDeathReport(GameObject deadWingman)
    {
        teamManager.LostWingman();

        wingmen.Remove(deadWingman);
        if (wingmen.Count != 0)
        {
            foreach (GameObject wingman in wingmen)
            {
                int newPos = wingmen.IndexOf(wingman) + 1;
                wingman.GetComponent<VesselAI>().statusInFormation = (VesselAI.StatusInFormation)newPos;
            }
        }
    }

    public void ReportBogiesToTM(List<RadarController.Bogie> bogies)
    {
        foreach (RadarController.Bogie bogie in bogies)
        {
            teamManager.BogieSpotted(bogie);
        }
    }

    public void ReportKill(GameObject target)
    {
        if (teamManager) { teamManager.ReportKill(target); }        
    }

    public void EnteredCivWp()
    {
        if(ship.shipType == ShipController.ShipType.civilian)
        {
            teamManager.ReportCivWpReached();
        }        
    }

    public FlightStatus ReportFlightStatus()
    {
        return flightStatus;
    }

    public bool ReportHasWp()
    {
        if(wpList.Count == 0)
        {
            return false;
        }
        else
        {
            return true;
        }
    }

    /*--------------------------------------------------------
                    METHODS FOR LEAD
         ---------------------------------------------------*/

    void OrderAllRadarOn()
    {
        for( int i = 0; i < wingmen.Count; i++)
        {
            if(wingmen[i] != null)
            {
                wingmen[i].GetComponent<VesselAI>().OrderRadarOn();
            }
        }
    }

    #endregion


    //--------------------------------------
    #region Waypoint methods
    void WaypointCheck() {
        wpIndex = wpList.Count - 1;

        if (statusInFormation == StatusInFormation.lead){          
            
            if (wpList.Count == 0) { return; } // sanity check

            // Check if the Vessel is close enough to the waypoint and if, then remove it
            Vector2 vectorToWP = wpList[wpIndex].wpCoordinates - gameObject.GetComponent<Rigidbody2D>().position;
            if (Vector2.SqrMagnitude(vectorToWP) < wpList[wpIndex].wpRadius * wpList[wpIndex].wpRadius) 
            {
                if (!wpList[wpIndex].movingWP)
                {
                    wpList.RemoveAt(wpIndex);
                    wpIndex = wpList.Count - 1;
                }
            }           
        }

        if (wpList.Count != 0)
        {
            if (wpList[wpIndex].temporary)
            {
                TempWPSanityCheck();
            }

            if (wpList[wpIndex].reccommendedSpeed > 0.5 && !wpList[wpIndex].movingWP)
            {
                desiredSpeed = wpList[wpIndex].reccommendedSpeed;
            }

            headingToWaypoint = RAMP.HeadingFromPositions(
                    new Vector2(gameObject.transform.position.x, gameObject.transform.position.y),
                    wpList[wpIndex].wpCoordinates
                    );

        }

        
    }

    void TempWPSanityCheck()
    {
        Vector2 shipProjectedPos = gameObject.GetComponent<Rigidbody2D>().position
            + (wpList[wpIndex].wpCoordinates - gameObject.GetComponent<Rigidbody2D>().position).normalized * desiredSpeed;

        int nonTempWPindex = wpList.Count - 1;        

        // This loop starts from the top of the list i.e closest wp until it finds a non Temp WP, then sets the index and breaks the loop
        while (wpList[nonTempWPindex].temporary)
        {            
            nonTempWPindex--;
        }

        Vector2 nonTempWP2D = wpList[nonTempWPindex].wpCoordinates;

        if(!CheckHeadingClear(shipProjectedPos, nonTempWP2D).realThreatsPresent)
        {
            ClearTemporaryWaypoints();
        }
    }

    

    void ClearWaypoints()    {
        wpIndex = 0;
        wpList.Clear();
        wpList.Capacity = 10;
        
    }
    
    void ClearTemporaryWaypoints()
    {
        for (int i = 0; i < wpList.Count; i++)
        {
            if (wpList[i].temporary)
            {
                wpList.RemoveAt(i);
            }
        }
        wpIndex = wpList.Count - 1;
    }
    #endregion
    //------------------------------------------

    // Works in principle
    void UpdateVelocityVectorAndTCAS()
    {
        Vector2 velocity2D = gameObject.GetComponent<Rigidbody2D>().velocity;
        Vector3 localVelocity3D = gameObject.transform.InverseTransformVector(new Vector3(velocity2D.x, velocity2D.y, 0f));

        Vector3 position = gameObject.transform.position;
        Vector3 lineStart = new Vector3(position.x + velocity2D.normalized.x, position.y + velocity2D.normalized.y, position.z);
        Vector3 lineEnd = new Vector3(position.x + velocity2D.x * 2, position.y + velocity2D.y * 2, position.z);

        velocityVector.SetPosition(0, lineStart);
        velocityVector.SetPosition(1, lineEnd);

        float tcasXOffset = Mathf.Clamp(localVelocity3D.x, -1f, 1f);
        float tcasYOffset = Mathf.Clamp(localVelocity3D.y, -3f, 20f);

        tcas.offset = new Vector2(tcasXOffset, tcasYOffset);
    }


    #region Debugging
    void UpdateWaypointLine()
    {
        if (SettingsStatic.debug)
        {
            wpIndex = wpList.Count - 1;
            if (wpList.Count == 0) { return; }
            Vector3 position = gameObject.transform.position;

            waypointLine.SetPosition(0, gameObject.transform.position);
            waypointLine.SetPosition(1, new Vector3(wpList[wpIndex].wpCoordinates.x, wpList[wpIndex].wpCoordinates.y, 0f));

            if (wpList.Count > 1)
            {
                waypointLine2.SetPosition(0, new Vector3(wpList[wpIndex].wpCoordinates.x, wpList[wpIndex].wpCoordinates.y, 0f));
                waypointLine2.SetPosition(1, new Vector3(wpList[wpIndex - 1].wpCoordinates.x, wpList[wpIndex - 1].wpCoordinates.y, 0f));

            }
            else
            {
                waypointLine2.SetPosition(0, new Vector3(0f, 0f, 0f));
                waypointLine2.SetPosition(1, new Vector3(0f, 0f, 0f));
            }
        }         

    }

    void DrawDebugLine(Vector2 start, Vector2 end)
    {
        Vector3 startGlobal = new Vector3(start.x, start.y, 0f);
        Vector3 endGlobal = new Vector3(end.x, end.y, 0f);
        GameObject debugLine = Instantiate(DebugLine2D, new Vector3(0f, 0f, 0f), Quaternion.identity) as GameObject;
        LineRenderer debugLine2D = debugLine.GetComponent<LineRenderer>();

        debugLine2D.SetPosition(0, startGlobal);
        debugLine2D.SetPosition(1, endGlobal);

        Destroy(debugLine, 3f);
    }
    #endregion
}
