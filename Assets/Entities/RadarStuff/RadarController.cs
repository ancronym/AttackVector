using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RadarController : MonoBehaviour {

    static int nrOfRadar;
        
    public float narrowBeamMultiplier = 0.5f;
    public float wideBeamMultiplier = 1f;
    public float opticalRange = 30f;
    public float radarRange = 150f;
    float radarScanRate = 2f;

    public GameObject target = null;
    
    public enum RadarState { off, narrow, wide};
    public RadarState radarState;
    public TeamManager.TeamSide teamSide;

    // public GameObject pingPrefab;
    public CapsuleCollider2D radarCollider;   
    public LayerMask greenLayerMask;
    public LayerMask redLayerMask;
    
    public class Bogie
    {
        public GameObject bogieObject;
        public float timeOfContact;
        public float sqrDistance;

        public Bogie(GameObject bogie, float contactTime, float sqrdistance) {
            bogieObject = bogie;
            timeOfContact = contactTime;
            sqrDistance = sqrdistance;
        }
    }

    public float bogieTimeout = 10f;
    public List<Bogie> radarBogies = new List<Bogie>(100);
    public List<Bogie> teamBogies = new List<Bogie>(100);

    public List<Vector3> RWRContacts = new List<Vector3>(50);
    public List<Vector3> locksFromBaddie = new List<Vector3>(50);
    
    // Use this for initialization
    void Start () {
        nrOfRadar++;

        radarCollider.offset = new Vector2(0f, 100f);
        radarCollider.size = new Vector2(250f,250f);
        
        InvokeRepeating("TimeOutBogies", 5f, 2f + UnityEngine.Random.Range(-0.5f,0.5f));

        SetRadarOff();
        SetRadarOn();
        
    }

    void Update() {
        
    
     
    }

    // Removes bogies, that have gone stale i.e the last contact is longer ago than timeout parameter
    private void TimeOutBogies()
    {
        int removed = 0;
        // Debug.Log("Bogies before: " + radarBogies.Count);

        ContactFilter2D filter = new ContactFilter2D();

        switch (teamSide)
        {
            case TeamManager.TeamSide.green:
                filter.SetLayerMask(redLayerMask);
                break;
            case TeamManager.TeamSide.red:
                filter.SetLayerMask(greenLayerMask);
                break;
        }

        if (radarState == RadarState.wide || radarState == RadarState.narrow)
        {
            for (int i = 0; i < radarBogies.Count; i++)
            {
                bool touchingRadar = false;                
                if (radarBogies[i].bogieObject != null)
                {

                    touchingRadar = radarCollider.IsTouching(radarBogies[i].bogieObject.GetComponent<PolygonCollider2D>());                    

                    if (!touchingRadar)
                    {
                        removed++;
                        radarBogies.RemoveAt(i);
                    }
                }
            }
        }
        else
        {
            radarBogies.Clear();
        }

        // Debug.Log("removed: " + removed + " nrOfBogies: " + radarBogies.Count);
    }
	
    // it returns the target GO, but also retains reference to it
	public GameObject GetNearestTarget()
    {
        // Resetting target
        target = null;

        if (radarBogies.Count != 0)
        {
            int closestTargetIndex = 0;

            for (int i = 1; i < radarBogies.Count; i++)
            {
                if (radarBogies[i].bogieObject != null)
                {
                    if (radarBogies[i].sqrDistance < radarBogies[closestTargetIndex].sqrDistance)
                    {
                        closestTargetIndex = i;
                    }
                }
            }

            target = radarBogies[closestTargetIndex].bogieObject;
            SendLockToTarget();
        }
        else
        {
            if (teamBogies.Count != 0)
            {
                int closestTargetIndex = 0;

                for (int i = 1; i < teamBogies.Count; i++)
                {
                    if (teamBogies[i].bogieObject != null)
                    {
                        if (teamBogies[i].sqrDistance < teamBogies[closestTargetIndex].sqrDistance)
                        {
                            closestTargetIndex = i;
                        }
                    }
                }
                target = teamBogies[closestTargetIndex].bogieObject;
                SendLockToTarget();
            }            
        }

        return target;
    }


    // woah, amazing
    public GameObject GetBoresightTarget()
    {
        // Resetting target
        target = null;

        if (radarBogies.Count != 0)
        {
            float boreDistClosest = 0f;

            int closestTargetIndex = 0;
            for (int i = 0; i < radarBogies.Count; i++)
            {
                if (radarBogies[i].bogieObject != null)
                {
                    boreDistClosest = Mathf.Abs(gameObject.transform.InverseTransformPoint(radarBogies[0].bogieObject.transform.position).x);
                    break;
                }
            }

            for (int i = 1; i < radarBogies.Count; i++)
            {

                if (radarBogies[i].bogieObject != null)
                {
                    if (Mathf.Abs(gameObject.transform.InverseTransformPoint(radarBogies[i].bogieObject.transform.position).x) < boreDistClosest)
                    {
                        boreDistClosest = Mathf.Abs(gameObject.transform.InverseTransformPoint(radarBogies[i].bogieObject.transform.position).x);
                        closestTargetIndex = i;
                    }
                }
            }
            target = radarBogies[closestTargetIndex].bogieObject;
            SendLockToTarget();

        }else if(radarBogies.Count == 0)
        {
            if(teamBogies.Count != 0)
            {
                float boreDistClosest = 0f;

                int closestTargetIndex = 0;
                for (int i = 0; i < teamBogies.Count; i++)
                {
                    if (teamBogies[i].bogieObject != null)
                    {
                        boreDistClosest = Mathf.Abs(gameObject.transform.InverseTransformPoint(teamBogies[0].bogieObject.transform.position).x);
                        break;
                    }
                }

                for (int i = 1; i < teamBogies.Count; i++)
                {

                    if (teamBogies[i].bogieObject != null)
                    {
                        if (Mathf.Abs(gameObject.transform.InverseTransformPoint(teamBogies[i].bogieObject.transform.position).x) < boreDistClosest)
                        {
                            boreDistClosest = Mathf.Abs(gameObject.transform.InverseTransformPoint(teamBogies[i].bogieObject.transform.position).x);
                            closestTargetIndex = i;
                        }
                    }
                }
                target = teamBogies[closestTargetIndex].bogieObject;
                SendLockToTarget();
            }

        } 
        

        return target;        
    }

    public List<RadarController.Bogie> Getbogies()
    {
        List<Bogie> bogies = new List<Bogie>(50);

        foreach(Bogie bogie in radarBogies)
        {
            bogies.Add(bogie);
        }

        return bogies;
    }

    // checks all unfriendly contacts in the collider and updates bogie list
    void Ping() {
        bool bogieListed = false;
        float distance = 0f;

        ContactFilter2D filter = new ContactFilter2D();              

        switch (teamSide)
        {
            case TeamManager.TeamSide.green:
                filter.SetLayerMask(redLayerMask);
                break;
            case TeamManager.TeamSide.red:
                filter.SetLayerMask(greenLayerMask);
                break;
        }

        Collider2D[] contacts = new Collider2D[100];
        

        int nrContacts = radarCollider.OverlapCollider(filter,contacts);       

        if(nrContacts == 0) { return; }

        for (int i = 0; i < nrContacts; i++)
        {
            bogieListed = false;

            // Debug.Log("Collider go name: " + contacts[i].gameObject.name);

            // check listed bogies for match
            for (int j = 0; j < radarBogies.Count; j++)
            {
                contacts[i].gameObject.GetComponentInChildren<RadarController>().IncomingRWR(gameObject.transform.position, gameObject.transform.parent.gameObject);

                // if matched bogie found, update time and distance
                if (radarBogies[j].bogieObject != null){                 

                    if (contacts[i].gameObject == radarBogies[j].bogieObject)
                    {
                        radarBogies[j].timeOfContact = Time.timeSinceLevelLoad;
                        radarBogies[j].sqrDistance = Vector3.SqrMagnitude(contacts[i].transform.position - gameObject.transform.position);
                        bogieListed = true;
                        break;
                    }
                } else if (radarBogies[i].bogieObject == null)
                {
                    radarBogies.RemoveAt(i);
                }
            }

            // if the bogie was not found in the list, add it with all data
            if (!bogieListed && contacts[i].gameObject != null)
            {
                distance = (contacts[i].gameObject.transform.position - gameObject.transform.position).magnitude;
                float detectability = 1f;

                if(contacts[i].GetComponent<ShipController>() != null)
                {
                    detectability  = contacts[i].GetComponent<ShipController>().detectability;
                } else if(contacts[i].GetComponent<StationAI>() != null)
                {
                    detectability = contacts[i].GetComponent<StationAI>().detectability;
                }

                if (contacts[i].GetComponent<ShipController>())
                {
                    if (contacts[i].GetComponent<ShipController>().detectability * radarRange > distance)
                    {

                        radarBogies.Add(new Bogie(
                            contacts[i].gameObject,
                            Time.timeSinceLevelLoad,
                            Vector3.SqrMagnitude(contacts[i].transform.position - gameObject.transform.position)
                            ));
                    }
                }
            }  
        }        
    }

    void See()
    {
        bool bogieListed = false;
        float distance = 0f;

        ContactFilter2D filter = new ContactFilter2D();

        switch (teamSide)
        {
            case TeamManager.TeamSide.green:
                filter.SetLayerMask(redLayerMask);
                break;
            case TeamManager.TeamSide.red:
                filter.SetLayerMask(greenLayerMask);
                break;
        }

        Collider2D[] contacts = new Collider2D[50];
        int nrContacts = radarCollider.OverlapCollider(filter, contacts);

        if (nrContacts == 0) { return; }

        for (int i = 0; i < nrContacts; i++)
        {
            bogieListed = false;

            // Debug.Log("Collider go name: " + contacts[i].gameObject.name);

            // check listed bogies for match
            for (int j = 0; j < radarBogies.Count; j++)
            {
                // if matched bogie found, update time and distance
                if (radarBogies[j].bogieObject != null)
                {
                    if (contacts[i].gameObject == radarBogies[j].bogieObject)
                    {
                        radarBogies[j].timeOfContact = Time.timeSinceLevelLoad;
                        radarBogies[j].sqrDistance = Vector3.SqrMagnitude(contacts[i].transform.position - gameObject.transform.position);
                        bogieListed = true;
                    }
                }
                else if (radarBogies[i].bogieObject == null)
                {
                    radarBogies.RemoveAt(i);
                }
            }

            // if the bogie was not found in the list, add it with all data
            if (!bogieListed)
            {
                distance = (contacts[i].gameObject.transform.position - gameObject.transform.position).magnitude;

                if (contacts[i].GetComponent<ShipController>().detectability * radarRange > distance)
                {

                    radarBogies.Add(new Bogie(
                        contacts[i].gameObject,
                        Time.timeSinceLevelLoad,
                        Vector3.SqrMagnitude(contacts[i].transform.position - gameObject.transform.position)
                        ));
                }

            }

        }
    }

    private void SendLockToTarget()
    {
        if(target != null)
        {
            target.GetComponentInChildren<RadarController>().IncomingLock(gameObject.transform.position);
        }
    }

    public void IncomingLock(Vector3 lockSource)
    {
        locksFromBaddie.Add(lockSource);
    }

    public void IncomingRWR(Vector3 pingPos, GameObject pinger)
    {
        RWRContacts.Add(pingPos);

        bool bogieListed = false;

        for(int i = 0; i < radarBogies.Count; i++)
        {
            if(radarBogies[i].bogieObject != null)
            {
                if(radarBogies[i].bogieObject == pinger)
                {
                    bogieListed = true;
                    break;
                }
            }
            else
            {
                radarBogies.RemoveAt(i);
            }
        }

        if (!bogieListed)
        {
            radarBogies.Add(new Bogie(
                pinger,
                Time.timeSinceLevelLoad,
                (gameObject.transform.position - pinger.transform.position).sqrMagnitude
                ));
        }
    }

    public List<Vector3> GetRWR()
    {
        return RWRContacts;
    }

    public void ClearRWR()
    {
        RWRContacts.Clear();
    }

    public void PassTeamBogies(List<Bogie> aBogies)
    {
        teamBogies = aBogies;
    }

    public void ToggleRadar() {
        switch (radarState) {
            case RadarState.off:
                SetRadarWide();
                break;
            case RadarState.narrow:
                SetRadarOff();
                break;
            case RadarState.wide:
                SetRadarOff();
                break;
        }
    }

    public void ToggleRadarWidth(){
        switch (radarState){
            case RadarState.off:
                SetRadarWide();
                break;
            case RadarState.narrow:
                SetRadarWide();
                break;
            case RadarState.wide:
                SetRadarNarrow();
                break;
        }
    }

    public void SetRadarNarrow(){
        if (radarState == RadarState.off)
        {
            InvokeRepeating("Ping", 0.01f, radarScanRate);
            CancelInvoke("See");
        }
        radarState = RadarState.narrow;
        
    }

    public void SetRadarWide() {
        if (radarState == RadarState.off)
        {
            InvokeRepeating("Ping", 0.01f, radarScanRate);
            CancelInvoke("See");
        }
        radarState = RadarState.wide;
    }

    public void SetRadarOff() {
        
        if (radarState != RadarState.off)
        {
            CancelInvoke("Ping");
            InvokeRepeating("See", 0.1f, radarScanRate);
            target = null;
        }
        radarState = RadarState.off;
    }

    public void SetRadarOn(){
        
        if (radarState == RadarState.off)
        {
            InvokeRepeating("Ping", 0.01f, radarScanRate);
            CancelInvoke("See");
        }
        radarState = RadarState.wide;
    }

}
