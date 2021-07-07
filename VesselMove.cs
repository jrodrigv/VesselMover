using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSP.Localization;

namespace VesselMover
{
  [KSPAddon(KSPAddon.Startup.Flight, false)]
  public class VesselMove : MonoBehaviour
  {
    #region Declarations

    public static VesselMove Instance;

    public enum MoveModes { Normal = 0, Slow = 1, Fine = 2, Ludicrous = 3 }

    private MoveModes _moveMode = MoveModes.Normal;
    private bool _moving = false;
    private List<Vessel> _placingVessels = new List<Vessel>();
    private bool _hoverChanged;
    
    public float MoveHeight = 0;
    private float _hoverAdjust = 0f;
    private readonly float[] _hoverHeights = new float[] { 35, 15, 5, 3000 };

    private float HoverHeight
    {
      get
      {
        return _hoverHeights[(int)_moveMode];
      }
    }

    private readonly float[] _moveSpeeds = new float[] { 10, 5, 1, 1500 };

    private float MoveSpeed
    {
      get
      {
        return _moveSpeeds[(int)_moveMode];
      }
    }

    private readonly float[] _moveAccels = new float[] { 10, 1, 0.5f, 750 };

    private float MoveAccel
    {
      get
      {
        return _moveAccels[(int)_moveMode];
      }
    }

    private readonly float[] _rotationSpeeds = new float[] { 50, 20, 10, 50 };

    private float RotationSpeed
    {
      get
      {
        return _rotationSpeeds[(int)_moveMode] * Time.fixedDeltaTime;
      }
    }

    public bool IsMovingVessel = false;
    public Vessel MovingVessel;
    private Quaternion _startRotation;
    private Quaternion _currRotation;
    private float _currMoveSpeed = 0;
    private Vector3 _currMoveVelocity;
    private VesselBounds _vBounds;
    private LineRenderer _debugLr;
    private Vector3 _up;
    private Vector3 _startingUp;
    private readonly float maxPlacementSpeed = 1050;
    private bool _hasRotated = false;
    private float _timeBoundsUpdated = 0;
    private ScreenMessage _moveMessage;

    #endregion

    #region KSP Events

    private void Awake()
    {
      if (Instance)
      {
        Destroy(Instance);
      }
      Instance = this;
    }

    private void Start()
    {
      _debugLr = new GameObject().AddComponent<LineRenderer>();
      _debugLr.material = new Material(Shader.Find("KSP/Emissive/Diffuse"));
      _debugLr.material.SetColor("_EmissiveColor", Color.green);
      _debugLr.startWidth = 0.15f;
      _debugLr.endWidth = 0.15f;
      _debugLr.enabled = false;
    }

    private void Update()
    {
      if (_moving)
      {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
          ToggleMoveMode();
        }
      }

      // Changed to a GameEvents handler flag.  This test was too sensitive and caused issues with display.
      // a latched setting will prevent misfires.
      if (IsMovingVessel)
      {
        _debugLr.enabled = VesselMoverToolbar.ShowUI;
      }
    }

    private void FixedUpdate()
    {
      if (!_moving) return;
      MovingVessel.IgnoreGForces(240);
      UpdateMove();

      if (_hasRotated && Time.time - _timeBoundsUpdated > 0.2f)
      {
        UpdateBounds();
      }
    }

    private void LateUpdate()
    {
      if (_moving)
      {
        UpdateDebugLines();
      }
    }

    #endregion

    private void UpdateBounds()
    {
      _hasRotated = false;
      _vBounds.UpdateBounds();
      _timeBoundsUpdated = Time.time;
    }

    private void UpdateMove()
    {
      if (!MovingVessel)
      {
        EndMove();
        return;
      }
      MovingVessel.IgnoreGForces(240);

      // Lerp is animating move
      if (!_hoverChanged)
        MoveHeight = Mathf.Lerp(MoveHeight, _vBounds.BottomLength + HoverHeight, 10 * Time.fixedDeltaTime);
      else
      {
        double alt = MovingVessel.radarAltitude;
        // sINCE Lerp is animating move from 0 to hoverheight, we do not want this going below current altitude
        if (MoveHeight < alt) MoveHeight = Convert.ToSingle(alt); 

        MoveHeight = MovingVessel.Splashed 
          ? Mathf.Lerp(MoveHeight, _vBounds.BottomLength + _hoverAdjust, 10 * Time.fixedDeltaTime) 
          : Mathf.Lerp(MoveHeight, _vBounds.BottomLength + (MoveHeight + _hoverAdjust < 0 ? -MoveHeight : _hoverAdjust), 10 * Time.fixedDeltaTime);
      }
      MovingVessel.ActionGroups.SetGroup(KSPActionGroup.RCS, false);

      _up = (MovingVessel.transform.position - FlightGlobals.currentMainBody.transform.position).normalized;

      Vector3 forward;
      if (MapView.MapIsEnabled)
      {
        forward = North();
      }
      else
      {
        forward = Vector3.ProjectOnPlane(MovingVessel.CoM - FlightCamera.fetch.mainCamera.transform.position, _up).normalized;
        if (Vector3.Dot(-_up, FlightCamera.fetch.mainCamera.transform.up) > 0)
        {
          forward = Vector3.ProjectOnPlane(FlightCamera.fetch.mainCamera.transform.up, _up).normalized;
        }
      }

      Vector3 right = Vector3.Cross(_up, forward);

      Vector3 offsetDirection = Vector3.zero;
      bool inputting = false;

      //Altitude Adjustment
      if (GameSettings.THROTTLE_CUTOFF.GetKey())
      {
        _hoverAdjust = 0f;
        _hoverChanged = false;
      }

      if (GameSettings.THROTTLE_UP.GetKey())
      {
        _hoverAdjust += MoveSpeed * Time.fixedDeltaTime;
        inputting = true;
        _hoverChanged = true;
      }

      if (GameSettings.THROTTLE_DOWN.GetKey())
      {
        _hoverAdjust += -(MoveSpeed * Time.fixedDeltaTime);
        inputting = true;
        _hoverChanged = true;
      }

      if (GameSettings.PITCH_DOWN.GetKey())
      {
        offsetDirection += (forward * MoveSpeed * Time.fixedDeltaTime);
        inputting = true;
      }
      if (GameSettings.PITCH_UP.GetKey())
      {
        offsetDirection += (-forward * MoveSpeed * Time.fixedDeltaTime);
        inputting = true;
      }

      if (GameSettings.YAW_RIGHT.GetKey())
      {
        offsetDirection += (right * MoveSpeed * Time.fixedDeltaTime);
        inputting = true;
      }
      if (GameSettings.YAW_LEFT.GetKey())
      {
        offsetDirection += (-right * MoveSpeed * Time.fixedDeltaTime);
        inputting = true;
      }

      if (GameSettings.TRANSLATE_RIGHT.GetKey())
      {
        _startRotation = Quaternion.AngleAxis(-RotationSpeed, MovingVessel.ReferenceTransform.forward) * _startRotation;
        _hasRotated = true;
      }
      else if (GameSettings.TRANSLATE_LEFT.GetKey())
      {
        _startRotation = Quaternion.AngleAxis(RotationSpeed, MovingVessel.ReferenceTransform.forward) * _startRotation;
        _hasRotated = true;
      }

      if (GameSettings.TRANSLATE_DOWN.GetKey())
      {
        _startRotation = Quaternion.AngleAxis(RotationSpeed, MovingVessel.ReferenceTransform.right) * _startRotation;
        _hasRotated = true;
      }
      else if (GameSettings.TRANSLATE_UP.GetKey())
      {
        _startRotation = Quaternion.AngleAxis(-RotationSpeed, MovingVessel.ReferenceTransform.right) * _startRotation;
        _hasRotated = true;
      }

      if (GameSettings.ROLL_LEFT.GetKey())
      {
        _startRotation = Quaternion.AngleAxis(RotationSpeed, MovingVessel.ReferenceTransform.up) * _startRotation;
        _hasRotated = true;
      }
      else if (GameSettings.ROLL_RIGHT.GetKey())
      {
        _startRotation = Quaternion.AngleAxis(-RotationSpeed, MovingVessel.ReferenceTransform.up) * _startRotation;
        _hasRotated = true;
      }

      //auto level plane
      if (GameSettings.TRANSLATE_FWD.GetKey())
      {
        Quaternion targetRot = Quaternion.LookRotation(-_up, forward);
        _startRotation = Quaternion.RotateTowards(_startRotation, targetRot, RotationSpeed * 2);
        _hasRotated = true;
      }
      else if (GameSettings.TRANSLATE_BACK.GetKey())//auto level rocket
      {
        Quaternion targetRot = Quaternion.LookRotation(forward, _up);
        _startRotation = Quaternion.RotateTowards(_startRotation, targetRot, RotationSpeed * 2);
        _hasRotated = true;
      }

      if (inputting)
      {
        _currMoveSpeed = Mathf.Clamp(Mathf.MoveTowards(_currMoveSpeed, MoveSpeed, MoveAccel * Time.fixedDeltaTime), 0, MoveSpeed);
      }
      else
      {
        _currMoveSpeed = 0;
      }

      Vector3 offset = offsetDirection.normalized * _currMoveSpeed;
      _currMoveVelocity = offset / Time.fixedDeltaTime;
      Vector3 vSrfPt = MovingVessel.CoM - (MoveHeight * _up);
      bool srfBelowWater = false;
      RaycastHit ringHit = default;


      bool surfaceDetected = false;
      var rayCastHits= CapsuleCast();


      Array.Sort(rayCastHits, (r1, r2) => r1.distance.CompareTo(r2.distance));

      foreach (var hit in rayCastHits)
      {
          var partHit = hit.collider.gameObject.GetComponentInParent<Part>();

          if (partHit == null)
          {
              ringHit = hit;
              surfaceDetected = true;
              break;
          }

          if (partHit?.vessel == MovingVessel) continue;


          ringHit = hit;
          surfaceDetected = true;
          break;
      }

      Vector3 finalOffset = Vector3.zero;

            

      if (surfaceDetected)
      {
        if (FlightGlobals.getAltitudeAtPos(ringHit.point) < 0)
        {
          srfBelowWater = true;
        }

        Vector3 rOffset = Vector3.Project(ringHit.point - vSrfPt, _up);
        Vector3 mOffset = (vSrfPt + offset) - MovingVessel.CoM;
        finalOffset = rOffset + mOffset + (MoveHeight * _up);
        MovingVessel.Translate(finalOffset);
      }

      PQS bodyPQS = MovingVessel.mainBody.pqsController;

      Vector3d geoCoords = WorldPositionToGeoCoords(MovingVessel.GetWorldPos3D() + (_currMoveVelocity * Time.fixedDeltaTime), MovingVessel.mainBody);
      double lat = geoCoords.x;
      double lng = geoCoords.y;

      Vector3d bodyUpVector = new Vector3d(1, 0, 0);
      bodyUpVector = QuaternionD.AngleAxis(lat, Vector3d.forward/*around Z axis*/) * bodyUpVector;
      bodyUpVector = QuaternionD.AngleAxis(lng, Vector3d.down/*around -Y axis*/) * bodyUpVector;

      double srfHeight = bodyPQS.GetSurfaceHeight(bodyUpVector);

      //double alt = srfHeight - bodyPQS.radius;         
      //double rAlt = movingVessel.radarAltitude;
      //double tAlt = TrueAlt(movingVessel);
      //double pAlt = movingVessel.pqsAltitude;            
      //double teralt = movingVessel.mainBody.TerrainAltitude(movingVessel.mainBody.GetLatitude(geoCoords), movingVessel.mainBody.GetLongitude(geoCoords));
      //Debug.Log ("Surface height: "+movingVessel.mainBody.pqsController.GetSurfaceHeight(up));

      if (!surfaceDetected || srfBelowWater)
      {
        Vector3 terrainPos = MovingVessel.mainBody.position + (float)srfHeight * _up;
        Vector3 waterSrfPoint = FlightGlobals.currentMainBody.position + ((float)FlightGlobals.currentMainBody.Radius * _up);

        if (!surfaceDetected)
        {
          MovingVessel.SetPosition(terrainPos + (MoveHeight * _up) + offset);
        }
        else
        {
          MovingVessel.SetPosition(waterSrfPoint + (MoveHeight * _up) + offset);
        }

        //update vessel situation to splashed down:
        MovingVessel.UpdateLandedSplashed();
      }

      //fix surface rotation
      Quaternion srfRotFix = Quaternion.FromToRotation(_startingUp, _up);
      _currRotation = srfRotFix * _startRotation;
      MovingVessel.SetRotation(_currRotation);

      if (Vector3.Angle(_startingUp, _up) > 5)
      {
        _startRotation = _currRotation;
        _startingUp = _up;
      }

      MovingVessel.SetWorldVelocity(Vector3d.zero);
      MovingVessel.angularVelocity = Vector3.zero;
      MovingVessel.angularMomentum = Vector3.zero;
    }

    private Vector3d WorldPositionToGeoCoords(Vector3d worldPosition, CelestialBody body)
    {
      if (!body)
      {
        Debug.LogWarning("WorldPositionToGeoCoords body is null");
        return Vector3d.zero;
      }

      double lat = body.GetLatitude(worldPosition);
      double longi = body.GetLongitude(worldPosition);
      double alt = body.GetAltitude(worldPosition);
      return new Vector3d(lat, longi, alt);
    }

    public void StartMove(Vessel v, bool forceReleaseClamps)
    {
      if (!v)
      {
        Debug.Log("[VesselMover] : Vessel mover tried to move a null vessel.");
      }

      if (v.packed)
      {
        return;
      }

      if (!_placingVessels.Contains(v) && v.LandedOrSplashed)
      {
        foreach (LaunchClamp clamp in v.FindPartModulesImplementing<LaunchClamp>())
        {
          if (forceReleaseClamps)
          {
            clamp.Release();
          }
          else
          {
            return;
          }
        }

        ShowModeMessage();

        MovingVessel = v;
        IsMovingVessel = true;

        _up = (v.transform.position - v.mainBody.transform.position).normalized;
        _startingUp = _up;

        _vBounds = new VesselBounds(MovingVessel);
        _moving = true;
        MoveHeight = _vBounds.BottomLength + 0.5f;

        _startRotation = MovingVessel.transform.rotation;
        _currRotation = _startRotation;

        _debugLr.enabled = true;
      }
    }

    public void EndMove()
    {
      StartCoroutine(EndMoveRoutine(_vBounds));
      IsMovingVessel = false;
      _debugLr.enabled = false;
    }

    public void DropMove()
    {
      StartCoroutine(DropMoveRoutine(_vBounds));
      IsMovingVessel = false;
      _debugLr.enabled = false;
    }

    private IEnumerator EndMoveRoutine(VesselBounds vesselBounds)
    {
      Vessel v = vesselBounds.vessel;
      if (!v) yield break;

      yield return new WaitForFixedUpdate();
      vesselBounds.UpdateBounds();

      yield return new WaitForFixedUpdate();

      while (_moveMode != MoveModes.Normal)
      {
        ToggleMoveMode();
      }

      _moving = false;
      MoveHeight = 0;
      _placingVessels.Add(vesselBounds.vessel);
      float bottomLength = _vBounds.BottomLength;

      //float heightOffset = GetRadarAltitude(movingVessel) - moveHeight;

      float altitude = GetRaycastAltitude(vesselBounds);

      while (v && !v.LandedOrSplashed)
      {
        v.IgnoreGForces(240);
        MovingVessel.IgnoreGForces(240);

        _up = (v.transform.position - FlightGlobals.currentMainBody.transform.position).normalized;
        float placeSpeed = Mathf.Clamp(((altitude - bottomLength) * 2), 0.1f, maxPlacementSpeed);
        if (placeSpeed > 3)
        {
          v.SetWorldVelocity(Vector3.zero);
          MovingVessel.angularVelocity = Vector3.zero;
          MovingVessel.angularMomentum = Vector3.zero;
          v.Translate(placeSpeed * Time.fixedDeltaTime * -_up);
        }
        else
        {
          v.SetWorldVelocity(placeSpeed * -_up);
        }
        altitude -= placeSpeed * Time.fixedDeltaTime;
        yield return new WaitForFixedUpdate();
      }

      _placingVessels.Remove(v);
      _hoverAdjust = 0f;
    }

    private IEnumerator DropMoveRoutine(VesselBounds vesselBounds)
    {
      Vessel v = vesselBounds.vessel;
      if (!v) yield break;

      _moving = false;
      MoveHeight = 0;
      _hoverAdjust = 0f;
    }

    private void UpdateDebugLines()
    {
      int circleRes = 24;

      Vector3[] positions = new Vector3[circleRes + 3];
      for (int i = 0; i < circleRes; i++)
      {
        positions[i] = GetBoundPoint(i, circleRes, 1);
      }
      positions[circleRes] = GetBoundPoint(0, circleRes, 1);
      positions[circleRes + 1] = MovingVessel.CoM;
      positions[circleRes + 2] = MovingVessel.CoM + (MoveHeight * -_up);

      _debugLr.positionCount = circleRes + 3;
      _debugLr.SetPositions(positions);
    }

    private Vector3 GetBoundPoint(int index, int totalPoints, float radiusFactor)
    {
      float angleIncrement = 360 / (float)totalPoints;

      float angle = index * angleIncrement;

      Vector3 forward = North();//Vector3.ProjectOnPlane((movingVessel.CoM)-FlightCamera.fetch.mainCamera.transform.position, up).normalized;

      float radius = _vBounds.Radius;

      Vector3 offsetVector = (radius * radiusFactor * forward);
      offsetVector = Quaternion.AngleAxis(angle, _up) * offsetVector;

      Vector3 point = MovingVessel.CoM + offsetVector;

      return point;
    }

    private RaycastHit[] CapsuleCast()
    {
      //float radius = (Mathf.Max (Mathf.Max(vesselBounds.size.x, vesselBounds.size.y), vesselBounds.size.z)) + (currMoveSpeed*2);
      float radius = _vBounds.Radius + Mathf.Clamp(_currMoveSpeed, 0, 200);

      return Physics.CapsuleCastAll(MovingVessel.CoM + (250 * _up), MovingVessel.CoM + (249 * _up), radius, -_up, 2000, 1 << 15);
    }

    private float GetRadarAltitude(Vessel vessel)
    {
      //Not needed anymore - RadarAlt is part of vessel now 
      float radarAlt = Mathf.Clamp((float)(vessel.mainBody.GetAltitude(vessel.CoM) - vessel.terrainAltitude), 0, (float)vessel.altitude);
      return radarAlt;
    }

    private float GetRaycastAltitude(VesselBounds vesselBounds) //TODO do the raycast from the bottom point of the ship, and include vessels in layer mask, so you can safely place on top of vessel
    {
      RaycastHit hit;

      //test
      if (Physics.Raycast(vesselBounds.vessel.CoM - (vesselBounds.BottomLength * _up), -_up, out hit, (float)vesselBounds.vessel.altitude, (1 << 15) | (1 << 0)))
      {
        return Vector3.Project(hit.point - vesselBounds.vessel.CoM, _up).magnitude;
      }

      /*
			if(Physics.Raycast(vesselBounds.vessel.CoM, -up, out hit, (float)vesselBounds.vessel.altitude, (1<<15)))
			{
				return hit.distance;
			}*/

      else
      {
        //return GetRadarAltitude(vesselBounds.vessel);
        return (float)vesselBounds.vessel.radarAltitude;
      }
    }

    private Vector3 GetRaycastPosition(VesselBounds vesselBounds)
    {
      Vector3 ZeroVector = new Vector3(0, 0, 0);
      RaycastHit hit;
      if (Physics.Raycast(vesselBounds.vessel.CoM - (vesselBounds.BottomLength * _up), -_up, out hit, (float)vesselBounds.vessel.altitude, (1 << 15) | (1 << 0)))
      {
        return Vector3.Project(hit.point - vesselBounds.vessel.CoM, _up);
      }
      else
      {
        return ZeroVector;
      }
    }

    private void ToggleMoveMode()
    {
      _moveMode = (MoveModes)(int)Mathf.Repeat((float)_moveMode + 1, 4);
      ShowModeMessage();

      switch (_moveMode)
      {
        case MoveModes.Normal:
          _debugLr.material.SetColor("_EmissiveColor", Color.green);
          break;
        case MoveModes.Slow:
          _debugLr.material.SetColor("_EmissiveColor", XKCDColors.Orange);
          break;
        case MoveModes.Fine:
          _debugLr.material.SetColor("_EmissiveColor", XKCDColors.BrightRed);
          break;
        case MoveModes.Ludicrous:
          _debugLr.material.SetColor("_EmissiveColor", XKCDColors.PurpleishBlue);
          break;
      }
    }

    private void ShowModeMessage()
    {
      if (_moveMessage != null)
      {
        ScreenMessages.RemoveMessage(_moveMessage);
      }
      _moveMessage = ScreenMessages.PostScreenMessage(Localizer.Format("#VesselMover_SrcMsg", _moveMode.ToString()), 3, ScreenMessageStyle.UPPER_CENTER);//"Mode : " +
    }

    private Vector3 North()
    {
      Vector3 n = MovingVessel.mainBody.GetWorldSurfacePosition(MovingVessel.latitude + 1, MovingVessel.longitude, MovingVessel.altitude) - MovingVessel.GetWorldPos3D();
      n = Vector3.ProjectOnPlane(n, _up);
      return n.normalized;
    }

    public struct VesselBounds
    {
      public Vessel vessel;
      public float BottomLength;
      public float Radius;

      private Vector3 _localBottomPoint;
      public Vector3 BottomPoint
      {
        get
        {
          return vessel.transform.TransformPoint(_localBottomPoint);
        }
      }

      public VesselBounds(Vessel v)
      {
        vessel = v;
        BottomLength = 0;
        Radius = 0;
        _localBottomPoint = Vector3.zero;
        UpdateBounds();
      }

      public void UpdateBounds()
      {
        Vector3 up = (vessel.CoM - vessel.mainBody.transform.position).normalized;
        Vector3 forward = Vector3.ProjectOnPlane(vessel.CoM - FlightCamera.fetch.mainCamera.transform.position, up).normalized;
        Vector3 right = Vector3.Cross(up, forward);

        float maxSqrDist = 0;
        Part furthestPart = null;

        //bottom check
        Vector3 downPoint = vessel.CoM - (2000 * up);
        Vector3 closestVert = vessel.CoM;
        float closestSqrDist = Mathf.Infinity;

        //radius check
        Vector3 furthestVert = vessel.CoM;
        float furthestSqrDist = -1;

        foreach (Part p in vessel.parts)
        {
          if (p.Modules.Contains("Tailhook")) return;
          if (p.Modules.Contains("Arrestwire")) return;
          if (p.Modules.Contains("Catapult")) return;
          if (p.Modules.Contains("CLLS")) return;
          if (p.Modules.Contains("OLS")) return;

          float sqrDist = Vector3.ProjectOnPlane((p.transform.position - vessel.CoM), up).sqrMagnitude;
          if (sqrDist > maxSqrDist)
          {
            maxSqrDist = sqrDist;
            furthestPart = p;
          }

          //if(Vector3.Dot(up, p.transform.position-vessel.CoM) < 0)
          //{

          foreach (MeshFilter mf in p.GetComponentsInChildren<MeshFilter>())
          {
            //Mesh mesh = mf.mesh;
            foreach (Vector3 vert in mf.mesh.vertices)
            {
              //bottom check
              Vector3 worldVertPoint = mf.transform.TransformPoint(vert);
              float bSqrDist = (downPoint - worldVertPoint).sqrMagnitude;
              if (bSqrDist < closestSqrDist)
              {
                closestSqrDist = bSqrDist;
                closestVert = worldVertPoint;
              }

              //radius check
              //float sqrDist = (vessel.CoM-worldVertPoint).sqrMagnitude;
              float hSqrDist = Vector3.ProjectOnPlane(vessel.CoM - worldVertPoint, up).sqrMagnitude;
              if (!(hSqrDist > furthestSqrDist)) continue;
              furthestSqrDist = hSqrDist;
              furthestVert = worldVertPoint;
            }
          }

          //}
        }

        Vector3 radVector = Vector3.ProjectOnPlane(furthestVert - vessel.CoM, up);
        Radius = radVector.magnitude;

        BottomLength = Vector3.Project(closestVert - vessel.CoM, up).magnitude;
        _localBottomPoint = vessel.transform.InverseTransformPoint(closestVert);

        //Debug.Log ("Vessel bottom length: "+bottomLength);
        /*
				if(furthestPart!=null)
				{
					//Debug.Log ("Furthest Part: "+furthestPart.partInfo.title);

					Vector3 furthestVert = vessel.CoM;
					float furthestSqrDist = -1;

					foreach(var mf in furthestPart.GetComponentsInChildren<MeshFilter>())
					{
						Mesh mesh = mf.mesh;
						foreach(var vert in mesh.vertices)
						{
							Vector3 worldVertPoint = mf.transform.TransformPoint(vert);
							float sqrDist = (vessel.CoM-worldVertPoint).sqrMagnitude;
							if(sqrDist > furthestSqrDist)
							{
								furthestSqrDist = sqrDist;
								furthestVert = worldVertPoint;
							}
						}
					}

					Vector3 radVector = Vector3.ProjectOnPlane(furthestVert-vessel.CoM, up);
					radius = radVector.magnitude;
					//Debug.Log ("Vert test found radius to be "+radius);
				}
				*/
        //radius *= 1.75f;
        //radius += 5;//15;
        Radius += Mathf.Clamp(Radius, 2, 10);
      }


    }

    public static List<string> partIgnoreModules = new List<string>(9)
        {
            "Tailhook",
            "Arrestwire",
            "Catapult",
            "CLLS",
            "OLS"
        };

    private static bool IsPartModuleIgnored(string ModuleName)
    {
      return true;
    }

  }
}

