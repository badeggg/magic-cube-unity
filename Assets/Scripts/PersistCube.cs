using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text.RegularExpressions;

public class GameState{
    public LinkedList<CubeRecord> records;
    public LinkedListNode<CubeRecord> currentRecord;
    public int currentInxFromLast;
    public GameState(){
    }
    public GameState(LinkedList<CubeRecord> records, LinkedListNode<CubeRecord> currentRecord){
        this.records = records;
        this.currentRecord = currentRecord;
    }
    public GameState(LinkedList<CubeRecord> records, LinkedListNode<CubeRecord> currentRecord, int currentInxFromLast)
    {
        this.records = records;
        this.currentRecord = currentRecord;
        this.currentInxFromLast = currentInxFromLast;
    }
}

//maybe this is a bad choice, but I just struggling all day to using tools(json.net etc..) to persis status and all failed.
public class PersistCube {
    public string Serialize(GameState gameState) {
        string resultStr = "";
        {
            LinkedList<CubeRecord>  records = gameState.records;
            LinkedListNode<CubeRecord> recordNode = records.First;
            while (recordNode != null){
                resultStr += "|records";
                {
                    CubeTiersRotateRoutine.SequenceAutoRotateItem tierRotation = recordNode.Value.tierRotation;
                    if (tierRotation != null)
                    {
                        resultStr += "|-tierRotation";
                        {
                            resultStr += "|--id";
                            TierId id = tierRotation.id;
                            {
                                {
                                    resultStr += "|---face";
                                    Face face = id.face;
                                    resultStr += ("|---" + face.ToString());
                                    resultStr += "|---face";
                                }
                                {
                                    resultStr += "|---level";
                                    int level = id.level;
                                    resultStr += ("|---" + level);
                                    resultStr += "|---level";
                                }
                            }
                            resultStr += "|--id";
                        }
                        {
                            resultStr += "|--sign";
                            Sign sign = tierRotation.sign;
                            resultStr += ("|--" + sign);
                            resultStr += "|--sign";
                        }
                        resultStr += "|-tierRotation";
                    }
                }
                {
                    resultStr += "|-boxesState";
                    BoxState[] boxesState = recordNode.Value.boxesState;
                    for (int i = 0; i < boxesState.Length; i++)
                    {
                        resultStr += "|--boxState";
                        BoxState boxState = boxesState[i];
                        {
                            resultStr += "|---cubeCoord";
                            Vector3Int cubeCoord = boxState.cubeCoord;
                            resultStr += ("|---" + cubeCoord.x + " " + cubeCoord.y + " " + cubeCoord.z);
                            resultStr += "|---cubeCoord";
                        }
                        {
                            resultStr += "|---localRotation";
                            Quaternion localRotation = boxState.localRotation;
                            resultStr += ("|---" + localRotation.x + " " + localRotation.y + " " + localRotation.z + " " + localRotation.w);
                            resultStr += "|---localRotation";
                        }
                        {
                            Dictionary<String, Direction> squareDirections = boxState.squareDirections;
                            if (squareDirections != null && squareDirections.Keys.Count > 0)
                            {
                                resultStr += "|---squareDirections";
                                foreach (string key in squareDirections.Keys)
                                {
                                    resultStr += ("|----" + key + " " + squareDirections[key]);
                                }
                                resultStr += "|---squareDirections";
                            }
                        }
                        resultStr += "|--boxState";
                    }
                    resultStr += "|-boxesState";
                }
                recordNode = recordNode.Next;
                resultStr += "|records";
            }
        }
        {
            LinkedListNode<CubeRecord> currentRecord = gameState.currentRecord;
            int currentInxFromLast = 0;
            LinkedListNode<CubeRecord> searchingRecord = gameState.records.Last;
            while(searchingRecord != currentRecord){
                currentInxFromLast++;
                searchingRecord = searchingRecord.Previous;
            }
            resultStr += "|currentInxFromLast";
            resultStr += "|" + currentInxFromLast;
            resultStr += "|currentInxFromLast";
        }
        return resultStr;
    }
    public GameState Deserialize(string json){
        GameState gameState = new GameState();
        {
            LinkedList<CubeRecord> records = new LinkedList<CubeRecord>();
            Regex recordsReg = new Regex(@"\|records([\|\-\w\d\s\.]*?)\|records");
            MatchCollection recordsMatchs = recordsReg.Matches(json);
            for (int i = 0; i < recordsMatchs.Count; i++)
            {
                string recordStr = recordsMatchs[i].Groups[1].ToString();
                CubeRecord record = new CubeRecord();
                {
                    Regex tierRotationReg = new Regex(@"\|-tierRotation([\|\-\w\d\s\.]*?)\|-tierRotation");
                    MatchCollection tierRotationMatchs = tierRotationReg.Matches(recordStr);
                    if (tierRotationMatchs.Count > 0){
                        string tierRotationStr = tierRotationMatchs[0].Groups[1].ToString();
                        TierId id;
                        Sign sign;
                        {
                            Regex idReg = new Regex(@"\|--id([\|\-\w\d\s\.]*?)\|--id");
                            MatchCollection idMatchs = idReg.Matches(tierRotationStr);
                            string idStr = idMatchs[0].Groups[1].ToString();
                            Face face;
                            int level;
                            {
                                Regex faceReg = new Regex(@"\|---face([\|\-\w\d\s\.]*?)\|---face");
                                MatchCollection faceMatchs = faceReg.Matches(idStr);
                                string faceStr = faceMatchs[0].Groups[1].ToString().Remove(0, "|---".Length);
                                face = (Face)Enum.Parse(typeof(Face), faceStr);
                            }
                            {
                                Regex levelReg = new Regex(@"\|---level([\|\-\w\d\s\.]*?)\|---level");
                                MatchCollection levelMatchs = levelReg.Matches(idStr);
                                string levelStr = levelMatchs[0].Groups[1].ToString().Remove(0, "|---".Length);
                                level = int.Parse(levelStr);
                            }
                            id = new TierId(face, level);
                        }
                        {
                            Regex signReg = new Regex(@"\|--sign([\|\-\w\d\s\.]*?)\|--sign");
                            MatchCollection signMatchs = signReg.Matches(tierRotationStr);
                            string signStr = signMatchs[0].Groups[1].ToString().Remove(0, "|--".Length);
                            sign = (Sign)Enum.Parse(typeof(Sign), signStr);
                        }
                        record.tierRotation = new CubeTiersRotateRoutine.SequenceAutoRotateItem(id, sign);
                    } else{
                        record.tierRotation = null;
                    }
                }
                {
                    Regex boxesStateReg = new Regex(@"\|-boxesState([\|\-\w\d\s\.]*?)\|-boxesState");
                    MatchCollection boxesStateMatchs = boxesStateReg.Matches(recordStr);
                    string boxesStateStr = boxesStateMatchs[0].Groups[1].ToString();
                    BoxState[] boxesState;
                    {
                        Regex boxStateReg = new Regex(@"\|--boxState([\|\-\w\d\s\.]*?)\|--boxState");
                        MatchCollection boxStateMatchs = boxStateReg.Matches(boxesStateStr);
                        boxesState = new BoxState[boxStateMatchs.Count];
                        for (int j = 0; j < boxStateMatchs.Count; j++)
                        {
                            string boxStr = boxStateMatchs[j].Groups[1].ToString();
                            Vector3Int cubeCoord;
                            Quaternion localRotation;
                            Dictionary<String, Direction> squareDirections;
                            {
                                Regex cubeCoordReg = new Regex(@"\|---cubeCoord([\|\-\w\d\s\.]*?)\|---cubeCoord");
                                MatchCollection cubeCoordMatchs = cubeCoordReg.Matches(boxStr);
                                string cubeCoordStr = cubeCoordMatchs[0].Groups[1].ToString().Remove(0, "|---".Length);
                                string[] xyz = cubeCoordStr.Split(new char[] { ' ' }, StringSplitOptions.None);
                                cubeCoord = new Vector3Int(int.Parse(xyz[0]), int.Parse(xyz[1]), int.Parse(xyz[2]));
                            }
                            {
                                Regex localRotationReg = new Regex(@"\|---localRotation([\|\-\w\d\s\.]*?)\|---localRotation");
                                MatchCollection localRotationMatchs = localRotationReg.Matches(boxStr);
                                string localRotationStr = localRotationMatchs[0].Groups[1].ToString().Remove(0, "|---".Length);
                                string[] wxyz = localRotationStr.Split(new char[] { ' ' }, StringSplitOptions.None);
                                localRotation = new Quaternion(float.Parse(wxyz[0]), float.Parse(wxyz[1]), float.Parse(wxyz[2]), float.Parse(wxyz[3]));
                            }
                            {
                                Regex squareDirectionsReg = new Regex(@"\|---squareDirections([\|\-\w\d\s\.]*?)\|---squareDirections");
                                MatchCollection squareDirectionsMatchs = squareDirectionsReg.Matches(boxStr);
                                squareDirections = new Dictionary<string, Direction>();
                                if (squareDirectionsMatchs.Count > 0)
                                {
                                    string[] squareDirectionsStrs = squareDirectionsMatchs[0].Groups[1].ToString().Split(new string[] { "|----" }, StringSplitOptions.RemoveEmptyEntries);
                                    for (int k = 0; k < squareDirectionsStrs.Length; k++)
                                    {
                                        string[] squareDirectionKeyValue = squareDirectionsStrs[k].Split(new char[] { ' ' }, StringSplitOptions.None);
                                        squareDirections.Add(squareDirectionKeyValue[0], (Direction)Enum.Parse(typeof(Direction), squareDirectionKeyValue[1]));
                                    }
                                }
                            }
                            BoxState boxState;
                            boxState.cubeCoord = cubeCoord;
                            boxState.localRotation = localRotation;
                            boxState.squareDirections = squareDirections;
                            boxesState[j] = boxState;
                        }
                    }
                    record.boxesState = boxesState;
                }
                records.AddLast(record);
            }
            gameState.records = records;
        }
        {
            int currentInxFromLast = -1;
            Regex currentInxFromLastReg = new Regex(@"\|currentInxFromLast([\|\-\w\d\s\.]*?)\|currentInxFromLast");
            MatchCollection currentInxFromLastMatchs = currentInxFromLastReg.Matches(json);
            string currentInxFromLastStr = currentInxFromLastMatchs[0].Groups[1].ToString().Remove(0, "|".Length);
            currentInxFromLast = int.Parse(currentInxFromLastStr);
            int searchingInx = 0;
            LinkedListNode<CubeRecord> searchingRecord = gameState.records.Last;
            while(searchingInx < currentInxFromLast){
                searchingInx++;
                searchingRecord = searchingRecord.Previous;
            }
            gameState.currentRecord = searchingRecord;
            gameState.currentInxFromLast = currentInxFromLast;
        }
        return gameState;
    }
}
