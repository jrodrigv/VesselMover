using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KSP.UI.Screens;
using KSP.Localization;

namespace VesselMover
{
  [KSPAddon(KSPAddon.Startup.Flight, false)]
  public class VesselMoverToolbar : MonoBehaviour
  {
    public static bool hasAddedButton = false;
    public static bool toolbarGuiEnabled = false;
    public static bool addCrewMembers = true;
    public static bool selectCrewMembers = false;
    public static bool ShowUI = true;
    private static bool latch;
    internal static GUIStyle ButtonToggledStyle;
    // ReSharper disable once PossibleNullReferenceException
    internal static GUIStyle ToolTipStyle;
    internal static List<ProtoCrewMember> SelectedCrewMembers = new List<ProtoCrewMember>();
    private Rect toolbarRect;
    private float toolbarWidth = 280;
    private float toolbarHeight = 0;
    private float toolbarMargin = 6;
    private float toolbarLineHeight = 20;
    private float contentWidth;
    private Vector2 toolbarPosition;
    private Rect svRectScreenSpace;
    private Rect svCrewScreenSpace;
    private bool showMoveHelp = false;
    private float helpHeight;

    private Rect _crewSelectRect;
    private float _crewSelectWidth = 300;
    private float _crewSelectHeight = 300;
    private static Vector2 _crewSelectPosition;
    private static Vector2 _displayViewerPosition = Vector2.zero;
    private static Rect Position;

    private void Start()
    {
      GameEvents.onHideUI.Add(OnHideUI);
      GameEvents.onShowUI.Add(OnShowUI);
      toolbarPosition = new Vector2(Screen.width - toolbarWidth - 80, 39);
      toolbarRect = new Rect(toolbarPosition.x, toolbarPosition.y, toolbarWidth, toolbarHeight);
      _crewSelectPosition = new Vector2(Screen.width/2 - _crewSelectWidth/2, Screen.height/2 - _crewSelectHeight /2);
      _crewSelectRect = new Rect(_crewSelectPosition.x, _crewSelectPosition.y, _crewSelectWidth, _crewSelectHeight);
      contentWidth = toolbarWidth - (2 * toolbarMargin);

      SelectedCrewMembers = new List<ProtoCrewMember>();

      AddToolbarButton();
    }

    private void OnDestroy()
    {
      GameEvents.onHideUI.Remove(OnHideUI);
      GameEvents.onShowUI.Remove(OnShowUI);
    }

    private void OnGUI()
    {
      SetGuiStyles();

      if (ShowUI && addCrewMembers && VesselSpawn.IsSelectingCrew)
      {
        _crewSelectRect = GUILayout.Window(401239, _crewSelectRect, CrewSelectionWindow, Localizer.Format("#VesselMover_SelectCrew"), HighLogic.Skin.window);//"Select Crew"
        if (!latch) Debug.Log(_crewSelectRect.ToString());
        latch = true;
      }

      if (!ShowUI || !toolbarGuiEnabled || !VesselMove.Instance || !VesselSpawn.instance ||
          VesselSpawn.instance.openingCraftBrowser || VesselSpawn.IsSelectingCrew) return;
      toolbarRect = GUI.Window(401240, toolbarRect, ToolbarWindow, Localizer.Format("#VesselMover_VesselMover"), HighLogic.Skin.window);//"Vessel Mover"

      if (!VesselMove.Instance.IsMovingVessel)
      {
        if (!MouseIsInRect(svRectScreenSpace)) return;
        Vector2 mousePos = MouseGUIPos();
        //Rect warningRect = new Rect(mousePos.x + 5, mousePos.y + 20, 200, 60);
        ShowToolTip(mousePos, Localizer.Format("#VesselMover_WARNING"));//"WARNING: Experimental. Launch clamps may be broken."
      }
      else if (showMoveHelp)
      {
        GUI.Window(401241, new Rect(toolbarRect.x, toolbarRect.y + toolbarRect.height, toolbarRect.width, helpHeight), MoveHelp, Localizer.Format("#VesselMover_Controls"), HighLogic.Skin.window);//"Controls"
      }

    }

    private void ToolbarWindow(int windowID)
    {
      float line = 0;
      line += 1.25f;

      if (FlightGlobals.ActiveVessel && (FlightGlobals.ActiveVessel.LandedOrSplashed || VesselMove.Instance.IsMovingVessel))
      {
        if (!VesselMove.Instance.IsMovingVessel)
        {
          if (GUI.Button(LineRect(ref line, 1.5f), Localizer.Format("#VesselMover_MoveVessel"), HighLogic.Skin.button))//"Move Vessel"
          {
            VesselMove.Instance.StartMove(FlightGlobals.ActiveVessel, true);
          }
          line += 0.2f;

          Rect spawnVesselRect = LineRect(ref line);
          svRectScreenSpace = new Rect(spawnVesselRect);
          svRectScreenSpace.x += toolbarRect.x;
          svRectScreenSpace.y += toolbarRect.y;

          if (GUI.Button(spawnVesselRect, Localizer.Format("#VesselMover_SpawnVessel"), HighLogic.Skin.button))//"Spawn Vessel"
          {
            VesselSpawn.instance.StartVesselSpawn();
          }

          line += .75f;
          Rect crewRect1 = LineRect(ref line);
          Rect crewRect2 = new Rect(crewRect1.x + crewRect1.width/2 + 5f, crewRect1.y, crewRect1.width / 2, crewRect1.height);
          crewRect1.width = crewRect1.width / 2;
          svCrewScreenSpace = new Rect(crewRect1);
          svCrewScreenSpace.x += toolbarRect.x;
          svCrewScreenSpace.y += toolbarRect.y;
          addCrewMembers = GUI.Toggle(crewRect1, addCrewMembers, Localizer.Format("#VesselMover_SpawnCrew"));//"Spawn Crew"
          if (!addCrewMembers) GUI.enabled = false;
          selectCrewMembers = GUI.Toggle(crewRect2, selectCrewMembers, Localizer.Format("#VesselMover_ChooseCrew"));//"Choose Crew"
          GUI.enabled = true;
          showMoveHelp = false;
        }
        else
        {
          if (GUI.Button(LineRect(ref line, 2), Localizer.Format("#VesselMover_PlaceVessel"), HighLogic.Skin.button))//"Place Vessel"
          {
            VesselMove.Instance.EndMove();
          }

          line += 0.3f;

          if (GUI.Button(LineRect(ref line, 2), Localizer.Format("#VesselMover_DropVessel"), HighLogic.Skin.button))//"Drop Vessel"
          {
            VesselMove.Instance.DropMove();
          }

          line += 0.3f;

          if (GUI.Button(LineRect(ref line), Localizer.Format("#VesselMover_Help"), HighLogic.Skin.button))//"Help"
          {
            showMoveHelp = !showMoveHelp;
          }
        }
      }
      else
      {
        GUIStyle centerLabelStyle = new GUIStyle(HighLogic.Skin.label);
        centerLabelStyle.alignment = TextAnchor.UpperCenter;
        GUI.Label(LineRect(ref line), Localizer.Format("#VesselMover_Msg"), centerLabelStyle);//"You need to be landed to use this!"
      }

      toolbarRect.height = (line * toolbarLineHeight) + (toolbarMargin * 2);
      GUI.DragWindow(new Rect(0, 0, Screen.width, 30));
      VMUtils.RepositionWindow(ref toolbarRect);
    }

    private void CrewSelectionWindow(int windowID)
    {
      KerbalRoster kerbalRoster = HighLogic.CurrentGame.CrewRoster;
      GUILayout.BeginVertical();
      _displayViewerPosition = GUILayout.BeginScrollView(_displayViewerPosition, GUI.skin.box, GUILayout.Height(250), GUILayout.Width(280));
      IEnumerator<ProtoCrewMember> kerbals = kerbalRoster.Kerbals(ProtoCrewMember.RosterStatus.Available).GetEnumerator();
      while (kerbals.MoveNext())
      {
        ProtoCrewMember crewMember = kerbals.Current;
        if (crewMember == null) continue;
        bool selected = SelectedCrewMembers.Contains(crewMember);
        GUIStyle buttonStyle = selected ? ButtonToggledStyle : HighLogic.Skin.button;
        selected = GUILayout.Toggle(selected,  $"{crewMember.name}, {crewMember.gender}, {crewMember.trait}", buttonStyle);
        if (selected && !SelectedCrewMembers.Contains(crewMember))
        {
          SelectedCrewMembers.Clear();
          SelectedCrewMembers.Add(crewMember);
        }
        else if (!selected && SelectedCrewMembers.Contains(crewMember))
        {
          SelectedCrewMembers.Clear();
        }
      }
      kerbals.Dispose();
      GUILayout.EndScrollView();
      GUILayout.Space(20);
      if (GUILayout.Button(Localizer.Format("#VesselMover_Select"), HighLogic.Skin.button))//"Select"
      {
        VesselSpawn.SelectedCrewData = SelectedCrewMembers;
        VesselSpawn.IsSelectingCrew = false;
        VesselSpawn.IsCrewSelected = true;
      }
      GUILayout.EndVertical();
    }

    private Rect LineRect(ref float currentLine, float heightFactor = 1)
    {
      Rect rect = new Rect(toolbarMargin, toolbarMargin + (currentLine * toolbarLineHeight), contentWidth, toolbarLineHeight * heightFactor);
      currentLine += heightFactor + 0.1f;
      return rect;
    }

    private void MoveHelp(int windowID)
    {
      float line = 0;
      line += 1.25f;
      LineLabel(Localizer.Format("#VesselMover_Movement") +" " + GameSettings.PITCH_DOWN.primary.ToString() + " " +//"Movement: "
        GameSettings.PITCH_UP.primary.ToString() + " " +
        GameSettings.YAW_LEFT.primary.ToString() + " " +
        GameSettings.YAW_RIGHT.primary.ToString(), ref line);
      LineLabel(Localizer.Format("#VesselMover_Roll") + " " + GameSettings.ROLL_LEFT.primary.ToString() + " " +//"Roll: "
        GameSettings.ROLL_RIGHT.primary.ToString(), ref line);
      LineLabel(Localizer.Format("#VesselMover_Pitch") + " " + GameSettings.TRANSLATE_DOWN.primary.ToString() + " " +//"Pitch: "
        GameSettings.TRANSLATE_UP.primary.ToString(), ref line);
      LineLabel(Localizer.Format("#VesselMover_Yaw") + " " + GameSettings.TRANSLATE_LEFT.primary.ToString() + " " +//"Yaw: "
        GameSettings.TRANSLATE_RIGHT.primary.ToString(), ref line);
      LineLabel(Localizer.Format("#VesselMover_AutoRotateR") + " " + GameSettings.TRANSLATE_BACK.primary.ToString(), ref line);//"Auto rotate rocket: "
      LineLabel(Localizer.Format("#VesselMover_AutoRotateP") + " " + GameSettings.TRANSLATE_FWD.primary.ToString(), ref line);//"Auto rotate plane: "
      LineLabel(Localizer.Format("#VesselMover_ChangeMovementMode"), ref line);//"Change movement mode: Tab"
      LineLabel(Localizer.Format("#VesselMover_ResetAltitude") + " " + GameSettings.THROTTLE_CUTOFF.primary.ToString(), ref line);//"Reset Altitude: "
      LineLabel(Localizer.Format("#VesselMover_AdjustAltitude") + " " + GameSettings.THROTTLE_UP.primary.ToString() + " " +//"Adjust Altitude: "
        GameSettings.THROTTLE_DOWN.primary.ToString(), ref line);

      helpHeight = (line * toolbarLineHeight) + (toolbarMargin * 2);
    }

    private void LineLabel(string label, ref float line)
    {
      GUI.Label(LineRect(ref line), label, HighLogic.Skin.label);
    }

    private void AddToolbarButton()
    {
      if (HighLogic.LoadedSceneIsFlight)
      {
        if (!hasAddedButton)
        {
          Texture buttonTexture = GameDatabase.Instance.GetTexture("VesselMover/Textures/icon", false);
          ApplicationLauncher.Instance.AddModApplication(ShowToolbarGUI, HideToolbarGUI, Dummy, Dummy, Dummy, Dummy, ApplicationLauncher.AppScenes.FLIGHT, buttonTexture);
          hasAddedButton = true;
        }
      }
    }

    public void ShowToolbarGUI()
    {
      VesselMoverToolbar.toolbarGuiEnabled = true;
    }

    public void HideToolbarGUI()
    {
      VesselMoverToolbar.toolbarGuiEnabled = false;
    }

    private void Dummy()
    { }

    public static bool MouseIsInRect(Rect rect)
    {
      return rect.Contains(MouseGUIPos());
    }

    public static Vector2 MouseGUIPos()
    {
      return new Vector3(Input.mousePosition.x, Screen.height - Input.mousePosition.y, 0);
    }

    public void OnHideUI()
    {
      ShowUI = false;
    }
    public void OnShowUI()
    {
      ShowUI = true;
    }

    internal static void ShowToolTip(Vector2 toolTipPos, string toolTip)
    {
      Vector2 size = ToolTipStyle.CalcSize(new GUIContent(toolTip));
      Position = new Rect(toolTipPos.x + 5, toolTipPos.y + 5, size.x, size.y);
      RepositionToolTip();
      GUI.Window(0, Position, EmptyWindow, toolTip, ToolTipStyle);
      GUI.BringWindowToFront(0);
    }

    internal void SetGuiStyles()
    {
      ButtonToggledStyle = new GUIStyle(HighLogic.Skin.button);
      ButtonToggledStyle.normal.background = ButtonToggledStyle.onActive.background;
      ToolTipStyle = new GUIStyle(GUI.skin.textArea)
      {
        border = new RectOffset(4, 4, 4, 4),
        padding = new RectOffset(5, 5, 5, 5),
        alignment = TextAnchor.MiddleLeft,
        fontStyle = FontStyle.Italic,
        wordWrap = false,
        normal = { textColor = Color.green },
        hover = { textColor = Color.green }
      };
      ToolTipStyle.hover.background = ToolTipStyle.normal.background;
    }

    private static void RepositionToolTip()
    {
      if (Position.xMax > Screen.width)
        Position.x = Screen.width - Position.width;
      if (Position.yMax > Screen.height)
        Position.y = Screen.height - Position.height;
    }

    private static void EmptyWindow(int windowId)
    {
    }
  }
}

