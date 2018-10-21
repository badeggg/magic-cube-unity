using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class MainCamera : MonoBehaviour {
    string serverBasePath = "https://magcb.club/magic-cube";
    Cube cube;

    // Use this for initialization
    void Start () {
        cube = GameObject.Find("cube").GetComponent<Cube>();
        if (PlayerPrefs.GetString("INITIATED", "FALSE") == "FALSE"){
            StartCoroutine(AddUser());
            cube.phase = CubePhase.playing;
        } else{
            StartCoroutine(GetHistory());
        }
    }
    IEnumerator AddUser(){
        //notice: 'SystemInfo.deviceUniqueIdentifier' work only for ios
        WWWForm form = new WWWForm();
        form.AddField("device_id", SystemInfo.deviceUniqueIdentifier);
        using( UnityWebRequest www = UnityWebRequest.Post(serverBasePath + "/user/add", form) ){
            yield return www.SendWebRequest();
            if(www.isNetworkError || www.isHttpError){
                Debug.Log(www.error);
            } else{
                PlayerPrefs.SetString("INITIATED", "TRUE");
            }
        }
    }
    IEnumerator GetHistory(){
        using( UnityWebRequest www = UnityWebRequest.Get(serverBasePath + "/history/?device_id=" + SystemInfo.deviceUniqueIdentifier) ){
            yield return www.SendWebRequest();
            if (www.isNetworkError || www.isHttpError){
                Debug.Log(www.error);
                cube.phase = CubePhase.playing;
            } else{
                string history = www.downloadHandler.text;
                if (cube.phase == CubePhase.historyLoading && history.StartsWith("|records")){
                    PersistCube pc = new PersistCube();
                    GameState gameState = pc.Deserialize(history);
                    cube.LoadGameState(gameState);
                    cube.phase = CubePhase.playing;
                }
            }
        }
    }

    // Update is called once per frame
    void Update () {
        
    }
}
