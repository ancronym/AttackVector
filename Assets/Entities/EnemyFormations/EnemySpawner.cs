using UnityEngine;
using System.Collections;

public class EnemySpawner : MonoBehaviour {
	public GameObject enemyPrefab;

	public float width = 10f;
	public float height = 5f;
	public float buffer = 0f;
	public float formationSpeed = 1f;
	public float spawnDelay = 0.2f;

	float minX;
	float maxX;

	enum direction{left,right};

	direction enemyDirection = direction.right;

	void OnDrawGizmos(){
		Gizmos.DrawWireCube(gameObject.transform.position,new Vector3(width, height));
	}

	// Use this for initialization
	void Start () {	
		float distance = gameObject.transform.position.z - Camera.main.transform.position.z;

		Vector3 leftmost = Camera.main.ViewportToWorldPoint (new Vector3(0, 0, distance));
		Vector3 rightmost = Camera.main.ViewportToWorldPoint (new Vector3(1, 0, distance));
		minX = leftmost.x + (width / 2) + buffer;
		maxX = rightmost.x - (width / 2) - buffer;

		SpawnUntilFull ();
	}
	
	// Update is called once per frame
	void Update () {
		switch(enemyDirection){
		case direction.left:
			gameObject.transform.position += Vector3.left * Time.deltaTime*formationSpeed;
			break;
		case direction.right:
			gameObject.transform.position += Vector3.right *Time.deltaTime*formationSpeed;
			break;
		}

		if (gameObject.transform.position.x <= (minX + 0.1f)) {
			enemyDirection = direction.right;
		} else if (gameObject.transform.position.x >= (maxX - 0.1f)) {
			enemyDirection = direction.left;
		}	

		float newX = Mathf.Clamp (gameObject.transform.position.x, minX, maxX);
		gameObject.transform.position = new Vector3 (newX, gameObject.transform.position.y, gameObject.transform.position.z);

		if (AllMembersDead ()) {
			Debug.Log("Empty Formation");
			SpawnUntilFull();
		}
	}

	Transform NextFreePosition(){
		foreach (Transform childPosition in transform) {
			if(childPosition.childCount == 0){
				return childPosition;
			}
		}
		return null;
	}

	bool AllMembersDead(){

		foreach (Transform childPositionGameObject in transform) {
			if(childPositionGameObject.childCount>0){
				return false;
			}
		}
		return true;
	}

	void SpawnUntilFull(){
		Transform nextFree = NextFreePosition ();
		if (nextFree) {
			GameObject enemy = Instantiate(enemyPrefab,nextFree.position,Quaternion.identity) as GameObject;
			enemy.transform.parent = nextFree;
		}
		if (NextFreePosition ()) {
			Invoke ("SpawnUntilFull",spawnDelay);
		}
	}

	void RespawnEnemies(){
		foreach (Transform child in transform) {
			GameObject enemy = Instantiate (enemyPrefab, child.transform.position,Quaternion.identity) as GameObject;
			enemy.transform.parent = child;
		}

	}
}
