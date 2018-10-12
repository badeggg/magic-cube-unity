using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RandomButton : MonoBehaviour {

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}
    public void exec(){
        CubeTiersRotateRoutine.SequenceAutoRotateItem[] items = new CubeTiersRotateRoutine.SequenceAutoRotateItem[10];
        Cube cube = GameObject.Find("cube").GetComponent<Cube>();
        int cubeRank = cube.GetComponent<Cube>().rank;
        System.Random random = new System.Random();
        System.Array faces = System.Enum.GetValues(typeof(Face));
        System.Array signs = System.Enum.GetValues(typeof(Sign));

        for (int i = 0; i < items.Length; i++){
            Face face = (Face)faces.GetValue(random.Next(0, 3));
            int level = random.Next(0, cubeRank);
            TierId id = new TierId(face, level);
            Sign sign = (Sign)signs.GetValue(random.Next(0, 2));
            items[i] = new CubeTiersRotateRoutine.SequenceAutoRotateItem(id, sign);
        }
        cube.SequenceAutoRotateTier(items);
    }
}
