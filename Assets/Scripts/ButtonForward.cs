using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ButtonForward : MonoBehaviour {
    private bool executable;
    Sprite FORWARD;
    Sprite FORWARD_GREY;

    // Use this for initialization
    void Start () {
        FORWARD = Resources.Load<Sprite>("Sprites/forward");
        FORWARD_GREY = Resources.Load<Sprite>("Sprites/forward-grey");
        executable = false;
        gameObject.GetComponent<Image>().sprite = FORWARD_GREY;
    }
	
	// Update is called once per frame
	void Update () {
		
	}
    public void CheckState(Cube cube){
        if(cube.records.Last == cube.currentRecord){
            executable = false;
            gameObject.GetComponent<Image>().sprite = FORWARD_GREY;
        } else{
            executable = true;
            gameObject.GetComponent<Image>().sprite = FORWARD;
        }
    }
    public void exec(){
        if (!executable){
            return;
        }
        Cube cube = GameObject.Find("cube").GetComponent<Cube>();
        cube.Forward();
    }
}
