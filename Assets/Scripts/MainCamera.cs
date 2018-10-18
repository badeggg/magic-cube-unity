using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class MainCamera : MonoBehaviour {
    string serverUrl = "localhost:8083";

    // Use this for initialization
    void Start () {
        //if(PlayerPrefs.GetString("INITIATED", "FALSE") == "FALSE"){
        //    PlayerPrefs.SetString("INITIATED", "TRUE");

        //    //notice: 'SystemInfo.deviceUniqueIdentifier' work only for ios
        //    WWWForm form = new WWWForm();
        //    form.AddField("device_id", SystemInfo.deviceUniqueIdentifier);
        //    UnityWebRequest www = UnityWebRequest.Post(serverUrl + "/user/add", form);
        //    www.SendWebRequest();
        //}
        /////
        Console.WriteLine("SystemInfo.deviceUniqueIdentifier: " + SystemInfo.deviceUniqueIdentifier);
        Console.WriteLine("SystemInfo.unsupportedIdentifier: " + SystemInfo.unsupportedIdentifier);
    }
    
    // Update is called once per frame
    void Update () {
        
    }
}
