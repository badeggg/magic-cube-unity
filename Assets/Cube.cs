using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

struct TierId {
    HashSet<string> face;
    int level;
}
class Tier{
    TierId id;
    HashSet<GameObject> boxes;

    public Tier(TierId id, HashSet<GameObject> boxes){
        this.id = id;
        this.boxes = boxes;
    }
}
struct CubeRotateRoutine{
    public Boolean active;
    public String axis;
    public float gearRatio;
    public CubeRotateRoutine(float gearRatio, Boolean active, String axis){
        this.gearRatio = gearRatio;
        this.active = active;
        this.axis = axis;
    }
}

public class Cube : MonoBehaviour {
    Dictionary<TierId, Tier> tiers;
    int rank = 3;
    string cubeRotateInPlane;
    CubeRotateRoutine cubeRR = new CubeRotateRoutine(7, false, "");

	// Use this for initialization
	void Start () {
        ConstructCube();
        InitView();
	}
	
	// Update is called once per frame
	void Update () {
        if(Input.touchCount > 0){
            Touch firstFinger = Input.touches[0];
            if (firstFinger.phase == TouchPhase.Began){
                cubeRR.active = true;
                if(firstFinger.position.y < Screen.height/2){
                    cubeRR.axis = "yx"; //delta position within x rotate y axis, delta position within y rotate z axis
                } else{
                    cubeRR.axis = "zx";
                }
            } else if (firstFinger.phase == TouchPhase.Moved){
                if(cubeRR.active){
                    if (cubeRR.axis[0] == 'y'){
                        transform.Rotate(firstFinger.deltaPosition.y / cubeRR.gearRatio, -firstFinger.deltaPosition.x / cubeRR.gearRatio, 0, Space.World);
                    } else if (cubeRR.axis[0] == 'z'){
                        transform.Rotate(firstFinger.deltaPosition.y / cubeRR.gearRatio, 0, -firstFinger.deltaPosition.x / cubeRR.gearRatio, Space.World);
                    }
                }

            } else if(firstFinger.phase == TouchPhase.Ended){
                if(cubeRR.active){
                    cubeRR.active = false;
                }
            }
        }

        //if(Input.touchCount > 0){
        //    Console.WriteLine("screen wh: " + Screen.width + " " + Screen.height);
        //    Console.Write("=== frame touch count: " + Input.touchCount);
        //    foreach (Touch touch in Input.touches)
        //    {
        //        Console.Write(" phase " + touch.phase);
        //        Console.Write(" position " + touch.position);
        //        Console.Write(" deltaPosition " + touch.deltaPosition);
        //        Console.WriteLine("");
        //    }
        //}
	}

    void ConstructCube(){
        GameObject protoBox = GameObject.Find("box");
        float frontDistance = rank * 2 / 2 - 2 / 2;
        for (int x = 0; x < rank; x++){
            for (int y = 0; y < rank; y++){
                for (int z = 0; z < rank; z++){
                    GameObject box = Instantiate(protoBox, new Vector3(x*2 - frontDistance, y*2 - frontDistance, z*2 - frontDistance), Quaternion.identity, gameObject.transform);
                }
            }
        }
        Destroy(protoBox);
    }
    void InitView(){
        transform.Rotate(-18, 20, 0, Space.World);
    }
}
