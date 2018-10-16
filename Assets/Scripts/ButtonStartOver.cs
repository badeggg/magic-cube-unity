using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ButtonStartOver : MonoBehaviour {

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}
    public void exec(){
        Cube cube = GameObject.Find("cube").GetComponent<Cube>();
        cube.StartOver();
    }
}
