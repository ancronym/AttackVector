using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public static class SpaceClearer {

  

    public static void ClearScenery(Vector3 positionToClear3D, float clearanceRadius){
        // Debug.Log("Scene clearer called");
        Vector2 positionToClear2D = new Vector2(positionToClear3D.x, positionToClear3D.y);
        Collider2D[] colliders = Physics2D.OverlapCircleAll(positionToClear2D,clearanceRadius);

        
        for (int i = 0; i < colliders.Length; i++)
        {
            // colliders[i].gameObject.GetComponent<LargeAsteroidScript>().Die();
            string name = colliders[i].gameObject.name;
            
            if (colliders[i].gameObject.tag == "Scenery")
            {
                // Debug.Log("Name of destroyable: " + name);
                UnityEngine.Object.Destroy(colliders[i].gameObject);
            }
        }           
        
    }


}
