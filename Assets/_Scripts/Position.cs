using UnityEngine;
using System.Collections;

public class Position : MonoBehaviour {

    public GameObject wpAssignedTo;
    public bool posOccupied;

	void OnDrawGizmos(){
		Gizmos.DrawWireSphere(transform.position,1);
	}
}
