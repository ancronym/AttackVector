using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PIDControllerFormation : MonoBehaviour {

	float[] previousErrors = new float[6];

	public float pGain = 0.05f;
	public float iGain = 0.05f;
	public float dGain = 0.05f;

	// returns a float correction pased on PID gains
	public float Correction(float DV, float CV,float deltaTime){
		float correction = 0f;

		float proportional = DV-CV;
		float integral = GetIntegral (deltaTime);
		float derivative = GetDerivative (proportional,deltaTime);

		correction = proportional * pGain + integral * iGain + derivative * dGain;
		// Debug.Log ("P: " + proportional + "I: " + integral + "D: " + derivative);

		SetPreviousErrors (proportional);	
		return correction;
	}

	float GetIntegral(float deltaTime){
		float integral = 0f;
		for (int i = 0; i <=5; i++){
			integral += previousErrors[i];
		}
		integral = (integral / previousErrors.Length)*deltaTime;
		return integral;
	}

	// latest error is the same as proportional
	float GetDerivative(float latestError, float deltaTime){
		float derivative = (latestError-previousErrors [0])/deltaTime;
		return derivative;
	}

	void SetPreviousErrors(float latestError){
		for (int i = 5; i >= 1; i--) {
			previousErrors [i] = previousErrors [i - 1];
		}
		previousErrors [0] = latestError;
	}
}
