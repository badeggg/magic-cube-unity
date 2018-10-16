using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

class CubicBezierCurve{
    Vector2 controlPoint1;
    Vector2 controlPoint2;
    public CubicBezierCurve(){
        this.controlPoint1 = new Vector2(0.4F, 0);
        this.controlPoint2 = new Vector2(0.6F, 1);
    }
    public CubicBezierCurve(Vector2 p1, Vector2 p2){
        this.controlPoint1 = p1;
        this.controlPoint2 = p2;
    }
    public CubicBezierCurve(float p1x, float p1y, float p2x, float p2y){
        this.controlPoint1 = new Vector2(p1x, p1y);
        this.controlPoint2 = new Vector2(p2x, p2y);
    }
    public float DeriveYFromT(float tTarget){
        return DeriveYFromX(tTarget);
    }
    public float DeriveYFromX(float xTarget){
        float tolerance = 0.00001F;
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
public enum Sign { plus, minus, empty };
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

enum CubeTiersRotateRoutinePhase { startDetecting, controlRotating, autoRotating, sequenceAutoRotating, sequenceAutoRotateGap, sleeping };
public struct CubeTiersRotateRoutine
{
    public float gearRatio;
    private CubeTiersRotateRoutinePhase _phase;
    internal CubeTiersRotateRoutinePhase phase
    {
        get { return _phase; }
    }
    public Tier tier;
    private Vector2 accumulateDeltaPosition;
    private Vector3 accumulateDeltaInCube;
    private float startDetectThreshold;
    private RaycastHit hit;
    private Direction controlDirection;
    private Touch placeholderTouch;
    public CubeTiersRotateRoutine(float gearRatio)
    {
        this.gearRatio = gearRatio;
        this._phase = CubeTiersRotateRoutinePhase.sleeping;
        this.tier = null;
        this.startDetectThreshold = 2.5F;
        this.hit = new RaycastHit();
        this.controlDirection = Direction.empty;
        this.accumulateDeltaPosition = Vector2.zero;
        this.accumulateDeltaInCube = Vector3.zero;
        this.autoRotateProperty = new AutoRotateProperty();
        this.placeholderTouch = new Touch();
        this.sequenceAutoRotateProperty = new SequenceAutoRotateProperty();
    }
    public void HandleTouch(Touch[] touches, Transform transform, RaycastHit hit)
    {
        this.hit = hit;
        this.HandleTouch(touches, transform);
    }
    public void HandleTouch(Touch[] touches, Transform transform)
    {
        Touch firstFinger = touches[0];
        this.accumulateDeltaPosition += firstFinger.deltaPosition;
        this.accumulateDeltaInCube += transform.InverseTransformPoint(new Vector3(firstFinger.deltaPosition.x, firstFinger.deltaPosition.y, 0));
        if (firstFinger.phase == TouchPhase.Began)
        {
            this._phase = CubeTiersRotateRoutinePhase.startDetecting;
            if (StartDetect())
            {
                this._phase = CubeTiersRotateRoutinePhase.controlRotating;
                DetermineTierAndControlDirection(firstFinger, transform);
                TestAccumulateEnoughResult result = TestAccumulateEnough(firstFinger, transform);
                if (!result.enough)
                {
                    this.Rotate(firstFinger, transform);
                }
                else
                {
                    this.Rotate(transform, result.deficient);
                    this.reviseCubeCoordOfBoxAndSquare(transform);
                    clearAccumulateDelta();
                    this._phase = CubeTiersRotateRoutinePhase.sleeping;
                }
            }
        }
        else if (firstFinger.phase == TouchPhase.Moved)
        {
            if (this.phase == CubeTiersRotateRoutinePhase.startDetecting)
            {
                if (StartDetect())
                {
                    this._phase = CubeTiersRotateRoutinePhase.controlRotating;
                    DetermineTierAndControlDirection(firstFinger, transform);
                    TestAccumulateEnoughResult result = TestAccumulateEnough(firstFinger, transform);
                    if (!result.enough)
                    {
                        this.Rotate(firstFinger, transform);
                    }
                    else
                    {
                        this.Rotate(transform, result.deficient);
                        this.reviseCubeCoordOfBoxAndSquare(transform);
                        clearAccumulateDelta();
                        this._phase = CubeTiersRotateRoutinePhase.sleeping;
                    }
                }
            }
            else if (this.phase == CubeTiersRotateRoutinePhase.controlRotating)
            {
                TestAccumulateEnoughResult result = TestAccumulateEnough(firstFinger, transform);
                if (!result.enough)
                {
                    this.Rotate(firstFinger, transform);
                }
                else
                {
                    this.Rotate(transform, result.deficient);
                    this.reviseCubeCoordOfBoxAndSquare(transform);
                    clearAccumulateDelta();
                    this._phase = CubeTiersRotateRoutinePhase.sleeping;
                }
            }
            else
            {
                Console.Error.WriteLine("This code branch should not be executing");
            }
        }
        else if (firstFinger.phase == TouchPhase.Ended || firstFinger.phase == TouchPhase.Canceled)
        {
            TestAccumulateEnoughResult result = TestAccumulateEnough(firstFinger, transform);
            if (!result.enough)
            {
                this.Rotate(firstFinger, transform);
                this.InitAutoRotateProperty(transform, firstFinger);
                this.AutoRotate(transform);
            }
            else
            {
                this.Rotate(transform, result.deficient);
                this.reviseCubeCoordOfBoxAndSquare(transform);
                clearAccumulateDelta();
                this._phase = CubeTiersRotateRoutinePhase.sleeping;
            }
        }
    }
    private class AutoRotateProperty
    {
        public CubicBezierCurve bezier = new CubicBezierCurve();
        public float duration = 0;
        public float accumulateTime = 0;
        public float speedThreshold = 200;
        public float initAngle = 0;
        public float currentAngle = 0;
        public float targetAngle = 0;
        public float anglePerSec = 360F;
        public float deltaAngle = 0;
    }
    AutoRotateProperty autoRotateProperty;
    internal void InitAutoRotateProperty(Transform transform, Touch firstFinger)
    {
        _phase = CubeTiersRotateRoutinePhase.autoRotating;
        autoRotateProperty.accumulateTime = 0;
        Vector3 deltaInCube = transform.InverseTransformPoint(new Vector3(firstFinger.deltaPosition.x, firstFinger.deltaPosition.y, 0));
        Vector3 speedInCube = deltaInCube / firstFinger.deltaTime;
        if (this.controlDirection == Direction.positiveX)
        {
            autoRotateProperty.initAngle = accumulateDeltaInCube.x / gearRatio;
            if (Math.Abs(speedInCube.x) > autoRotateProperty.speedThreshold)
            {
                if (speedInCube.x > 0)
                {
                    if (autoRotateProperty.initAngle < 0)
                    {
                        autoRotateProperty.targetAngle = 0;
                    }
                    else
                    {
                        autoRotateProperty.targetAngle = 90;
                    }
                }
                else
                {
                    if (autoRotateProperty.initAngle < 0)
                    {
                        autoRotateProperty.targetAngle = -90;
                    }
                    else
                    {
                        autoRotateProperty.targetAngle = 0;
                    }
                }
                autoRotateProperty.bezier = new CubicBezierCurve(0.4F / Math.Abs(speedInCube.x), 0, 0.6F, 1);
            }
            else
            {
                if (autoRotateProperty.initAngle > 45)
                {
                    autoRotateProperty.targetAngle = 90;
                }
                else if (autoRotateProperty.initAngle > -45 && autoRotateProperty.initAngle < 45)
                {
                    autoRotateProperty.targetAngle = 0;
                }
                else if (autoRotateProperty.initAngle < -45)
                {
                    autoRotateProperty.targetAngle = -90;
                }
                autoRotateProperty.bezier = new CubicBezierCurve(0.4F, 0, 0.6F, 1);
            }
        }
        else if (this.controlDirection == Direction.negativeX)
        {
            autoRotateProperty.initAngle = -accumulateDeltaInCube.x / gearRatio;
            if (Math.Abs(speedInCube.x) > autoRotateProperty.speedThreshold)
            {
                if (speedInCube.x > 0)
                {
                    if (autoRotateProperty.initAngle < 0)
                    {
                        autoRotateProperty.targetAngle = -90;
                    }
                    else
                    {
                        autoRotateProperty.targetAngle = 0;
                    }
                }
                else
                {
                    if (autoRotateProperty.initAngle < 0)
                    {
                        autoRotateProperty.targetAngle = 0;
                    }
                    else
                    {
                        autoRotateProperty.targetAngle = 90;
                    }
                }
                autoRotateProperty.bezier = new CubicBezierCurve(0.4F / Math.Abs(speedInCube.x), 0, 0.6F, 1);
            }
            else
            {
                if (autoRotateProperty.initAngle > 45)
                {
                    autoRotateProperty.targetAngle = 90;
                }
                else if (autoRotateProperty.initAngle > -45 && autoRotateProperty.initAngle < 45)
                {
                    autoRotateProperty.targetAngle = 0;
                }
                else if (autoRotateProperty.initAngle < -45)
                {
                    autoRotateProperty.targetAngle = -90;
                }
                autoRotateProperty.bezier = new CubicBezierCurve(0.4F, 0, 0.6F, 1);
            }
        }
        else if (this.controlDirection == Direction.positiveY)
        {
            autoRotateProperty.initAngle = accumulateDeltaInCube.y / gearRatio;
            if (Math.Abs(speedInCube.y) > autoRotateProperty.speedThreshold)
            {
                if (speedInCube.y > 0)
                {
                    if (autoRotateProperty.initAngle < 0)
                    {
                        autoRotateProperty.targetAngle = 0;
                    }
                    else
                    {
                        autoRotateProperty.targetAngle = 90;
                    }
                }
                else
                {
                    if (autoRotateProperty.initAngle < 0)
                    {
                        autoRotateProperty.targetAngle = -90;
                    }
                    else
                    {
                        autoRotateProperty.targetAngle = 0;
                    }
                }
                autoRotateProperty.bezier = new CubicBezierCurve(0.4F / Math.Abs(speedInCube.y), 0, 0.6F, 1);
            }
            else
            {
                if (autoRotateProperty.initAngle > 45)
                {
                    autoRotateProperty.targetAngle = 90;
                }
                else if (autoRotateProperty.initAngle > -45 && autoRotateProperty.initAngle < 45)
                {
                    autoRotateProperty.targetAngle = 0;
                }
                else if (autoRotateProperty.initAngle < -45)
                {
                    autoRotateProperty.targetAngle = -90;
                }
                autoRotateProperty.bezier = new CubicBezierCurve(0.4F, 0, 0.6F, 1);
            }
        }
        else if (this.controlDirection == Direction.negativeY)
        {
            autoRotateProperty.initAngle = -accumulateDeltaInCube.y / gearRatio;
            if (Math.Abs(speedInCube.y) > autoRotateProperty.speedThreshold)
            {
                if (speedInCube.y > 0)
                {
                    if (autoRotateProperty.initAngle < 0)
                    {
                        autoRotateProperty.targetAngle = -90;
                    }
                    else
                    {
                        autoRotateProperty.targetAngle = 0;
                    }
                }
                else
                {
                    if (autoRotateProperty.initAngle < 0)
                    {
                        autoRotateProperty.targetAngle = 0;
                    }
                    else
                    {
                        autoRotateProperty.targetAngle = 90;
                    }
                }
                autoRotateProperty.bezier = new CubicBezierCurve(0.4F / Math.Abs(speedInCube.y), 0, 0.6F, 1);
            }
            else
            {
                if (autoRotateProperty.initAngle > 45)
                {
                    autoRotateProperty.targetAngle = 90;
                }
                else if (autoRotateProperty.initAngle > -45 && autoRotateProperty.initAngle < 45)
                {
                    autoRotateProperty.targetAngle = 0;
                }
                else if (autoRotateProperty.initAngle < -45)
                {
                    autoRotateProperty.targetAngle = -90;
                }
                autoRotateProperty.bezier = new CubicBezierCurve(0.4F, 0, 0.6F, 1);
            }
        }
        else if (this.controlDirection == Direction.positiveZ)
        {
            autoRotateProperty.initAngle = accumulateDeltaInCube.z / gearRatio;
            if (Math.Abs(speedInCube.z) > autoRotateProperty.speedThreshold)
            {
                if (speedInCube.z > 0)
                {
                    if (autoRotateProperty.initAngle < 0)
                    {
                        autoRotateProperty.targetAngle = 0;
                    }
                    else
                    {
                        autoRotateProperty.targetAngle = 90;
                    }
                }
                else
                {
                    if (autoRotateProperty.initAngle < 0)
                    {
                        autoRotateProperty.targetAngle = -90;
                    }
                    else
                    {
                        autoRotateProperty.targetAngle = 0;
                    }
                }
                autoRotateProperty.bezier = new CubicBezierCurve(0.4F / Math.Abs(speedInCube.z), 0, 0.6F, 1);
            }
            else
            {
                if (autoRotateProperty.initAngle > 45)
                {
                    autoRotateProperty.targetAngle = 90;
                }
                else if (autoRotateProperty.initAngle > -45 && autoRotateProperty.initAngle < 45)
                {
                    autoRotateProperty.targetAngle = 0;
                }
                else if (autoRotateProperty.initAngle < -45)
                {
                    autoRotateProperty.targetAngle = -90;
                }
                autoRotateProperty.bezier = new CubicBezierCurve(0.4F, 0, 0.6F, 1);
            }
        }
        else if (this.controlDirection == Direction.negativeZ)
        {
            autoRotateProperty.initAngle = -accumulateDeltaInCube.z / gearRatio;
            if (Math.Abs(speedInCube.z) > autoRotateProperty.speedThreshold)
            {
                if (speedInCube.z > 0)
                {
                    if (autoRotateProperty.initAngle < 0)
                    {
                        autoRotateProperty.targetAngle = -90;
                    }
                    else
                    {
                        autoRotateProperty.targetAngle = 0;
                    }
                }
                else
                {
                    if (autoRotateProperty.initAngle < 0)
                    {
                        autoRotateProperty.targetAngle = 0;
                    }
                    else
                    {
                        autoRotateProperty.targetAngle = 90;
                    }
                }
                autoRotateProperty.bezier = new CubicBezierCurve(0.4F / Math.Abs(speedInCube.z), 0, 0.6F, 1);
            }
            else
            {
                if (autoRotateProperty.initAngle > 45)
                {
                    autoRotateProperty.targetAngle = 90;
                }
                else if (autoRotateProperty.initAngle > -45 && autoRotateProperty.initAngle < 45)
                {
                    autoRotateProperty.targetAngle = 0;
                }
                else if (autoRotateProperty.initAngle < -45)
                {
                    autoRotateProperty.targetAngle = -90;
                }
                autoRotateProperty.bezier = new CubicBezierCurve(0.4F, 0, 0.6F, 1);
            }
        }
        autoRotateProperty.currentAngle = autoRotateProperty.initAngle;
        autoRotateProperty.deltaAngle = autoRotateProperty.targetAngle - autoRotateProperty.initAngle;
        autoRotateProperty.duration = Math.Abs(autoRotateProperty.targetAngle - autoRotateProperty.initAngle) / autoRotateProperty.anglePerSec;
    }
    public void AutoRotate(Transform transform){
        AutoRotate(transform, true);
    }
    public void AutoRotate(Transform transform, Boolean shouldRecord){
        float nextAngle = 0;
        float frameDeltaAngle = 0;
        autoRotateProperty.accumulateTime += Time.deltaTime;
        if (autoRotateProperty.accumulateTime >= autoRotateProperty.duration)
        { //end
            nextAngle = autoRotateProperty.targetAngle;
            frameDeltaAngle = nextAngle - autoRotateProperty.currentAngle;
            Rotate(transform, frameDeltaAngle);
            autoRotateProperty.accumulateTime = 0;
            if (this.phase == CubeTiersRotateRoutinePhase.autoRotating){
                this.reviseCubeCoordOfBoxAndSquare(transform);
                clearAccumulateDelta();
                _phase = CubeTiersRotateRoutinePhase.sleeping;
            } else if(this.phase == CubeTiersRotateRoutinePhase.sequenceAutoRotating){
                SequenceAutoRotateItem currentItem = sequenceAutoRotateProperty.sequenceAutoRotateItems[sequenceAutoRotateProperty.currentItemNum];
                sequenceAutoRotateProperty.currentItemNum++;
                Face face = currentItem.id.face;
                Sign sign = currentItem.sign;
                Quaternion rotation = Quaternion.identity;
                switch (face){
                    case Face.xy:
                        if(sign == Sign.plus){
                            rotation = Quaternion.Euler(0, 0, 90);
                        } else if(sign == Sign.minus){
                            rotation = Quaternion.Euler(0, 0, -90);
                        }
                        break;
                    case Face.xz:
                        if(sign == Sign.plus){
                            rotation = Quaternion.Euler(0, 90, 0);
                        } else if(sign == Sign.minus){
                            rotation = Quaternion.Euler(0, -90, 0);
                        }
                        break;
                    case Face.yz:
                        if(sign == Sign.plus){
                            rotation = Quaternion.Euler(90, 0, 0);
                        } else if(sign == Sign.minus){
                            rotation = Quaternion.Euler(-90, 0, 0);
                        }
                        break;
                }
                this.reviseCubeCoordOfBoxAndSquare(transform, rotation, shouldRecord);
                _phase = CubeTiersRotateRoutinePhase.sequenceAutoRotateGap;
            }
        }
        else
        { //rotating
            nextAngle = autoRotateProperty.initAngle + autoRotateProperty.bezier.DeriveYFromT(autoRotateProperty.accumulateTime / autoRotateProperty.duration) * autoRotateProperty.deltaAngle;
            frameDeltaAngle = nextAngle - autoRotateProperty.currentAngle;
            autoRotateProperty.currentAngle = nextAngle;
            Rotate(transform, frameDeltaAngle);
        }
    }
    public class SequenceAutoRotateItem : ICloneable{
        public TierId id;
        public Sign sign;
        public SequenceAutoRotateItem(TierId id, Sign sign){
            this.id = id;
            this.sign = sign;
        }
        public object Clone(){
            return this.MemberwiseClone();
        }
    }
    private class SequenceAutoRotateProperty{
        public SequenceAutoRotateItem[] sequenceAutoRotateItems;
        public int currentItemNum;
        public Boolean shouldRecord;
    }
    SequenceAutoRotateProperty sequenceAutoRotateProperty;
    public void InitSequenceAutoRotate(SequenceAutoRotateItem[] items, Boolean shouldRecord){
        this._phase = CubeTiersRotateRoutinePhase.sequenceAutoRotateGap;
        this.sequenceAutoRotateProperty.sequenceAutoRotateItems = items;
        this.sequenceAutoRotateProperty.currentItemNum = 0;
        this.sequenceAutoRotateProperty.shouldRecord = shouldRecord;

        this.autoRotateProperty.anglePerSec = 360F;
        this.autoRotateProperty.bezier = new CubicBezierCurve();
        this.autoRotateProperty.duration = 0.4F;
        this.autoRotateProperty.initAngle = 0;
    }
    public void SequenceAutoRotate(Transform transform){
        if(this.phase == CubeTiersRotateRoutinePhase.sequenceAutoRotateGap){
            if (sequenceAutoRotateProperty.currentItemNum < sequenceAutoRotateProperty.sequenceAutoRotateItems.Length){
                this.autoRotateProperty.currentAngle = 0;
                Cube cubeComponent = transform.gameObject.GetComponent<Cube>();
                TierId id = sequenceAutoRotateProperty.sequenceAutoRotateItems[sequenceAutoRotateProperty.currentItemNum].id;
                this.tier = cubeComponent.tiers[id];
                this.autoRotateProperty.accumulateTime = 0;
                if (sequenceAutoRotateProperty.sequenceAutoRotateItems[sequenceAutoRotateProperty.currentItemNum].sign == Sign.plus)
                {
                    this.autoRotateProperty.targetAngle = 90;
                    this.autoRotateProperty.deltaAngle = 90 - 0;
                }
                else if (sequenceAutoRotateProperty.sequenceAutoRotateItems[sequenceAutoRotateProperty.currentItemNum].sign == Sign.minus)
                {
                    this.autoRotateProperty.targetAngle = -90;
                    this.autoRotateProperty.deltaAngle = -90 - 0;
                }
                this._phase = CubeTiersRotateRoutinePhase.sequenceAutoRotating;
                this.AutoRotate(transform, sequenceAutoRotateProperty.shouldRecord);
            } else{
                this._phase = CubeTiersRotateRoutinePhase.sleeping;
            }
        } else{
            this.AutoRotate(transform, sequenceAutoRotateProperty.shouldRecord);
        }
    }
    private void reviseCubeCoordOfBoxAndSquare(Transform transform){
        this.reviseCubeCoordOfBoxAndSquare(transform, Quaternion.identity, true);
    }
    private void reviseCubeCoordOfBoxAndSquare(Transform transform, Quaternion quaternion){
        this.reviseCubeCoordOfBoxAndSquare(transform, quaternion, true);
    }
    private void reviseCubeCoordOfBoxAndSquare(Transform transform, Quaternion rotation, bool shouldRecord){
        //Another way to handle maintaining is using Matrix4x4 api. I am not that familiar with matrix so I can't master Matrix4x4 api now. Maybe I will refactor this method when I do.
        if( rotation == Quaternion.identity ){
            if (
                   this.phase == CubeTiersRotateRoutinePhase.controlRotating 
                || this.phase == CubeTiersRotateRoutinePhase.startDetecting 
               )
            {
                if(   (this.controlDirection == Direction.positiveX && this.accumulateDeltaInCube.x > 0)
                   || (this.controlDirection == Direction.negativeX && this.accumulateDeltaInCube.x < 0)
                  )
                {
                    if(tier.id.face == Face.xy){
                        rotation = Quaternion.Euler(0, 0, 90);
                    } else if(tier.id.face == Face.xz){
                        rotation = Quaternion.Euler(0, 90, 0);
                    }
                } 
                else if(   (this.controlDirection == Direction.positiveX && this.accumulateDeltaInCube.x < 0)
                        || (this.controlDirection == Direction.negativeX && this.accumulateDeltaInCube.x > 0)
                       )
                {
                    if(tier.id.face == Face.xy){
                        rotation = Quaternion.Euler(0, 0, -90);
                    } else if(tier.id.face == Face.xz){
                        rotation = Quaternion.Euler(0, -90, 0);
                    }
                }
                else if(   (this.controlDirection == Direction.positiveY && this.accumulateDeltaInCube.y > 0)
                        || (this.controlDirection == Direction.negativeY && this.accumulateDeltaInCube.y < 0)
                       )
                {
                    if(tier.id.face == Face.xy){
                        rotation = Quaternion.Euler(0, 0, 90);
                    } else if(tier.id.face == Face.yz){
                        rotation = Quaternion.Euler(90, 0, 0);
                    }
                }
                else if(   (this.controlDirection == Direction.positiveY && this.accumulateDeltaInCube.y < 0)
                        || (this.controlDirection == Direction.negativeY && this.accumulateDeltaInCube.y > 0)
                       )
                {
                    if(tier.id.face == Face.xy){
                        rotation = Quaternion.Euler(0, 0, -90);
                    } else if(tier.id.face == Face.yz){
                        rotation = Quaternion.Euler(-90, 0, 0);
                    }
                }
                else if(   (this.controlDirection == Direction.positiveZ && this.accumulateDeltaInCube.z > 0)
                        || (this.controlDirection == Direction.negativeZ && this.accumulateDeltaInCube.z < 0)
                       )
                {
                    if(tier.id.face == Face.xz){
                        rotation = Quaternion.Euler(0, 90, 0);
                    } else if(tier.id.face == Face.yz){
                        rotation = Quaternion.Euler(90, 0, 0);
                    }
                }
                else if(   (this.controlDirection == Direction.positiveZ && this.accumulateDeltaInCube.z < 0)
                        || (this.controlDirection == Direction.negativeZ && this.accumulateDeltaInCube.z > 0)
                       )
                {
                    if(tier.id.face == Face.xz){
                        rotation = Quaternion.Euler(0, -90, 0);
                    } else if(tier.id.face == Face.yz){
                        rotation = Quaternion.Euler(-90, 0, 0);
                    }
                }
            } else if( this.phase == CubeTiersRotateRoutinePhase.autoRotating ){
                switch(this.tier.id.face){
                    case Face.xy:
                        rotation = Quaternion.Euler(0, 0, this.autoRotateProperty.targetAngle);
                        break;
                    case Face.xz:
                        rotation = Quaternion.Euler(0, this.autoRotateProperty.targetAngle, 0);
                        break;
                    case Face.yz:
                        rotation = Quaternion.Euler(this.autoRotateProperty.targetAngle, 0, 0);
                        break;
                }
            }
        }
        if(Math.Abs(rotation.eulerAngles.x) < 0.1 && Math.Abs(rotation.eulerAngles.y) < 0.1 && Math.Abs(rotation.eulerAngles.z) < 0.1 ){
        //no rotation
            return;
        }
        Cube cube = transform.gameObject.GetComponent<Cube>();
        CubeRecord cubeRecord = new CubeRecord();
        int rank = cube.rank;
        if (shouldRecord){
            float xAngle = (rotation.eulerAngles.x % 360 + 360) % 360;
            float yAngle = (rotation.eulerAngles.y % 360 + 360) % 360;
            float zAngle = (rotation.eulerAngles.z % 360 + 360) % 360;
            Sign sign = xAngle > 180 || yAngle > 180 || zAngle > 180 ? Sign.minus : Sign.plus;
            cubeRecord.tierRotation = new SequenceAutoRotateItem(tier.id, sign);
            cubeRecord.boxesState = new BoxState[rank * rank * rank];
        }
        Vector3 toCenter = new Vector3((rank - 1) / 2, (rank - 1) / 2, (rank - 1) / 2);
        HashSet<GameObject> boxesCopy = new HashSet<GameObject>(tier.boxes);
        GameObject[] boxesCopyArray = boxesCopy.OrderBy(box => box.name).ToArray();
        for (int i = 0; i < boxesCopyArray.Length; i++){
            GameObject box = boxesCopyArray[i];
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

            if(shouldRecord){
                cubeRecord.boxesState[i].cubeCoord = boxComp.cubeCoord;
                cubeRecord.boxesState[i].localRotation = boxComp.transform.localRotation;
                cubeRecord.boxesState[i].squareDirections = new Dictionary<string, Direction>();
            }

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
                if(shouldRecord){
                    cubeRecord.boxesState[i].squareDirections.Add(square.name, square.direction);
                }
            }
        }
        if(shouldRecord){
            if(cube._records.Last == cube._currentRecord){
                cube._records.AddLast(cubeRecord);
            } else{
                cube._records.AddAfter(cube._currentRecord, cubeRecord);
                while(cube._records.Last.Value != cubeRecord){
                    cube._records.RemoveLast();
                }
            }
            cube._currentRecord = cube._records.Last;
            cube.CheckButtonBackForward();
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
                if ( accumulateDeltaInCube.x / gearRatio >= 90 ){
                    result = new TestAccumulateEnoughResult(true, lastDeficientInPositiveAxes);
                } else if( accumulateDeltaInCube.x / gearRatio <= -90 ){
                    result = new TestAccumulateEnoughResult(true, lastDeficientInNegativeAxes);
                } else{
                    result = new TestAccumulateEnoughResult(false, Vector3.zero);
                }
                break;
            case Direction.negativeX:
                if ( -accumulateDeltaInCube.x / gearRatio >= 90 ){
                    result = new TestAccumulateEnoughResult(true, lastDeficientInPositiveAxes);
                } else if( -accumulateDeltaInCube.x / gearRatio <= -90 ){
                    result = new TestAccumulateEnoughResult(true, lastDeficientInNegativeAxes);
                } else{
                    result = new TestAccumulateEnoughResult(false, Vector3.zero);
                }
                break;
            case Direction.positiveY:
                if ( accumulateDeltaInCube.y / gearRatio >= 90 ){
                    result = new TestAccumulateEnoughResult(true, lastDeficientInPositiveAxes);
                } else if( accumulateDeltaInCube.y / gearRatio <= -90 ){
                    result = new TestAccumulateEnoughResult(true, lastDeficientInNegativeAxes);
                } else{
                    result = new TestAccumulateEnoughResult(false, Vector3.zero);
                }
                break;
            case Direction.negativeY:
                if ( -accumulateDeltaInCube.y / gearRatio >= 90 ){
                    result = new TestAccumulateEnoughResult(true, lastDeficientInPositiveAxes);
                } else if( -accumulateDeltaInCube.y / gearRatio <= -90 ){
                    result = new TestAccumulateEnoughResult(true, lastDeficientInNegativeAxes);
                } else{
                    result = new TestAccumulateEnoughResult(false, Vector3.zero);
                }
                break;
            case Direction.positiveZ:
                if( accumulateDeltaInCube.z / gearRatio >= 90 ){
                    result = new TestAccumulateEnoughResult(true, lastDeficientInPositiveAxes);
                } else if( accumulateDeltaInCube.z / gearRatio <= -90 ){
                    result = new TestAccumulateEnoughResult(true, lastDeficientInNegativeAxes);
                } else{
                    result = new TestAccumulateEnoughResult(false, Vector3.zero);
                }
                break;
            case Direction.negativeZ:
                if( -accumulateDeltaInCube.z / gearRatio >= 90 ){
                    result = new TestAccumulateEnoughResult(true, lastDeficientInPositiveAxes);
                } else if( -accumulateDeltaInCube.z / gearRatio <= -90 ){
                    result = new TestAccumulateEnoughResult(true, lastDeficientInNegativeAxes);
                } else{
                    result = new TestAccumulateEnoughResult(false, Vector3.zero);
                }
                break;
            default:
                result = new TestAccumulateEnoughResult(false, Vector3.zero);
                break;
        }
        return result;
    }
    private Boolean StartDetect(){
        if ( Math.Abs(this.accumulateDeltaPosition.x) > this.startDetectThreshold || Math.Abs(this.accumulateDeltaPosition.y) > this.startDetectThreshold){
            return true;
        } else{
            return false;
        }
    }
    private Boolean DetermineTierAndControlDirection(Touch firstFinger, Transform transform){
        TierId id = new TierId();
        Cube cubeComponent = transform.gameObject.GetComponent<Cube>();
        Square square = this.hit.collider.GetComponent<Square>();
        Boolean success = true;
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
                break;
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
                break;
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
                break;
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
                break;
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
                break;
            case Direction.negativeZ:
                if (Math.Abs(accumulateDeltaInCube.x) > Math.Abs(accumulateDeltaInCube.y)){
                    id = new TierId(Face.xz, square.transform.parent.GetComponent<Box>().cubeCoord.y);
                    this.tier = cubeComponent.tiers[id];
                    this.controlDirection = Direction.negativeX;
                }
                else{
                    id = new TierId(Face.yz, square.transform.parent.GetComponent<Box>().cubeCoord.x);
                    this.tier = cubeComponent.tiers[id];
                    this.controlDirection = Direction.positiveY;
                }
                break;
            default:
                this.tier = null;
                this.controlDirection = Direction.empty;
                success = false;
                break;
        }
        return success;
    }
    private void Rotate(Transform transform, float deltaAngle){
        Rotate(placeholderTouch, transform, Vector3.zero, deltaAngle);
    }
    private void Rotate(Transform transform, Vector3 deltaInCube){
        Rotate(placeholderTouch, transform, deltaInCube, 0);
    }
    private void Rotate(Touch firstFinger, Transform transform)
    {
        Rotate(firstFinger, transform, Vector3.zero, 0);
    }
    private void Rotate(Touch firstFinger, Transform transform, Vector3 deltaInCube, float availableAngle)
    {
        Boolean useAvailableAngle = false;
        if (Math.Abs(availableAngle - 0) > 0.0001){
            useAvailableAngle = true;
        } else{
            useAvailableAngle = false;
            if (deltaInCube == Vector3.zero){
                deltaInCube = transform.InverseTransformPoint(firstFinger.deltaPosition.x, firstFinger.deltaPosition.y, 0);
            }
        }
        float angle = 0;
        Vector3 axis = new Vector3();
        if(tier.id.face == Face.xy){
            axis = new Vector3(0, 0, 1);
            if(useAvailableAngle){
                angle = availableAngle;
            } else{
                switch(controlDirection){
                    case Direction.positiveX:
                        angle = deltaInCube.x / gearRatio;
                        break;
                    case Direction.negativeX:
                        angle = -deltaInCube.x / gearRatio;
                        break;
                    case Direction.positiveY:
                        angle = deltaInCube.y / gearRatio;
                        break;
                    case Direction.negativeY:
                        angle = -deltaInCube.y / gearRatio;
                        break;
                }
            }
        } else if(tier.id.face == Face.xz){
            axis = new Vector3(0, 1, 0);
            if(useAvailableAngle){
                angle = availableAngle;
            } else{
                switch(controlDirection){
                    case Direction.positiveX:
                        angle = deltaInCube.x / gearRatio;
                        break;
                    case Direction.negativeX:
                        angle = -deltaInCube.x / gearRatio;
                        break;
                    case Direction.positiveZ:
                        angle = deltaInCube.z / gearRatio;
                        break;
                    case Direction.negativeZ:
                        angle = -deltaInCube.z / gearRatio;
                        break;
                }
            }
        } else if(tier.id.face == Face.yz){
            axis = new Vector3(1, 0, 0);
            if(useAvailableAngle){
                angle = availableAngle;
            } else{
                switch(controlDirection){
                    case Direction.positiveY:
                        angle = deltaInCube.y / gearRatio;
                        break;
                    case Direction.negativeY:
                        angle = -deltaInCube.y / gearRatio;
                        break;
                    case Direction.positiveZ:
                        angle = deltaInCube.z / gearRatio;
                        break;
                    case Direction.negativeZ:
                        angle = -deltaInCube.z / gearRatio;
                        break;
                }
            }
        }
        foreach (GameObject box in tier.boxes){
            box.transform.RotateAround(Vector3.zero, transform.rotation * axis, angle);
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
        } else if(firstFinger.phase == TouchPhase.Ended || firstFinger.phase == TouchPhase.Canceled){
            if(this.phase == CubeRotateRoutinePhase.active){
                this._phase = CubeRotateRoutinePhase.sleeping;
            }
            this.Rotate(firstFinger, transform);
        }
    }
}
public struct BoxState{
    public Vector3Int cubeCoord;
    public Quaternion localRotation;
    public Dictionary<String, Direction> squareDirections;
}
public class CubeRecord{
    public CubeTiersRotateRoutine.SequenceAutoRotateItem tierRotation;
    public BoxState[] boxesState;
}
public class Cube : MonoBehaviour {
    public int rank = 3;
    public Dictionary<TierId, Tier> tiers = new Dictionary<TierId, Tier>();
    public CubeTiersRotateRoutine tiersRR = new CubeTiersRotateRoutine(7);
    CubeRotateRoutine cubeRR = new CubeRotateRoutine(7);
    public BoxState[] originBoxesState = new BoxState[0];
    float frontDistance;
    internal LinkedList<CubeRecord> _records = new LinkedList<CubeRecord>();
    internal LinkedListNode<CubeRecord> _currentRecord;
    public LinkedList<CubeRecord> records {
        get{
            return _records;
        }
    }
    public LinkedListNode<CubeRecord> currentRecord{
        get{
            return _currentRecord;
        }
    }
    ButtonBack buttonBack;
    ButtonForward buttonForward;
    CubeRecord originRecord = new CubeRecord();

    void InitProperties() {
        this.name = "cube";
        this.frontDistance = rank * 2 / 2 - 2 / 2;
        originBoxesState = new BoxState[rank * rank * rank];
        for (int x = 0; x < rank; x++){
            for (int y = 0; y < rank; y++){
                for (int z = 0; z < rank; z++){
                    originBoxesState[x * rank * rank + y * rank + z].cubeCoord = new Vector3Int(x, y, z);
                    originBoxesState[x * rank * rank + y * rank + z].localRotation = Quaternion.identity;
                    originBoxesState[x * rank * rank + y * rank + z].squareDirections = new Dictionary<string, Direction>();
                    originBoxesState[x * rank * rank + y * rank + z].squareDirections.Add("blue", Direction.negativeZ);
                    originBoxesState[x * rank * rank + y * rank + z].squareDirections.Add("green", Direction.positiveZ);
                    originBoxesState[x * rank * rank + y * rank + z].squareDirections.Add("orange", Direction.negativeX);
                    originBoxesState[x * rank * rank + y * rank + z].squareDirections.Add("red", Direction.positiveX);
                    originBoxesState[x * rank * rank + y * rank + z].squareDirections.Add("white", Direction.negativeY);
                    originBoxesState[x * rank * rank + y * rank + z].squareDirections.Add("yellow", Direction.positiveY);
                }
            }
        }
        for (int i = 0; i < this.rank; i++){
            TierId idz = new TierId(Face.xy, i);
            TierId idy = new TierId(Face.xz, i);
            TierId idx = new TierId(Face.yz, i);
            this.tiers.Add(idz, new Tier(idz, new HashSet<GameObject>()));
            this.tiers.Add(idy, new Tier(idy, new HashSet<GameObject>()));
            this.tiers.Add(idx, new Tier(idx, new HashSet<GameObject>()));
        }
        originRecord.boxesState = originBoxesState;
        originRecord.tierRotation = null;
        _records.AddLast(originRecord);
        _currentRecord = _records.Last;
        buttonBack = GameObject.Find("back").GetComponent<ButtonBack>();
        buttonForward = GameObject.Find("forward").GetComponent<ButtonForward>();
        CheckButtonBackForward();
    }
    void ConstructCube() {
        GameObject protoBox = GameObject.Find("box");
        for (int x = 0; x < rank; x++)
        {
            for (int y = 0; y < rank; y++)
            {
                for (int z = 0; z < rank; z++)
                {
                    GameObject box = Instantiate(protoBox, new Vector3(x * 2 - frontDistance, y * 2 - frontDistance, z * 2 - frontDistance), Quaternion.identity, gameObject.transform);
                    Box boxCompo = box.GetComponent<Box>();
                    boxCompo.cubeCoord = new Vector3Int(x, y, z);
                    boxCompo.originCubeCoord = new Vector3Int(x, y, z);
                    boxCompo.name = "box" + x + y + z;
                    foreach (Square square in box.GetComponentsInChildren<Square>()){
                        if(   (x == 0 && square.direction == Direction.negativeX)
                           || (x == rank-1 && square.direction == Direction.positiveX)
                           || (y == 0 && square.direction == Direction.negativeY)
                           || (y == rank - 1 && square.direction == Direction.positiveY)
                           || (z == 0 && square.direction == Direction.negativeZ)
                           || (z == rank - 1 && square.direction == Direction.positiveZ)
                          )
                        {

                        } else{
                            Destroy(square.gameObject);
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
    void LoadState(BoxState[] boxesState){
        Box[] boxes = FindObjectsOfType<Box>();
        boxes = boxes.OrderBy(box => box.name).ToArray();
        for (int i = 0; i < rank * rank * rank; i++){
            Box box = boxes[i];

            //remove box gameObject to original tier
            tiers[new TierId(Face.xy, box.cubeCoord.z)].boxes.Remove(box.gameObject);
            tiers[new TierId(Face.xz, box.cubeCoord.y)].boxes.Remove(box.gameObject);
            tiers[new TierId(Face.yz, box.cubeCoord.x)].boxes.Remove(box.gameObject);

            box.cubeCoord = boxesState[i].cubeCoord;
            box.transform.localPosition = new Vector3(boxesState[i].cubeCoord.x * 2 - frontDistance, boxesState[i].cubeCoord.y * 2 - frontDistance, boxesState[i].cubeCoord.z * 2 - frontDistance);
            box.transform.localRotation = boxesState[i].localRotation;
            Square[] squares = FindObjectsOfType<Square>();
            foreach(Square square in squares){
                square.direction = boxesState[i].squareDirections[square.name];
            }

            //add box gameObject to proper tier
            tiers[new TierId(Face.xy, box.cubeCoord.z)].boxes.Add(box.gameObject);
            tiers[new TierId(Face.xz, box.cubeCoord.y)].boxes.Add(box.gameObject);
            tiers[new TierId(Face.yz, box.cubeCoord.x)].boxes.Add(box.gameObject);
        }
    }
    // Use this for initialization
    void Start () {
        InitProperties();
        ConstructCube();
        InitView();
	}

    // Update is called once per frame
	void Update () {
        if(this.tiersRR.phase == CubeTiersRotateRoutinePhase.autoRotating){
            this.tiersRR.AutoRotate(transform);
        } else if(this.tiersRR.phase == CubeTiersRotateRoutinePhase.sequenceAutoRotating || this.tiersRR.phase == CubeTiersRotateRoutinePhase.sequenceAutoRotateGap){
            this.tiersRR.SequenceAutoRotate(transform);
            if(Input.touchCount > 0){
                this.cubeRR.HandleTouch(Input.touches, transform);
            }
        } else if(Input.touchCount > 0){
            if (this.cubeRR.phase == CubeRotateRoutinePhase.active){
                this.cubeRR.HandleTouch(Input.touches, transform);
            } else if(this.tiersRR.phase == CubeTiersRotateRoutinePhase.startDetecting || this.tiersRR.phase == CubeTiersRotateRoutinePhase.controlRotating){
                this.tiersRR.HandleTouch(Input.touches, transform);
            } else if(this.cubeRR.phase == CubeRotateRoutinePhase.sleeping && this.tiersRR.phase == CubeTiersRotateRoutinePhase.sleeping && Input.touches[0].phase == TouchPhase.Began){
                Ray raycast = Camera.main.ScreenPointToRay(Input.GetTouch(0).position);
                RaycastHit hit;
                Boolean isHit = Physics.Raycast(raycast, out hit);
                if(!isHit){
                    this.cubeRR.HandleTouch(Input.touches, transform);
                } else if(isHit && hit.collider.tag == "square"){
                    this.tiersRR.HandleTouch(Input.touches, transform, hit);
                }
            }
        }
	}
    public Boolean SequenceAutoRotateTier(CubeTiersRotateRoutine.SequenceAutoRotateItem[] items){
        return SequenceAutoRotateTier(items, true);
    }
    public Boolean SequenceAutoRotateTier(CubeTiersRotateRoutine.SequenceAutoRotateItem[] items, Boolean shouldRecord){
        if(this.tiersRR.phase == CubeTiersRotateRoutinePhase.sleeping){
            this.tiersRR.InitSequenceAutoRotate(items, shouldRecord);
            return true;
        } else{
            return false;
        }
    }
    public Boolean StartOver(){
        if(this.tiersRR.phase == CubeTiersRotateRoutinePhase.sleeping){
            LoadState(originBoxesState);
            _records.Clear();
            _records.AddLast(originRecord);
            _currentRecord = _records.Last;
            CheckButtonBackForward();
            return true;
        } else{
            return false;
        }
    }
    internal void CheckButtonBackForward(){
        buttonBack.CheckState(this);
        buttonForward.CheckState(this);
    }
    public Boolean Back(){
        if(this.tiersRR.phase == CubeTiersRotateRoutinePhase.sleeping && this.currentRecord != this.records.First){
            CubeTiersRotateRoutine.SequenceAutoRotateItem tierRotation = (CubeTiersRotateRoutine.SequenceAutoRotateItem)this.currentRecord.Value.tierRotation.Clone();
            tierRotation.sign = tierRotation.sign == Sign.minus ? Sign.plus : Sign.minus;
            SequenceAutoRotateTier(new CubeTiersRotateRoutine.SequenceAutoRotateItem[]{tierRotation}, false);
            this._currentRecord = this.currentRecord.Previous;
            CheckButtonBackForward();
            return true;
        } else{
            return false;
        }
    }
    public Boolean Forward(){
        if (this.tiersRR.phase == CubeTiersRotateRoutinePhase.sleeping && this.currentRecord != this.records.Last){
            CubeTiersRotateRoutine.SequenceAutoRotateItem tierRotation = (CubeTiersRotateRoutine.SequenceAutoRotateItem)this.currentRecord.Next.Value.tierRotation.Clone();
            SequenceAutoRotateTier(new CubeTiersRotateRoutine.SequenceAutoRotateItem[] { tierRotation }, false);
            this._currentRecord = this.currentRecord.Next;
            CheckButtonBackForward();
            return true;
        } else{
            return false;
        }
    }
}
