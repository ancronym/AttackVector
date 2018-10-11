using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;   

public class PlayerController : MonoBehaviour {

	// TODO find out why this is negative!
	public float rotationSpeed = 0.1f;    

    enum Weapon { plasma, railgun, missile};

    Weapon selectedWeapon = Weapon.plasma;

    ShipController ship;
	LineRenderer velocityVector;
    public LineRenderer leadLine;
    FiringController fc;
    WeaponTextScript weaponText;
    FuelText fuelText;
    UIRadarText radarText;
    UIContactsText contactsText;
    RadarController.RadarState radarState;

    float desiredHeading = 0f; float aimX, aimY; Vector2 aimpoint;
    public float zoomSpeed = 5f;
    public float initialZoom = 10f;   

    
    bool alive;

    // Target indication 
    bool targetIndicatorEnabled;
    public GameObject targetIndicatorPrefab;
    public GameObject lockIndicator; public TextMesh lockText;
    public GameObject aimBracket;
    public Camera camera;
    GameObject targetIndicator;

    // ----------- UI STUFF ---------------
    public Slider fuelSlider;
    public Slider healthSlider; float initialHealth;
    GameObject menuCanvas; GameObject hintsCanvas;
    LevelManager levelManager;
    // For incoming locks    
    public GameObject RWRLinePrefab;
    public GameObject inLockLinePrefab;

    SettingsStatic.CamSettings camSetting; bool camPlayerSet;

    /* communications etc
     */
    public TeamManager teamManager;
    public TeamManager.TeamSide teamSide;

    List<GameObject> wingmen = new List<GameObject>(10);
    List<RadarController.Bogie> formationBogies = new List<RadarController.Bogie>(100);

    public List<RAMP.Waypoint> wpList = new List<RAMP.Waypoint>(10);
    int wpIndex;

    public VesselAI.FlightStatus flightStatus;

    public enum StatusInFormation { lead, Position2, Position3, Position4 };
    public StatusInFormation statusInFormation;

    void Start () {
        camSetting = (SettingsStatic.CamSettings)SettingsManager.GetCameraSetting();
        camPlayerSet = false;

        ScoreKeeper.shots = 0;
        ScoreKeeper.hits = 0;        
        ScoreKeeper.playerKills = 0;
        
        // TODO figure out a better way to do this
        LevelManager.isPaused = false;
        alive = true;

        ship = gameObject.GetComponentInParent<ShipController>();
        ship.owner = gameObject.GetComponent<PlayerController>();

        targetIndicatorEnabled = false;
        velocityVector = gameObject.transform.Find("VelocityVector").GetComponent<LineRenderer>();
        leadLine.enabled = false;
        fc = gameObject.GetComponent<FiringController>();
        //ship.IFF = "green";

        menuCanvas = GameObject.Find("MenuCanvas");
        hintsCanvas = GameObject.Find("HintsCanvas");
        // Debug.Log("Canvas found with name: " + menuCanvas.name);
        if (menuCanvas != null) { menuCanvas.SetActive(false); }
        if(hintsCanvas != null) { hintsCanvas.SetActive(false);}

        levelManager = GameObject.Find("LevelManager").GetComponent<LevelManager>();

        weaponText = GameObject.Find("UIWeaponText").GetComponent<WeaponTextScript>();
        radarText = GameObject.Find("UIRadarText").GetComponent<UIRadarText>();
        fuelSlider = GameObject.Find("FuelSlider").GetComponent<Slider>();
        healthSlider = GameObject.Find("HealthSlider").GetComponent<Slider>();
        contactsText = GameObject.Find("UIContactsText").GetComponent<UIContactsText>();
        initialHealth = ship.health;

        selectedWeapon = Weapon.plasma;
        weaponText.SetUIWeapontext("Plasma");
        radarText.SetUIRadarText("off");
        SetCursorGame();

        // float distance = transform.position.z - Camera.main.transform.position.z;        
        camera.fieldOfView = initialZoom;

        InvokeRepeating("UpdateUI", 0.00001f, 0.5f);
        Invoke("LateStart", 0.5f);
        lockIndicator.SetActive(false);

        ResizeMinimap();
    }

    void ResizeMinimap()
    {

    }
    
    void LateStart()
    {       
        ship.radar.teamSide = TeamManager.TeamSide.green;
    }
	
	// Update is called once per frame
	void Update () {

        if (Input.GetKey(KeyCode.Escape) && !LevelManager.isPaused) { levelManager.PauseGame(menuCanvas); }

        if (alive)
        {
            if (!LevelManager.isPaused)
            {
                switch (camSetting)
                {
                    case SettingsStatic.CamSettings.player:

                        desiredHeading += Input.GetAxis("Horizontal") * rotationSpeed;
                        float deltaTime = Time.deltaTime;
                        ship.Rotate(desiredHeading, deltaTime);
                        
                        camera.fieldOfView -= Input.GetAxis("MouseScrollWheel") * zoomSpeed;
                        camera.fieldOfView = Mathf.Clamp(camera.fieldOfView, 30f, 80f);
                        camera.transform.localPosition = new Vector3(
                            0,
                            camera.fieldOfView * 0.1f,
                            -30f
                            );

                        break;
                    case SettingsStatic.CamSettings.north:
                        camera.fieldOfView -= Input.GetAxis("MouseScrollWheel") * zoomSpeed;
                        camera.fieldOfView = Mathf.Clamp(camera.fieldOfView, 30f, 80f);
                        camera.transform.localPosition = new Vector3(
                            0,
                            camera.fieldOfView * 0.1f,
                            -30f
                            );

                        aimX -= Input.GetAxis("Horizontal"); 
                        aimY += Input.GetAxis("Vertical");                        

                        //Debug.Log("Aimx: " + aimX + " AimY: " + aimY);
                        Vector2 aimChange = new Vector2(aimX, aimY);
                        aimpoint += aimChange;

                        
                        desiredHeading = NTools.HeadingFromVector(aimpoint);
                        ship.Rotate(desiredHeading, Time.deltaTime);

                        //Debug.Log(Input.GetAxis("Horizontal"));

                        aimX = 0f; aimY = 0f;
                        break;
                }

                if (Input.GetKey(KeyCode.W))
                {
                    if (!Input.GetKey(KeyCode.LeftShift)) {ship.ThrustForward(1f); }
                    if (Input.GetKey(KeyCode.LeftShift)) { ship.ThrustForward(2f); }
                }                

                if (Input.GetKey(KeyCode.A)) {
                    if (!Input.GetKey(KeyCode.LeftShift)) { ship.ThrustLeft(1f); }
                    if (Input.GetKey(KeyCode.LeftShift)) { ship.ThrustLeft(2f); }
                }                

                if (Input.GetKey(KeyCode.S)) {
                    if (!Input.GetKey(KeyCode.LeftShift)) { ship.ThrustBackward(1f); }
                    if (Input.GetKey(KeyCode.LeftShift)) { ship.ThrustBackward(2f); }
                }                

                if (Input.GetKey(KeyCode.D)) {
                    if (!Input.GetKey(KeyCode.LeftShift)) { ship.ThrustRight(1f); }
                    if (Input.GetKey(KeyCode.LeftShift)) { ship.ThrustRight(2f); }
                }

                if (Input.GetKey(KeyCode.X)) { ship.Stop(); }

                if (Input.GetKeyDown(KeyCode.C))
                {
                    switch (camSetting)
                    {
                        case SettingsStatic.CamSettings.north:
                            camSetting = SettingsStatic.CamSettings.player;
                            camPlayerSet = false;
                            break;
                        case SettingsStatic.CamSettings.player:
                            camSetting = SettingsStatic.CamSettings.north;
                            break;
                    }                    
                }

                if (Input.GetKeyDown(KeyCode.Tab)) { if(hintsCanvas != null) { hintsCanvas.SetActive(true); } }
                if (Input.GetKeyUp(KeyCode.Tab)) { if (hintsCanvas != null) { hintsCanvas.SetActive(false); } }

                if (Input.GetKeyDown(KeyCode.Mouse0)) { ship.FireWeapon(true); }
                if (Input.GetKeyUp(KeyCode.Mouse0)) { ship.FireWeapon(false); }
                if (Input.GetKeyDown(KeyCode.Mouse1)) { ship.SetNearestTarget(); }

                if (Input.GetKeyDown(KeyCode.R)) { ship.ToggleRadar(); SetRadarText(); }
                if (Input.GetKeyDown(KeyCode.T)) {  ship.SetBoreTarget();  }
                if (Input.GetKeyDown(KeyCode.Y)) { ship.SetNearestTarget(); }

                if (Input.GetKeyDown(KeyCode.Alpha1)) { SelectWeaponByNumber(1); }
                if (Input.GetKeyDown(KeyCode.Alpha2)) { SelectWeaponByNumber(2); }
                if (Input.GetKeyDown(KeyCode.Alpha3)) { SelectWeaponByNumber(3); }
                if (Input.GetKeyDown(KeyCode.Alpha4)) { SelectWeaponByNumber(4); }
            }

            
            UpdateVelocityVector();
            UpdateTargetIndicator();
            UpdateLeadLine();
            UpdateAimAid();
            UpdateOwnLockIndicator();
            UpdateIncomingLockIndicator();
            UpdateCamera();
        }

	}

    void UpdateCamera()
    {
        switch (camSetting)
        {
            case SettingsStatic.CamSettings.player:
                if (!camPlayerSet)
                {
                    camera.transform.localEulerAngles = new Vector3(0f, 0f, 0f);
                    camPlayerSet = true;
                }               

                break;
            case SettingsStatic.CamSettings.north:                

                camera.transform.localEulerAngles = new Vector3(0f, 0f, -gameObject.transform.eulerAngles.z);
                break;
        }
    }

    void UpdateUI() {        
        SetFuelAndHealthBars();
        UpdateContacts();
    }
    
    void UpdateLeadLine()
    {
        if (targetIndicatorEnabled)
        {
            if (!leadLine.enabled) { leadLine.enabled = true; }

            Vector3 lineStart = new Vector3(0f,0f,0f);
            Vector3 lineEnd = new Vector3(0f, 0f, 0f);

            if (ship.activeWeapon.GetComponent<WepContr>().weaponCategory == WepContr.WeapCat.missile)
            {
                Vector3 vToTarget = ship.radar.target.transform.position - gameObject.transform.position;
                lineStart = gameObject.transform.position + vToTarget.normalized * 2;
                lineEnd = ship.radar.target.transform.position;
            }
            else
            {

                Vector2 aimpoint = RAMP.GetLeadPoint(
                    new Vector2(gameObject.transform.position.x, gameObject.transform.position.y),
                    new Vector2(ship.radar.target.transform.position.x, ship.radar.target.transform.position.y),
                    ship.radar.target.GetComponent<Rigidbody2D>().velocity,
                    gameObject.GetComponent<Rigidbody2D>().velocity,
                    ship.GetProjectileSpeed()
                    );

                Vector3 v2aimpoint = new Vector3(aimpoint.x, aimpoint.y, 0f) - gameObject.transform.position;

                lineStart = gameObject.transform.position + v2aimpoint.normalized * 2;
                lineEnd = new Vector3(aimpoint.x, aimpoint.y, 0f);
            }

            leadLine.SetPosition(0, lineStart);
            leadLine.SetPosition(1, lineEnd);

        } else
        {
            if (leadLine.enabled) { leadLine.enabled = false; }
        }
    }

    void UpdateAimAid()
    {
        if (ship.radar.target != null)
        {
            if(aimBracket.active == false) {
                if(ship.activeWeapon.GetComponent<WepContr>().weaponCategory != WepContr.WeapCat.missile)
                {
                    aimBracket.SetActive(true);
                } else
                {
                    aimBracket.SetActive(false);
                    
                }
            } else
            {
                if (aimBracket.active == true &&
                    ship.activeWeapon.GetComponent<WepContr>().weaponCategory == WepContr.WeapCat.missile)
                {
                    aimBracket.SetActive(false);
                    
                }
            }

            // Updating the aim bracket location
            Vector3 vToTarget = ship.radar.target.transform.position - gameObject.transform.position;
            vToTarget = gameObject.transform.InverseTransformVector(vToTarget);

            float fov = camera.fieldOfView;
            float y = vToTarget.y;

            y = Mathf.Clamp(y, 4f, fov * 0.25f);

            aimBracket.transform.localPosition = new Vector3(0f, y,0f);
        }
        else
        {
            if (aimBracket.active == true) { aimBracket.SetActive(false); }
            
        }
    }

    void UpdateVelocityVector()
    {
        Vector2 velocity = gameObject.GetComponent<Rigidbody2D>().velocity;
        Vector3 position = gameObject.transform.position;
        Vector3 lineStart = new Vector3(position.x + velocity.normalized.x, position.y + velocity.normalized.y, position.z);
        Vector3 lineEnd = new Vector3(position.x + velocity.x * 2, position.y + velocity.y * 2, position.z);

        velocityVector.SetPosition(0, lineStart);
        velocityVector.SetPosition(1, lineEnd);
    }

    void UpdateTargetIndicator()
    {
        if (ship.radar.target != null)
        {
            // Creates Target indicator if disabled
            if (targetIndicatorEnabled == false)
            {
                targetIndicator = Instantiate(
                    targetIndicatorPrefab,
                    ship.radar.target.transform.position,
                    Quaternion.identity
                    ) as GameObject;
                targetIndicatorEnabled = true;
            }
            else if (ship.radar.target && targetIndicator)
            {
                targetIndicator.transform.position = ship.radar.target.transform.position;

                targetIndicator.transform.Rotate(0f, 0f, 100f*Time.deltaTime);
            }


            // Debug.Log("TI pos: " + targetIndicator.transform.parent + "Tgt Pos: " + GameObject.Find(ship.radar.target).transform.position); 
        }

        // Destroys Target indicator if no target
        else
        {
            if (targetIndicatorEnabled == true)
            {
                Destroy(targetIndicator);
                targetIndicatorEnabled = false;
            }
        }

    }

    void UpdateIncomingLockIndicator()
    {
        Vector3 ownPos = gameObject.transform.position;        
        Vector3 LocalNormToLocker = new Vector3(0f, 0f, 0f);

        if(ship.radar.locksFromBaddie.Count != 0)
        {
            for(int i = 0; i < ship.radar.locksFromBaddie.Count; i++)
            {
                GameObject line = Instantiate(inLockLinePrefab, gameObject.transform.position, Quaternion.identity) as GameObject;
                // Debug.Log("LL ini.");
                line.GetComponent<LockIndicatorScript>().SetLockLineParams(
                    gameObject, 
                    ship.radar.locksFromBaddie[i],
                    LockIndicatorScript.LineType.Lock
                    );                
            }

            ship.radar.locksFromBaddie.Clear();
        }

        if(ship.radar.RWRContacts.Count != 0)
        {
            for (int i = 0; i < ship.radar.RWRContacts.Count; i++)
            {
                GameObject line = Instantiate(inLockLinePrefab, gameObject.transform.position, Quaternion.identity) as GameObject;
               // Debug.Log("RWR line ini");
                line.GetComponent<LockIndicatorScript>().SetLockLineParams(
                    gameObject,
                    ship.radar.RWRContacts[i],
                    LockIndicatorScript.LineType.RWR
                    );
            }
            ship.radar.RWRContacts.Clear();
        }

    }

    void UpdateOwnLockIndicator()
    {
        if (ship.radar.target != null)
        {
            if (!lockIndicator.active) { lockIndicator.SetActive(true); }

            switch (ship.activeWeapon.GetComponent<WepContr>().weaponCategory)
            {
                case WepContr.WeapCat.closeRange:
                    lockText.text = "DST: " +
                        (int)(gameObject.GetComponent<Rigidbody2D>().position - ship.radar.target.GetComponent<Rigidbody2D>().position).magnitude; ;
                    break;

                case WepContr.WeapCat.Ranged:
                    int ammo = ship.GetAmmo();
                    lockText.text = "AMMO: " + ammo.ToString();
                    break;

                case WepContr.WeapCat.missile:
                    lockText.text = "MLOCK / AMMO: " + ship.GetAmmo();
                    break;
            }
        }
        else
        {
            if (lockIndicator.active) { lockIndicator.SetActive(false); }
        }
    }

    void SelectNextWeapon() {
        string weaponName = ship.SelectNextWeapon();
        if(ship.activeWeapon.GetComponent<WepContr>().weaponCategory == WepContr.WeapCat.missile)
        {
            string missileMode = ship.activeWeapon.GetComponent<WepContr>().missileLaunchMode.ToString();
            weaponName = weaponName + " " + missileMode;
            Debug.Log(weaponName);
        }

        weaponText.SetUIWeapontext(weaponName);
    }

    void SelectWeaponByNumber(int weaponNumber)
    {
        ship.SelectWeaponByNumber(weaponNumber);
        string weaponName = ship.GetWeaponName();
        if(ship.activeWeapon.GetComponent<WepContr>().weaponCategory == WepContr.WeapCat.missile)
        {
            string firingMode = ship.activeWeapon.GetComponent<WepContr>().missileLaunchMode.ToString();
            weaponName += (" - " + firingMode);
        }

        weaponText.SetUIWeapontext(weaponName);
    }

    /* 
     * ----------------------   UI STUFF -------------------------------------------- 
     */

    void SetCursorGame(){
        Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;

	}

	void SetCursorUI(){
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = true;
	}

    void SetRadarText() {
        radarState = ship.GetRadarState();
        switch (radarState)
        {
            case RadarController.RadarState.off:
                radarText.SetUIRadarText("off");
                break;

            case RadarController.RadarState.wide:
                radarText.SetUIRadarText("wide");
                break;

            case RadarController.RadarState.narrow:
                radarText.SetUIRadarText("narrow");
                break;

        }
    }

    void UpdateContacts()
    {
        int TeamContacts = ship.radar.teamBogies.Count;
        int OwnContacts = ship.radar.radarBogies.Count;
        contactsText.SetContactsText(TeamContacts,OwnContacts);
    }

    void SetFuelAndHealthBars()    {
        fuelSlider.value = ship.fuel / ship.initialFuel * 100f;
        healthSlider.value = ship.health / initialHealth * 100f;
    }


    public void Death(){
        teamManager.ReportPlayerDeath(gameObject);

		SetCursorUI();
        
        gameObject.GetComponent<PolygonCollider2D>().enabled = false;
        
        gameObject.transform.Find("VelocityVector").GetComponent<LineRenderer>().enabled = false;

        alive = false;

        camera.transform.parent = camera.transform;

        
        Destroy(gameObject, 1f); 
		

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

            foreach (RadarController.Bogie bogie in bogieTemp)
            {
                bool bogieListed = false;
                for (int i = 0; i < formationBogies.Count; i++)
                {
                    if (bogie.bogieObject == formationBogies[i].bogieObject)
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

        float formationHealth = ship.health;


        foreach (GameObject wingman in wingmen)
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

        return new LFAI.FormationReport(
                formationBogies,
                hasWP,
                formationBogies.Count,
                flightSize,
                formationHealth,
                fuelIsBingo,
                fuelPercentageAverage,
                flightStatus
                );
    }

    public void ReportKill(GameObject target)
    {
        if (teamManager) { teamManager.ReportKill(target); }
        ScoreKeeper.playerKills++;        
    }

    public void ReportHit()
    {
        ScoreKeeper.hits++;
    }

    public void ReportShot()
    {
        ScoreKeeper.shots++;
    }

    public void ReportBogiesToTM(List<RadarController.Bogie> bogies)
    {
        if (!teamManager) { return; }
        foreach (RadarController.Bogie bogie in bogies)
        {
            teamManager.BogieSpotted(bogie);
        }
    }

    public void ShareBogies(List<RadarController.Bogie> bogies)
    {
        ship.radar.PassTeamBogies(bogies);
    }

    public void EnteredCivWp()
    {
        
        teamManager.ReportCivWpReached();
    }
}


