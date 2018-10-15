using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ButtonBack : MonoBehaviour {
    private bool executable;
    Sprite BACK;
    Sprite BACK_GREY;

    // Use this for initialization
    void Start () {
        BACK = Resources.Load<Sprite>("Sprites/back");
        BACK_GREY = Resources.Load<Sprite>("Sprites/back-grey");
        executable = false;
        gameObject.GetComponent<Image>().sprite = BACK_GREY;
    }
	
	// Update is called once per frame
	void Update () {
		
	}
    public void CheckState(Cube cube){
        if (cube.records.First == cube.currentRecord){
            executable = false;
            gameObject.GetComponent<Image>().sprite = BACK_GREY;
        } else{
            executable = true;
            gameObject.GetComponent<Image>().sprite = BACK;
        }
    }
    public void exec(){
        if(!executable){
            return;
        }
        Cube cube = GameObject.Find("cube").GetComponent<Cube>();
        cube.Back();
    }
}
