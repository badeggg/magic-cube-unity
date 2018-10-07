using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

class CubicBezierCurve{
    Vector2 controlPoint1;
    Vector2 controlPoint2;
    public CubicBezierCurve(Vector2 p1, Vector2 p2){
        this.controlPoint1 = p1;
        this.controlPoint2 = p2;
    }
    public float DeriveYFromX(float xTarget){
        float tolerance = 0.0001F;
        float t0 = 0.6F;
        float x = 3 * (1 - t0) * (1 - t0) * t0 * controlPoint1.x + 3 * (1 - t0) * t0 * t0 * controlPoint2.x + t0 * t0 * t0;
        float t = -1.0F;
        while (Math.Abs(x - xTarget) > tolerance)
        {
            t = t0 - (3 * (1 - t0) * (1 - t0) * t0 * controlPoint1.x + 3 * (1 - t0) * t0 * t0 * controlPoint2.x + t0 * t0 * t0 - xTarget) / (3 * (1 - t0) * (1 - t0) * controlPoint1.x + 6 * (1 - t0) * t0 * (controlPoint2.x - controlPoint1.x) + 3 * t0 * t0 * (1 - controlPoint2.x));
            t0 = t;
            x = 3 * (1 - t0) * (1 - t0) * t0 * controlPoint1.x + 3 * (1 - t0) * t0 * t0 * controlPoint2.x + t0 * t0 * t0;
        }
        return 3*(1-t)*(1-t)*t*controlPoint1.y + 3*(1-t)*t*t*controlPoint2.y + t*t*t;
    }
}
public enum Face { xy, xz, yz };
enum Axis { x, y, z, empty };
public struct TierId {
    public Face face;
    public int level;
    public TierId(Face face, int level){
        this.face = face;
        this.level = level;
    }
}

public class Tier{
    public TierId id;
    public HashSet<GameObject> boxes;

    public Tier(TierId id, HashSet<GameObject> boxes){
        this.id = id;
        this.boxes = boxes;
    }
    public void RawRotate(float angle){
        foreach (GameObject box in this.boxes){
            switch(this.id.face){
                case Face.xy:
                    box.transform.RotateAround(Vector3.zero, box.transform.parent.forward, angle);
                    break;
                case Face.xz:
                    box.transform.RotateAround(Vector3.zero, box.transform.parent.up, angle);
                    break;
                case Face.yz:
                    box.transform.RotateAround(Vector3.zero, box.transform.parent.right, angle);
                    break;
                default:
                    break;
            }
        }
    }
}

enum CubeTiersRotateRoutinePhase { startDetecting, controlRotating, autoRotating, sleeping };
struct CubeTiersRotateRoutine
{
    public float gearRatio;
    private CubeTiersRotateRoutinePhase _phase;
    public CubeTiersRotateRoutinePhase phase{
        get { return _phase; }
    }
    private Tier tier;
    private float progress; //-1 ~ 0 ~ +1 , considering the case where tier boxes rotate clockwise(or counterclockwise) along an axis at first but then rotate counterclockwise(or clockwise) and swing, we use this scheme, both -1 and +1 means progress done.
    private Vector2 accumulateDeltaPosition;
    private Vector3 accumulateDeltaInCube;
    private float startDetectThredhold;
    private RaycastHit hit;
    private Direction controlDirection;
    public CubeTiersRotateRoutine(float gearRatio)
    {
        this.gearRatio = gearRatio;
        this._phase = CubeTiersRotateRoutinePhase.sleeping;
        this.tier = null;
        this.progress = 0;
        this.startDetectThredhold = 5;
        this.hit = new RaycastHit();
        this.controlDirection = Direction.empty;
        this.accumulateDeltaPosition = Vector2.zero;
        this.accumulateDeltaInCube = Vector3.zero;
    }
    public void HandleTouch(Touch[] touches, Transform transform){
        this.HandleTouch(touches, transform, new RaycastHit());
    }
    public void HandleTouch(Touch[] touches, Transform transform, RaycastHit hit){
        Touch firstFinger = touches[0];
        this.hit = hit;
        this.accumulateDeltaPosition += firstFinger.deltaPosition;
        this.accumulateDeltaInCube += transform.InverseTransformPoint(new Vector3(firstFinger.deltaPosition.x, firstFinger.deltaPosition.y, 0));
        if (firstFinger.phase == TouchPhase.Began){
            this._phase = CubeTiersRotateRoutinePhase.startDetecting;
            if(StartDetect()){
                DetermineTierAndControlDirection(firstFinger, transform);
                TestAccumulateEnoughResult result = TestAccumulateEnough(firstFinger, transform);
                if (!result.enough){
                    this.Rotate(firstFinger, transform);
                } else{
                    this.Rotate(firstFinger, transform, result.deficient);
                    this.reviseCubeCoordOfBoxAndSquare(transform);
                    clearAccumulateDelta();
                    this._phase = CubeTiersRotateRoutinePhase.sleeping;
                }
            }
        } else if(firstFinger.phase == TouchPhase.Moved){
            if(this.phase == CubeTiersRotateRoutinePhase.startDetecting){
                if (StartDetect()){
                    DetermineTierAndControlDirection(firstFinger, transform);
                    TestAccumulateEnoughResult result = TestAccumulateEnough(firstFinger, transform);
                    if (!result.enough){
                        this.Rotate(firstFinger, transform);
                    } else{
                        this.Rotate(firstFinger, transform, result.deficient);
                        this.reviseCubeCoordOfBoxAndSquare(transform);
                        clearAccumulateDelta();
                        this._phase = CubeTiersRotateRoutinePhase.sleeping;
                    }
                }
            } else if(this.phase == CubeTiersRotateRoutinePhase.controlRotating){
                TestAccumulateEnoughResult result = TestAccumulateEnough(firstFinger, transform);
                if (!result.enough){
                    this.Rotate(firstFinger, transform);
                } else{
                    this.Rotate(firstFinger, transform, result.deficient);
                    this.reviseCubeCoordOfBoxAndSquare(transform);
                    clearAccumulateDelta();
                    this._phase = CubeTiersRotateRoutinePhase.sleeping;
                }
            } else{
                Console.Error.WriteLine("This code branch should not be executing");
            }
        } else if(firstFinger.phase == TouchPhase.Ended || firstFinger.phase == TouchPhase.Canceled){
            TestAccumulateEnoughResult result = TestAccumulateEnough(firstFinger, transform);
            if (!result.enough){
                this.Rotate(firstFinger, transform);
                this._phase = CubeTiersRotateRoutinePhase.autoRotating;
                //todo continue here, autoRotate
                this._phase = CubeTiersRotateRoutinePhase.sleeping;
            } else{
                this.Rotate(firstFinger, transform, result.deficient);
                this.reviseCubeCoordOfBoxAndSquare(transform);
                clearAccumulateDelta();
                this._phase = CubeTiersRotateRoutinePhase.sleeping;
            }
        }
    }
    private void AutoRotate(){
        //todo to complete this method
    }
    private void reviseCubeCoordOfBoxAndSquare(Transform transform){
        //A better way to handle maintaining is using Matrix4x4 api. I am not that familiar with matrix so I can't master Matrix4x4 api now. I will refactor this method when I do.
        // int[] rotatePX = new int[] { 1, 0, 0, 0, 0, -1, 0, 1, 0 };
        // int[] rotatePY = new int[] { 0, 0, 1, 0, 1, 0, -1, 0, 0 };
        // int[] rotatePZ = new int[] { 0, -1, 0, 1, 0, 0, 0, 0, 1 };
        // int[] rotateNX = new int[] { 1, 0, 0, 0, 0, 1, 0, -1, 0 };
        // int[] rotateNY = new int[] { 0, 0, -1, 0, 1, 0, 1, 0, 0 };
        // int[] rotateNZ = new int[] { 0, 1, 0, -1, 0, 0, 0, 0, 1 };
        // int[] rotateMatrix;
        // if (this.phase == CubeTiersRotateRoutinePhase.controlRotating || this.phase == CubeTiersRotateRoutinePhase.startDetecting){
        //     if(   (this.controlDirection == Direction.positiveX && this.accumulateDeltaInCube.x > 0)
        //        || (this.controlDirection == Direction.negativeX && this.accumulateDeltaInCube.x < 0)
        //       )
        //     {
        //         rotateMatrix = rotatePX;
        //     } 
        //     else if(   (this.controlDirection == Direction.positiveX && this.accumulateDeltaInCube.x < 0)
        //             || (this.controlDirection == Direction.negativeX && this.accumulateDeltaInCube.x > 0)
        //            )
        //     {
        //         rotateMatrix = rotateNX;
        //     }
        //     else if(   (this.controlDirection == Direction.positiveY && this.accumulateDeltaInCube.y > 0)
        //             || (this.controlDirection == Direction.negativeY && this.accumulateDeltaInCube.y < 0)
        //            )
        //     {
        //         rotateMatrix = rotatePY;
        //     }
        //     else if(   (this.controlDirection == Direction.positiveY && this.accumulateDeltaInCube.y < 0)
        //             || (this.controlDirection == Direction.negativeY && this.accumulateDeltaInCube.y > 0)
        //            )
        //     {
        //         rotateMatrix = rotateNY;
        //     }
        //     else if(   (this.controlDirection == Direction.positiveZ && this.accumulateDeltaInCube.z > 0)
        //             || (this.controlDirection == Direction.negativeZ && this.accumulateDeltaInCube.z < 0)
        //            )
        //     {
        //         rotateMatrix = rotatePZ;
        //     }
        //     else if(   (this.controlDirection == Direction.positiveZ && this.accumulateDeltaInCube.z < 0)
        //             || (this.controlDirection == Direction.negativeZ && this.accumulateDeltaInCube.z > 0)
        //            )
        //     {
        //         rotateMatrix = rotateNY;
        //     }
        // }
        Quaternion rotation = Quaternion.identity;
        if (this.phase == CubeTiersRotateRoutinePhase.controlRotating || this.phase == CubeTiersRotateRoutinePhase.startDetecting){
            if(   (this.controlDirection == Direction.positiveX && this.accumulateDeltaInCube.x > 0)
               || (this.controlDirection == Direction.negativeX && this.accumulateDeltaInCube.x < 0)
              )
            {
                rotation = Quaternion.Euler(90, 0, 0);
            } 
            else if(   (this.controlDirection == Direction.positiveX && this.accumulateDeltaInCube.x < 0)
                    || (this.controlDirection == Direction.negativeX && this.accumulateDeltaInCube.x > 0)
                   )
            {
                rotation = Quaternion.Euler(-90, 0, 0);
            }
            else if(   (this.controlDirection == Direction.positiveY && this.accumulateDeltaInCube.y > 0)
                    || (this.controlDirection == Direction.negativeY && this.accumulateDeltaInCube.y < 0)
                   )
            {
                rotation = Quaternion.Euler(0, 90, 0);
            }
            else if(   (this.controlDirection == Direction.positiveY && this.accumulateDeltaInCube.y < 0)
                    || (this.controlDirection == Direction.negativeY && this.accumulateDeltaInCube.y > 0)
                   )
            {
                rotation = Quaternion.Euler(0, -90, 0);
            }
            else if(   (this.controlDirection == Direction.positiveZ && this.accumulateDeltaInCube.z > 0)
                    || (this.controlDirection == Direction.negativeZ && this.accumulateDeltaInCube.z < 0)
                   )
            {
                rotation = Quaternion.Euler(0, 0, 90);
            }
            else if(   (this.controlDirection == Direction.positiveZ && this.accumulateDeltaInCube.z < 0)
                    || (this.controlDirection == Direction.negativeZ && this.accumulateDeltaInCube.z > 0)
                   )
            {
                rotation = Quaternion.Euler(0, 0, -90);
            }
        }
        Cube cube = transform.gameObject.GetComponent<Cube>();
        int rank = cube.rank;
        Vector3 toCenter = new Vector3((rank - 1) / 2, (rank - 1) / 2, (rank - 1) / 2);
        foreach(GameObject box in this.tier.boxes){
            Box boxComp = box.GetComponent<Box>();

            //remove box from original tier
            cube.tiers[new TierId(Face.xy, boxComp.cubeCoord.z)].boxes.Remove(box);
            cube.tiers[new TierId(Face.xz, boxComp.cubeCoord.y)].boxes.Remove(box);
            cube.tiers[new TierId(Face.yz, boxComp.cubeCoord.x)].boxes.Remove(box);

            //trans cubeCoord
            Vector3 coordTmp = boxComp.cubeCoord;
            coordTmp -= toCenter;
            coordTmp = rotation * coordTmp;
            coordTmp += toCenter;
            boxComp.cubeCoord = new Vector3Int(Convert.ToInt32(coordTmp.x), Convert.ToInt32(coordTmp.y), Convert.ToInt32(coordTmp.z));

            //add box to proper tier
            cube.tiers[new TierId(Face.xy, boxComp.cubeCoord.z)].boxes.Add(box);
            cube.tiers[new TierId(Face.xz, boxComp.cubeCoord.y)].boxes.Add(box);
            cube.tiers[new TierId(Face.yz, boxComp.cubeCoord.x)].boxes.Add(box);

            foreach (Square square in box.GetComponentsInChildren<Square>()){
                Vector3 direction = Vector3.zero;
                switch(square.direction){
                    case Direction.positiveX:
                        direction = Vector3.right;
                        break;
                    case Direction.negativeX:
                        direction = Vector3.left;
                        break;
                    case Direction.positiveY:
                        direction = Vector3.up;
                        break;
                    case Direction.negativeY:
                        direction = Vector3.down;
                        break;
                    case Direction.positiveZ:
                        direction = Vector3.forward;
                        break;
                    case Direction.negativeZ:
                        direction = Vector3.back;
                        break;
                }
                direction = rotation * direction;
                if(direction.x > 0.5){
                    square.direction = Direction.positiveX;
                } else if(direction.x < -0.5){
                    square.direction = Direction.negativeX;
                } else if(direction.y > 0.5){
                    square.direction = Direction.positiveY;
                } else if(direction.y < -0.5){
                    square.direction = Direction.negativeY;
                } else if(direction.z > 0.5){
                    square.direction = Direction.positiveZ;
                } else if(direction.z < -0.5){
                    square.direction = Direction.negativeZ;
                }
            }
        }
    }
    private void clearAccumulateDelta(){
        this.accumulateDeltaPosition = Vector2.zero;
        this.accumulateDeltaInCube = Vector3.zero;
    }
    private struct TestAccumulateEnoughResult {
        internal Boolean enough;
        internal Vector3 deficient;
        public TestAccumulateEnoughResult(Boolean enough, Vector3 deficient){
            this.enough = enough;
            this.deficient = deficient;
        }
    };
    private TestAccumulateEnoughResult TestAccumulateEnough(Touch firstFinger, Transform transform){
        // if enough then return Vector3.zero, else return deficient Vector3 from last touch
        Vector3 deltaInCube = transform.InverseTransformPoint(new Vector3(firstFinger.deltaPosition.x, firstFinger.deltaPosition.y, 0));
        Vector3 lastDeficientInPositiveAxes = deltaInCube - (accumulateDeltaInCube - new Vector3(90 * gearRatio, 90 * gearRatio, 90 * gearRatio));
        Vector3 lastDeficientInNegativeAxes = deltaInCube - (accumulateDeltaInCube - new Vector3(-90 * gearRatio, -90 * gearRatio, -90 * gearRatio));
        TestAccumulateEnoughResult result = new TestAccumulateEnoughResult();
        switch (controlDirection){
            case Direction.positiveX:
                if( accumulateDeltaInCube.x / gearRatio >= 90 ){
                    result = new TestAccumulateEnoughResult(true, lastDeficientInPositiveAxes);
                }
                result = new TestAccumulateEnoughResult(false, Vector3.zero);
                break;
            case Direction.negativeX:
                if( accumulateDeltaInCube.x / gearRatio <= -90 ){
                    result = new TestAccumulateEnoughResult(true, lastDeficientInNegativeAxes);
                }
                result = new TestAccumulateEnoughResult(false, Vector3.zero);
                break;
            case Direction.positiveY:
                if( accumulateDeltaInCube.y / gearRatio >= 90 ){
                    result = new TestAccumulateEnoughResult(true, lastDeficientInPositiveAxes);
                }
                result = new TestAccumulateEnoughResult(false, Vector3.zero);
                break;
            case Direction.negativeY:
                if( accumulateDeltaInCube.y / gearRatio <= -90 ){
                    result = new TestAccumulateEnoughResult(true, lastDeficientInNegativeAxes);
                }
                result = new TestAccumulateEnoughResult(false, Vector3.zero);
                break;
            case Direction.positiveZ:
                if( accumulateDeltaInCube.z / gearRatio >= 90 ){
                    result = new TestAccumulateEnoughResult(true, lastDeficientInPositiveAxes);
                }
                result = new TestAccumulateEnoughResult(false, Vector3.zero);
                break;
            case Direction.negativeZ:
                if( accumulateDeltaInCube.z / gearRatio <= -90 ){
                    result = new TestAccumulateEnoughResult(true, lastDeficientInNegativeAxes);
                }
                result = new TestAccumulateEnoughResult(false, Vector3.zero);
                break;
            default:
                result = new TestAccumulateEnoughResult(false, Vector3.zero);
                break;
        }
        return result;
    }
    private Boolean StartDetect(){
        if ( Math.Abs(this.accumulateDeltaPosition.x) > this.startDetectThredhold || Math.Abs(this.accumulateDeltaPosition.y) > this.startDetectThredhold){
            this._phase = CubeTiersRotateRoutinePhase.controlRotating;
            return true;
        }
        return false;
    }
    private Boolean DetermineTierAndControlDirection(Touch firstFinger, Transform transform){
        TierId id = new TierId();
        Cube cubeComponent = transform.gameObject.GetComponent<Cube>();
        Square square = this.hit.collider.GetComponent<Square>();
        switch(square.direction){
            case Direction.positiveX:
                if(Math.Abs(accumulateDeltaInCube.y) > Math.Abs(accumulateDeltaInCube.z)){
                    id = new TierId(Face.xy, square.transform.parent.GetComponent<Box>().cubeCoord.z);
                    this.tier = cubeComponent.tiers[id];
                    this.controlDirection = Direction.positiveY;
                } else{
                    id = new TierId(Face.xz, square.transform.parent.GetComponent<Box>().cubeCoord.y);
                    this.tier = cubeComponent.tiers[id];
                    this.controlDirection = Direction.negativeZ;
                }
                return true;
            case Direction.negativeX:
                if (Math.Abs(accumulateDeltaInCube.y) > Math.Abs(accumulateDeltaInCube.z)){
                    id = new TierId(Face.xy, square.transform.parent.GetComponent<Box>().cubeCoord.z);
                    this.tier = cubeComponent.tiers[id];
                    this.controlDirection = Direction.negativeY;
                } else{
                    id = new TierId(Face.xz, square.transform.parent.GetComponent<Box>().cubeCoord.y);
                    this.tier = cubeComponent.tiers[id];
                    this.controlDirection = Direction.positiveZ;
                }
                return true;
            case Direction.positiveY:
                if (Math.Abs(accumulateDeltaInCube.x) > Math.Abs(accumulateDeltaInCube.z)){
                    id = new TierId(Face.xy, square.transform.parent.GetComponent<Box>().cubeCoord.z);
                    this.tier = cubeComponent.tiers[id];
                    this.controlDirection = Direction.negativeX;
                } else {
                    id = new TierId(Face.yz, square.transform.parent.GetComponent<Box>().cubeCoord.x);
                    this.tier = cubeComponent.tiers[id];
                    this.controlDirection = Direction.positiveZ;
                }
                return true;
            case Direction.negativeY:
                if (Math.Abs(accumulateDeltaInCube.x) > Math.Abs(accumulateDeltaInCube.z)){
                    id = new TierId(Face.xy, square.transform.parent.GetComponent<Box>().cubeCoord.z);
                    this.tier = cubeComponent.tiers[id];
                    this.controlDirection = Direction.positiveX;
                } else{
                    id = new TierId(Face.yz, square.transform.parent.GetComponent<Box>().cubeCoord.x);
                    this.tier = cubeComponent.tiers[id];
                    this.controlDirection = Direction.negativeZ;
                }
                return true;
            case Direction.positiveZ:
                if (Math.Abs(accumulateDeltaInCube.x) > Math.Abs(accumulateDeltaInCube.y)){
                    id = new TierId(Face.xz, square.transform.parent.GetComponent<Box>().cubeCoord.y);
                    this.tier = cubeComponent.tiers[id];
                    this.controlDirection = Direction.positiveX;
                } else {
                    id = new TierId(Face.yz, square.transform.parent.GetComponent<Box>().cubeCoord.x);
                    this.tier = cubeComponent.tiers[id];
                    this.controlDirection = Direction.negativeY;
                }
                return true;
            case Direction.negativeZ:
                if (Math.Abs(accumulateDeltaInCube.x) > Math.Abs(accumulateDeltaInCube.y))
                {
                    id = new TierId(Face.xz, square.transform.parent.GetComponent<Box>().cubeCoord.y);
                    this.tier = cubeComponent.tiers[id];
                    this.controlDirection = Direction.negativeX;
                }
                else
                {
                    id = new TierId(Face.yz, square.transform.parent.GetComponent<Box>().cubeCoord.x);
                    this.tier = cubeComponent.tiers[id];
                    this.controlDirection = Direction.positiveY;
                }
                return true;
            default:
                this.tier = null;
                this.controlDirection = Direction.empty;
                return false;
        }
    }
    private void Rotate(Touch firstFinger, Transform transform)
    {
        Rotate(firstFinger, transform, Vector3.zero);
    }
    private void Rotate(Touch firstFinger, Transform transform, Vector3 deltaInCubeArg){
        Vector3 deltaInCube = deltaInCubeArg;
        if (deltaInCube == Vector3.zero){
            deltaInCube = transform.InverseTransformPoint(firstFinger.deltaPosition.x, firstFinger.deltaPosition.y, 0);
        }
        Vector3 rotateAngle = new Vector3();
        if(tier.id.face == Face.xy){
            switch(controlDirection){
                case Direction.positiveX:
                    rotateAngle = new Vector3(0, 0, deltaInCube.x);
                    break;
                case Direction.negativeX:
                    rotateAngle = new Vector3(0, 0, -deltaInCube.x);
                    break;
                case Direction.positiveY:
                    rotateAngle = new Vector3(0, 0, deltaInCube.y);
                    break;
                case Direction.negativeY:
                    rotateAngle = new Vector3(0, 0, -deltaInCube.y);
                    break;
            }
        } else if(tier.id.face == Face.xz){
            switch(controlDirection){
                case Direction.positiveX:
                    rotateAngle = new Vector3(0, deltaInCube.x, 0);
                    break;
                case Direction.negativeX:
                    rotateAngle = new Vector3(0, -deltaInCube.x, 0);
                    break;
                case Direction.positiveZ:
                    rotateAngle = new Vector3(0, deltaInCube.z, 0);
                    break;
                case Direction.negativeZ:
                    rotateAngle = new Vector3(0, -deltaInCube.z, 0);
                    break;
            }
        } else if(tier.id.face == Face.yz){
            switch(controlDirection){
                case Direction.positiveY:
                    rotateAngle = new Vector3(deltaInCube.y, 0, 0);
                    break;
                case Direction.negativeY:
                    rotateAngle = new Vector3(-deltaInCube.y, 0, 0);
                    break;
                case Direction.positiveZ:
                    rotateAngle = new Vector3(deltaInCube.z, 0, 0);
                    break;
                case Direction.negativeZ:
                    rotateAngle = new Vector3(-deltaInCube.z, 0, 0);
                    break;
            }
        }
        foreach(GameObject box in tier.boxes){
            box.transform.Rotate(rotateAngle, Space.World);
        }
    }
}
enum CubeRotateControl { delta_x_rotate_y, delta_x_rotate_z, empty };
enum CubeRotateRoutinePhase { active, sleeping };
struct CubeRotateRoutine{
    private CubeRotateRoutinePhase _phase;
    public CubeRotateRoutinePhase phase{
        get { return _phase; }
    }
    public CubeRotateControl rotateControl;
    public float gearRatio;
    public CubeRotateRoutine(float gearRatio){
        this.gearRatio = gearRatio;
        this._phase = CubeRotateRoutinePhase.sleeping;
        this.rotateControl = CubeRotateControl.empty;
    }
    public void Rotate(Touch firstFinger, Transform transform){
        if (this.rotateControl == CubeRotateControl.delta_x_rotate_y){
            transform.Rotate(firstFinger.deltaPosition.y / this.gearRatio, -firstFinger.deltaPosition.x / this.gearRatio, 0, Space.World);
        } else if (this.rotateControl == CubeRotateControl.delta_x_rotate_z){
            transform.Rotate(firstFinger.deltaPosition.y / this.gearRatio, 0, -firstFinger.deltaPosition.x / this.gearRatio, Space.World);
        }
    }
    public void HandleTouch(Touch[] touches, Transform transform){
        Touch firstFinger = touches[0];
        if (firstFinger.phase == TouchPhase.Began){
            this._phase = CubeRotateRoutinePhase.active;
            if(firstFinger.position.y < Screen.height/2){
                this.rotateControl = CubeRotateControl.delta_x_rotate_y;
            } else{
                this.rotateControl = CubeRotateControl.delta_x_rotate_z;
            }
            this.Rotate(firstFinger, transform);
        } else if (firstFinger.phase == TouchPhase.Moved){
            this.Rotate(firstFinger, transform);
        } else if(firstFinger.phase == TouchPhase.Ended){
            if(this.phase == CubeRotateRoutinePhase.active){
                this._phase = CubeRotateRoutinePhase.sleeping;
            }
            this.Rotate(firstFinger, transform);
        }
    }
}
public class Cube : MonoBehaviour {
    public int rank = 3;
    public Dictionary<TierId, Tier> tiers = new Dictionary<TierId, Tier>();
    CubeTiersRotateRoutine tiersRR = new CubeTiersRotateRoutine(7);
    CubeRotateRoutine cubeRR = new CubeRotateRoutine(7);

    void InitProperty() {
        this.name = "cube";
        for (int i = 0; i < this.rank; i++){
            TierId idz = new TierId(Face.xy, i);
            TierId idy = new TierId(Face.xz, i);
            TierId idx = new TierId(Face.yz, i);
            this.tiers.Add(idz, new Tier(idz, new HashSet<GameObject>()));
            this.tiers.Add(idy, new Tier(idy, new HashSet<GameObject>()));
            this.tiers.Add(idx, new Tier(idx, new HashSet<GameObject>()));
        }
    }

    void ConstructCube() {
        GameObject protoBox = GameObject.Find("box");
        float frontDistance = rank * 2 / 2 - 2 / 2;
        for (int x = 0; x < rank; x++)
        {
            for (int y = 0; y < rank; y++)
            {
                for (int z = 0; z < rank; z++)
                {
                    GameObject box = Instantiate(protoBox, new Vector3(x * 2 - frontDistance, y * 2 - frontDistance, z * 2 - frontDistance), Quaternion.identity, gameObject.transform);
                    box.GetComponent<Box>().cubeCoord = new Vector3Int(x, y, z);
                    foreach(Square square in box.GetComponentsInChildren<Square>()){
                        if(   (x == 0 && square.direction == Direction.negativeX)
                           || (x == rank-1 && square.direction == Direction.positiveX)
                           || (y == 0 && square.direction == Direction.negativeY)
                           || (y == rank - 1 && square.direction == Direction.positiveY)
                           || (z == 0 && square.direction == Direction.negativeZ)
                           || (z == rank - 1 && square.direction == Direction.positiveZ)
                          )
                        {

                        } else{
                            Destroy(square);
                        }
                    }
                    TierId idz = new TierId(Face.xy, z);
                    TierId idy = new TierId(Face.xz, y);
                    TierId idx = new TierId(Face.yz, x);
                    this.tiers[idz].boxes.Add(box);
                    this.tiers[idy].boxes.Add(box);
                    this.tiers[idx].boxes.Add(box);
                }
            }
        }
        Destroy(protoBox);
    }
    void InitView()
    {
        transform.Rotate(-18, 20, 0, Space.World);
    }
    // Use this for initialization
    void Start () {
        InitProperty();
        ConstructCube();
        InitView();
	}
	
	// Update is called once per frame
	void Update () {
        if(Input.touchCount > 0){
            if (this.cubeRR.phase == CubeRotateRoutinePhase.active){
                this.cubeRR.HandleTouch(Input.touches, transform);
            } else if(this.tiersRR.phase == CubeTiersRotateRoutinePhase.startDetecting || this.tiersRR.phase == CubeTiersRotateRoutinePhase.controlRotating){
                this.tiersRR.HandleTouch(Input.touches, transform);
            } else if(this.cubeRR.phase == CubeRotateRoutinePhase.sleeping && this.tiersRR.phase == CubeTiersRotateRoutinePhase.sleeping){
                Ray raycast = Camera.main.ScreenPointToRay(Input.GetTouch(0).position);
                RaycastHit hit;
                if (Physics.Raycast(raycast, out hit) && hit.collider.tag == "square")
                {
                    this.tiersRR.HandleTouch(Input.touches, transform, hit);
                } else{
                    this.cubeRR.HandleTouch(Input.touches, transform);
                }
            }
        }

        /////debug start
        if(Input.touchCount > 0){
            Console.WriteLine("screen wh: " + Screen.width + " " + Screen.height);
            Console.Write("=== frame touch count: " + Input.touchCount);
            foreach (Touch touch in Input.touches)
            {
                Console.Write(" phase " + touch.phase);
                Console.Write(" position " + touch.position);
                Console.Write(" deltaPosition " + touch.deltaPosition);
                Console.Write(" deltaTime " + touch.deltaTime);
                Console.Write(" deltaPosition/deltaTime " + touch.deltaPosition / touch.deltaTime);
                Console.WriteLine("");
            }
        }
        /////debug end
	}
}
