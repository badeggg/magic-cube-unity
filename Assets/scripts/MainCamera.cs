using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainCamera : MonoBehaviour {

	// Use this for initialization
	void Start () {
        Console.WriteLine("SystemInfo.deviceUniqueIdentifier: " + SystemInfo.deviceUniqueIdentifier);
        Console.WriteLine("SystemInfo.unsupportedIdentifier: " + SystemInfo.unsupportedIdentifier);
	}
	
	// Update is called once per frame
	void Update () {
		
	}
}
