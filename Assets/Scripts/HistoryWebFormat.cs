using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HistoryWebFormat {
    public string device_id;
    public string data;
    public string errMsg;
    public HistoryWebFormat(string device_id, string data){
        this.device_id = device_id;
        this.data = data;
    }
    public HistoryWebFormat(string device_id, string data, string errMsg){
        this.device_id = device_id;
        this.data = data;
        this.errMsg = errMsg;
    }
}
