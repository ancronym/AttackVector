using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Nozzle : MonoBehaviour {

    public enum ThrusterDirection { left, right, back, front};
    public ThrusterDirection thrusterDirection;

	GameObject thrustEffect;
    public ParticleSystem afterBurner;
    ParticleSystem abEffect;
    bool thrustOn;    
    float emitStartTime;
    float emitCalledTime;
    float thrustDuration = 10f;

	void Start(){

        abEffect = Instantiate(afterBurner, gameObject.transform.position, Quaternion.identity) as ParticleSystem;
        abEffect.transform.parent = gameObject.transform;

        thrustEffect = gameObject.transform.Find("ThrustEffect").gameObject;
        emitStartTime = Time.timeSinceLevelLoad;

        switch (thrusterDirection)
        {
            case ThrusterDirection.back:
                abEffect.transform.eulerAngles = new Vector3(90, 0, 0);
                abEffect.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                break;

            case ThrusterDirection.front:
                abEffect.transform.eulerAngles = new Vector3(-90, 0, 0);
                break;

            case ThrusterDirection.left:
                abEffect.transform.eulerAngles = new Vector3(0, -90, 0);
                break;

            case ThrusterDirection.right:
                abEffect.transform.eulerAngles = new Vector3(0, 90, 0);
                break;
        }

        if (thrustEffect) {
            thrustEffect.SetActive(false);
            // Debug.Log("thrust effect found: " + thrustEffect.name);
        }
    }

	public void EmitThrust(float throttle){
        emitCalledTime = Time.timeSinceLevelLoad;

        if (!thrustOn)
        {
            emitStartTime = emitCalledTime;
            thrustOn = true;
            thrustEffect.SetActive(true);
        }

        // Sanity check
        if (!afterBurner) { return; }

        if (throttle > 1)
        {
            if (abEffect.isStopped)
            {
                abEffect.Play();
            }            
        }
        else
        {
            if (abEffect.isPlaying)
            {
                abEffect.Stop();
            }
        }
    }

    private void LateUpdate()
    {
        if((Time.timeSinceLevelLoad - Time.deltaTime * thrustDuration) > emitStartTime && (Time.timeSinceLevelLoad - Time.deltaTime * thrustDuration) > emitCalledTime)
        {
            StopThrust();
        }
    }   

    public void StopThrust()
    {
        if (abEffect.isPlaying)
        {
            abEffect.Stop();
        }

        if (thrustOn)
        {
            thrustOn = false;
            thrustEffect.SetActive(false);
        }
    }
}
