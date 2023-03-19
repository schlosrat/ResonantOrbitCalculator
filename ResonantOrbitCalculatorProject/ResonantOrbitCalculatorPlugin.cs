using BepInEx;
using HarmonyLib;
using KSP.Game;
using KSP.Sim.impl;
using KSP.Sim.Maneuver;
using KSP.Sim.DeltaV;
using KSP.Sim;
using KSP.UI.Binding;
using SpaceWarp;
using SpaceWarp.API.Assets;
using SpaceWarp.API.Mods;
using SpaceWarp.API.Game;
using SpaceWarp.API.Game.Extensions;
using SpaceWarp.API.UI;
using SpaceWarp.API.UI.Appbar;
using UnityEngine;
using static KSP.Rendering.Planets.PQSData;

namespace ResonantOrbitCalculator;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency(SpaceWarpPlugin.ModGuid, SpaceWarpPlugin.ModVer)]
public class ResonantOrbitCalculatorPlugin : BaseSpaceWarpPlugin
{
    // These are useful in case some other mod wants to add a dependency to this one
    public const string ModGuid = MyPluginInfo.PLUGIN_GUID;
    public const string ModName = MyPluginInfo.PLUGIN_NAME;
    public const string ModVer = MyPluginInfo.PLUGIN_VERSION;
    
    // private bool _isWindowOpen;
    // private Rect _windowRect;

    private const string ToolbarFlightButtonID = "BTN-ResonantOrbitCalculatorFlight";
    // private const string ToolbarOABButtonID = "BTN-ResonantOrbitCalculatorOAB";

    public static ResonantOrbitCalculatorPlugin Instance { get; set; }

    // Begin MicroEngineer Hijack
    private bool showGUI = false;

    private readonly int windowWidth = 290;
    private readonly int windowHeight = 700;
    public Rect mainGuiRect, settingsGuiRect, parGuiRect, orbGuiRect, surGuiRect, fltGuiRect, manGuiRect, tgtGuiRect, stgGuiRect;
    private Rect closeBtnRect;

    private GUISkin _spaceWarpUISkin;
    // private GUIStyle popoutBtnStyle;
    private GUIStyle mainWindowStyle;
    // private GUIStyle popoutWindowStyle;
    private GUIStyle sectionToggleStyle;
    private GUIStyle closeBtnStyle;
    // private GUIStyle saveLoadBtnStyle;
    // private GUIStyle loadBtnStyle;
    private GUIStyle nameLabelStyle;
    private GUIStyle valueLabelStyle;
    private GUIStyle unitLabelStyle;
    // private GUIStyle tableHeaderLabelStyle;

    private string unitColorHex;

    private int spacingAfterHeader = -12;
    private int spacingAfterEntry = -12;
    private int spacingAfterSection = 5;
    private float spacingBelowPopout = 10;

    // public bool showSettings = false;
    public bool diveOrbit = true;
    // public bool showOrb = true;
    // public bool showSur = true;
    // public bool showFlt = false;
    public bool showMan = true;
    // public bool showTgt = false;
    public bool occlusionModifiers = true;

    public bool popoutSettings, popoutPar, popoutOrb, popoutSur, popoutMan, popoutTgt, popoutFlt, popoutStg;

    private VesselComponent activeVessel;
    private SimulationObjectModel currentTarget;
    private ManeuverNodeData currentManeuver;

    // End MicroEngineer Hijack

    /// <summary>
    /// Runs when the mod is first initialized.
    /// </summary>
    public override void OnInitialized()
    {
        base.OnInitialized();

        Instance = this;

        // Begin Hijack from MicroEngineer
        _spaceWarpUISkin = Skins.ConsoleSkin;

        mainWindowStyle = new GUIStyle(_spaceWarpUISkin.window)
        {
            padding = new RectOffset(8, 8, 20, 8),
            contentOffset = new Vector2(0, -22),
            fixedWidth = windowWidth
        };

        //popoutWindowStyle = new GUIStyle(mainWindowStyle)
        //{
        //    padding = new RectOffset(mainWindowStyle.padding.left, mainWindowStyle.padding.right, 0, mainWindowStyle.padding.bottom - 5),
        //    fixedWidth = windowWidth
        //};

        //popoutBtnStyle = new GUIStyle(_spaceWarpUISkin.button)
        //{
        //    alignment = TextAnchor.MiddleCenter,
        //    contentOffset = new Vector2(0, 2),
        //    fixedHeight = 15,
        //    fixedWidth = 15,
        //    fontSize = 28,
        //    clipping = TextClipping.Overflow,
        //    margin = new RectOffset(0, 0, 10, 0)
        //};

        sectionToggleStyle = new GUIStyle(_spaceWarpUISkin.toggle)
        {
            padding = new RectOffset(14, 0, 3, 3)
        };

        nameLabelStyle = new GUIStyle(_spaceWarpUISkin.label);
        nameLabelStyle.normal.textColor = new Color(.7f, .75f, .75f, 1);

        valueLabelStyle = new GUIStyle(_spaceWarpUISkin.label)
        {
            alignment = TextAnchor.MiddleRight
        };
        valueLabelStyle.normal.textColor = new Color(.6f, .7f, 1, 1);

        unitLabelStyle = new GUIStyle(valueLabelStyle)
        {
            fixedWidth = 24,
            alignment = TextAnchor.MiddleLeft
        };
        unitLabelStyle.normal.textColor = new Color(.7f, .75f, .75f, 1);

        unitColorHex = ColorUtility.ToHtmlStringRGBA(unitLabelStyle.normal.textColor);

        closeBtnStyle = new GUIStyle(_spaceWarpUISkin.button)
        {
            fontSize = 8
        };

        //saveLoadBtnStyle = new GUIStyle(_spaceWarpUISkin.button)
        //{
        //    alignment = TextAnchor.MiddleCenter
        //};

        closeBtnRect = new Rect(windowWidth - 23, 6, 16, 16);

        // tableHeaderLabelStyle = new GUIStyle(nameLabelStyle) { alignment = TextAnchor.MiddleRight };

        // Register Flight AppBar button (from munix template)
        Appbar.RegisterAppButton(
            "Resonant Orbit Calc",
            ToolbarFlightButtonID,
            AssetManager.GetAsset<Texture2D>($"{SpaceWarpMetadata.ModID}/images/icon.png"),
            // Toggle the GUI the munix way
            //isOpen =>
            //{
            //    _isWindowOpen = isOpen;
            //    GameObject.Find(ToolbarFlightButtonID)?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(isOpen);
            //}
            // Toggle the GUI the MicroEngineer way
            delegate { showGUI = !showGUI; }
        );

        //// Register OAB AppBar Button (from munix template - not needed in this mod)
        //Appbar.RegisterOABAppButton(
        //    "Resonant Orbit Calculator",
        //    ToolbarOABButtonID,
        //    AssetManager.GetAsset<Texture2D>($"{SpaceWarpMetadata.ModID}/images/icon.png"),
        //    isOpen =>
        //    {
        //        _isWindowOpen = isOpen;
        //        GameObject.Find(ToolbarOABButtonID)?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(isOpen);
        //    }
        //);


        InitializeRects();
        ResetLayout();
        // load window positions and states from disk, if file exists
        // LoadLayoutState();

        // End Hijack from MicroEngineer

        // Register all Harmony patches in the project
        Harmony.CreateAndPatchAll(typeof(ResonantOrbitCalculatorPlugin).Assembly);

        // Boilerplate template stuff from munix - remove once we're certain the basic app functions!
        // Try to get the currently active vessel, set its throttle to 100% and toggle on the landing gear
        //try
        //{
        //    var currentVessel = Vehicle.ActiveVesselVehicle;
        //    if (currentVessel != null)
        //    {
        //        // currentVessel.SetMainThrottle(1.0f);
        //        currentVessel.SetGearState(true);
        //    }
        //}
        //catch (Exception e) {}
        
        // Put MicroEngineer-like layout state info into the configuration values?
        // Fetch a configuration value or create a default one if it does not exist
        var defaultValue = "my_value";
        var configValue = Config.Bind<string>("Settings section", "Option 1", defaultValue, "Option description");
        
        // Log the config value into <KSP2 Root>/BepInEx/LogOutput.log
        Logger.LogInfo($"Option 1: {configValue.Value}");
    }

    private void InitializeRects() // Hijacked from MicroEngineer
    {
        mainGuiRect = settingsGuiRect = parGuiRect = orbGuiRect = surGuiRect = fltGuiRect = manGuiRect = tgtGuiRect = stgGuiRect = new();
    }

    private void ResetLayout() // Hijacked from MicroEngineer
    {
        popoutPar = popoutStg = popoutOrb = popoutSur = popoutFlt = popoutTgt = popoutMan = popoutSettings = false;
        mainGuiRect.position = new(Screen.width * 0.8f, Screen.height * 0.2f);
        Vector2 popoutWindowPosition = new(Screen.width * 0.6f, Screen.height * 0.2f);
        parGuiRect.position = popoutWindowPosition;
        // stgGuiRect.position = popoutWindowPosition;
        orbGuiRect.position = popoutWindowPosition;
        // surGuiRect.position = popoutWindowPosition;
        // fltGuiRect.position = popoutWindowPosition;
        // tgtGuiRect.position = popoutWindowPosition;
        manGuiRect.position = popoutWindowPosition;
        // settingsGuiRect.position = popoutWindowPosition;
    }

    /// <summary>
    /// Draws a simple UI window when <code>this._isWindowOpen</code> is set to <code>true</code>.
    /// </summary>
    private void OnGUI() // Adapted from MicroEngineer
    {
        //// Set the UI
        //GUI.skin = Skins.ConsoleSkin;

        //if (_isWindowOpen)
        //{
        //    _windowRect = GUILayout.Window(
        //        GUIUtility.GetControlID(FocusType.Passive),
        //        _windowRect,
        //        FillWindow,
        //        "Resonant Orbit Calculator",
        //        GUILayout.Height(350),
        //        GUILayout.Width(350)
        //    );
        //}

        // begin MicroEngineer Hijack
        activeVessel = GameManager.Instance?.Game?.ViewController?.GetActiveVehicle(true)?.GetSimVessel(true);
        if (!showGUI || activeVessel == null) return;

        currentTarget = activeVessel.TargetObject;
        currentManeuver = GameManager.Instance?.Game?.SpaceSimulation.Maneuvers.GetNodesForVessel(activeVessel.GlobalId).FirstOrDefault();
        GUI.skin = _spaceWarpUISkin;

        mainGuiRect = GUILayout.Window(
            GUIUtility.GetControlID(FocusType.Passive),
            mainGuiRect,
            FillMainGUI,
            "<color=#696DFF>Resonant Orbit Calc</color>",
            mainWindowStyle,
            GUILayout.Height(0)
        );
        mainGuiRect.position = ClampToScreen(mainGuiRect.position, mainGuiRect.size);

        // NOTE: Do Not Need Popouts for ROC
        //if (showSettings && popoutSettings)
        //{
        //    DrawPopoutWindow(ref settingsGuiRect, FillSettings);
        //}

        //if (diveOrbit && popoutPar)
        //{
        //    DrawPopoutWindow(ref parGuiRect, FillParameters);
        //}

        //if (showOrb && popoutOrb)
        //{
        //    DrawPopoutWindow(ref orbGuiRect, FillCurrentOrbit);
        //}

        //if (showSur && popoutSur)
        //{
        //    DrawPopoutWindow(ref surGuiRect, FillNewOrbit);
        //}

        //if (showFlt && popoutFlt)
        //{
        //    DrawPopoutWindow(ref fltGuiRect, FillFlight);
        //}

        //if (showTgt && popoutTgt && currentTarget != null)
        //{
        //    DrawPopoutWindow(ref tgtGuiRect, FillTarget);
        //}

        //if (showMan && popoutMan && currentManeuver != null)
        //{
        //    DrawPopoutWindow(ref manGuiRect, FillManeuver);
        //}

        //if (occlusionModifiers && popoutStg)
        //{
        //    DrawPopoutWindow(ref stgGuiRect, FillStages);
        //}
        // End Microengineer Hijack
    }

    //private void DrawPopoutWindow(ref Rect guiRect, UnityEngine.GUI.WindowFunction fillAction)
    //{
    //    guiRect = GUILayout.Window(
    //        GUIUtility.GetControlID(FocusType.Passive),
    //        guiRect,
    //        fillAction,
    //        "",
    //        popoutWindowStyle,
    //        GUILayout.Height(0),
    //        GUILayout.Width(windowWidth)
    //    );
    //    guiRect.position = ClampToScreen(guiRect.position, guiRect.size);
    //}

    private Vector2 ClampToScreen(Vector2 position, Vector2 size)
    {
        float x = Mathf.Clamp(position.x, 0, Screen.width - size.x);
        float y = Mathf.Clamp(position.y, 0, Screen.height - size.y);
        return new Vector2(x, y);
    }

    private void FillMainGUI(int windowID)
    {
        if (CloseButton())
        {
            CloseWindow();
        }

        GUILayout.Space(10);

        // NOTE: Repurpose toggle buttons for ROC controls
        GUILayout.BeginHorizontal();
        diveOrbit = GUILayout.Toggle(diveOrbit, "<b>Dive</b>", sectionToggleStyle);
        GUILayout.Space(26);
        occlusionModifiers = GUILayout.Toggle(occlusionModifiers, "<b>Occlusion</b>", sectionToggleStyle);
        GUILayout.Space(26);
        //showOrb = GUILayout.Toggle(showOrb, "<b>ORB</b>", sectionToggleStyle);
        //GUILayout.Space(26);
        //showSur = GUILayout.Toggle(showSur, "<b>SUR</b>", sectionToggleStyle);
        //GUILayout.Space(26);
        //showFlt = GUILayout.Toggle(showFlt, "<b>FLT</b>", sectionToggleStyle);
        //GUILayout.Space(26);
        //showTgt = GUILayout.Toggle(showTgt, "<b>TGT</b>", sectionToggleStyle);
        GUILayout.EndHorizontal();

        //GUILayout.BeginHorizontal();
        //showMan = GUILayout.Toggle(showMan, "<b>MAN</b>", sectionToggleStyle);
        //GUILayout.Space(26);
        //showSettings = GUILayout.Toggle(showSettings, "<b>SET</b>", sectionToggleStyle);
        //GUILayout.EndHorizontal();


        GUILayout.Space(-3);

        GUILayout.BeginHorizontal();
        GUILayout.EndHorizontal();

        // NOTE: Do not need conditional logic for most/all of ROC - just show stuff!
        //if (showSettings && !popoutSettings)
        //{
        //    FillSettings();
        //}

        //if (diveOrbit && !popoutPar)
        //{
            FillParameters();
        //}

        //if (occlusionModifiers && !popoutStg)
        //{
        //    FillStages();
        //}

        //if (showOrb && !popoutOrb)
        //{
            FillCurrentOrbit();
        //}

        //if (showSur && !popoutSur)
        //{
            FillNewOrbit();
        //}

        //if (showFlt && !popoutFlt)
        //{
        //    FillFlight();
        //}

        //if (showTgt && !popoutTgt && currentTarget != null)
        //{
        //    FillTarget();
        //}

        if (showMan && currentManeuver != null)
        {
            FillManeuver();
        }

        GUI.DragWindow(new Rect(0, 0, windowWidth, windowHeight));
    }

    //private void FillSettings(int _ = 0)
    //{
    //    DrawSectionHeader("Settings", ref popoutSettings);

    //    GUILayout.Space(10);
    //    GUILayout.BeginHorizontal();
    //    if (GUILayout.Button("SAVE LAYOUT", saveLoadBtnStyle))
    //        SaveLayoutState();
    //    GUILayout.Space(5);
    //    if (GUILayout.Button("LOAD LAYOUT", saveLoadBtnStyle))
    //        LoadLayoutState();
    //    GUILayout.Space(5);
    //    if (GUILayout.Button("RESET", saveLoadBtnStyle))
    //        ResetLayout();
    //    GUILayout.EndHorizontal();

    //    DrawSectionEnd(popoutSettings);
    //}

    private void FillParameters(int _ = 0)
    {
        DrawSectionHeader("Vessel", ref popoutPar, activeVessel.DisplayName);

        // DrawEntry("Mass", $"{activeVessel.totalMass * 1000:N0}", "kg");
        DrawEntry("Celestial Body", activeVessel.mainBody.bodyName);
        DrawEntry("Situation", SituationToString(activeVessel.Situation));
        DrawEntry("Payloads", "3", "");
        DrawEntry("Altitude", "600000", "m");
        DrawEntry("Period", "600000", "s");
        DrawEntry("Synchronous", "600000", "m");
        DrawEntry("Min LOS Orbit", "600000", "m");
        DrawEntry("Occlusion Modifiers", occlusionModifiers.ToString());
        if (occlusionModifiers)
        {
            DrawEntry("Atm", "0.75");
            DrawEntry("Vac", "0.9");
        }
        //VesselDeltaVComponent deltaVComponent = activeVessel.VesselDeltaV;
        //if (deltaVComponent != null)
        //{
        //    DrawEntry("∆v", $"{deltaVComponent.TotalDeltaVActual:N0}", "m/s");
        //    if (deltaVComponent.StageInfo.FirstOrDefault()?.DeltaVinVac > 0.0001 || deltaVComponent.StageInfo.FirstOrDefault()?.DeltaVatASL > 0.0001)
        //    {
        //        DrawEntry("Thrust", $"{deltaVComponent.StageInfo.FirstOrDefault()?.ThrustActual * 1000:N0}", "N");
        //        DrawEntry("TWR", $"{deltaVComponent.StageInfo.FirstOrDefault()?.TWRActual:N2}");
        //    }
        //}

        DrawSectionEnd(popoutPar);
    }

    //private void FillStages(int _ = 0)
    //{
    //    DrawStagesHeader(ref popoutStg);

    //    List<DeltaVStageInfo> stages = activeVessel.VesselDeltaV?.StageInfo;

    //    int stageCount = stages?.Count ?? 0;
    //    if (stages != null && stageCount > 0)
    //    {
    //        float highestTwr = Mathf.Floor(stages.Max(stage => stage.TWRActual));
    //        int preDecimalDigits = Mathf.FloorToInt(Mathf.Log10(highestTwr)) + 1;
    //        string twrFormatString = "N2";

    //        if (preDecimalDigits == 3)
    //        {
    //            twrFormatString = "N1";
    //        }
    //        else if (preDecimalDigits == 4)
    //        {
    //            twrFormatString = "N0";
    //        }

    //        for (int i = stages.Count - 1; i >= 0; i--)
    //        {

    //            DeltaVStageInfo stageInfo = stages[i];
    //            if (stageInfo.DeltaVinVac > 0.0001 || stageInfo.DeltaVatASL > 0.0001)
    //            {
    //                int stageNum = stageCount - stageInfo.Stage;
    //                DrawStageEntry(stageNum, stageInfo, twrFormatString);
    //            }
    //        }
    //    }

    //    DrawSectionEnd(popoutStg);
    //}

    private void FillCurrentOrbit(int _ = 0)
    {
        DrawSectionHeader("Orbital", ref popoutOrb);

        DrawEntry("Dive Orbit", diveOrbit.ToString());
        DrawEntry("Period", $"{SecondsToTimeString(activeVessel.Orbit.period)}", "s");
        DrawEntry("Apoapsis", $"{MetersToDistanceString(activeVessel.Orbit.ApoapsisArl)}", "m");
        DrawEntry("Periapsis", $"{MetersToDistanceString(activeVessel.Orbit.PeriapsisArl)}", "m");
        DrawEntry("Semi-Major Axis", $"{MetersToDistanceString(activeVessel.Orbit.SemimajorAxis)}", "m");
        DrawEntry("Time to Ap.", $"{SecondsToTimeString((activeVessel.Situation == VesselSituations.Landed || activeVessel.Situation == VesselSituations.PreLaunch) ? 0f : activeVessel.Orbit.TimeToAp)}", "s");
        DrawEntry("Time to Pe.", $"{SecondsToTimeString(activeVessel.Orbit.TimeToPe)}", "s");
        DrawEntry("Inclination", $"{activeVessel.Orbit.inclination:N3}", "°");
        DrawEntry("Eccentricity", $"{activeVessel.Orbit.eccentricity:N3}");
        //double secondsToSoiTransition = activeVessel.Orbit.UniversalTimeAtSoiEncounter - GameManager.Instance.Game.UniverseModel.UniversalTime;
        //if (secondsToSoiTransition >= 0)
        //{
        //    DrawEntry("SOI Trans.", SecondsToTimeString(secondsToSoiTransition), "s");
        //}
        DrawSectionEnd(popoutOrb);
    }

    private void FillNewOrbit(int _ = 0)
    {
        DrawSectionHeader("Surface", ref popoutSur, activeVessel.mainBody.bodyName);

        DrawEntry("Situation", SituationToString(activeVessel.Situation));
        DrawEntry("Latitude", $"{DegreesToDMS(activeVessel.Latitude)}", activeVessel.Latitude < 0 ? "S" : "N");
        DrawEntry("Longitude", $"{DegreesToDMS(activeVessel.Longitude)}", activeVessel.Longitude < 0 ? "W" : "E");
        DrawEntry("Biome", BiomeToString(activeVessel.SimulationObject.Telemetry.SurfaceBiome));
        DrawEntry("Alt. MSL", MetersToDistanceString(activeVessel.AltitudeFromSeaLevel), "m");
        DrawEntry("Alt. AGL", MetersToDistanceString(activeVessel.AltitudeFromScenery), "m");
        DrawEntry("Horizontal Vel.", $"{activeVessel.HorizontalSrfSpeed:N1}", "m/s");
        DrawEntry("Vertical Vel.", $"{activeVessel.VerticalSrfSpeed:N1}", "m/s");

        DrawSectionEnd(popoutSur);
    }

    //private void FillFlight(int _ = 0)
    //{
    //    DrawSectionHeader("Flight", ref popoutFlt);

    //    DrawEntry("Speed", $"{activeVessel.SurfaceVelocity.magnitude:N1}", "m/s");
    //    DrawEntry("Mach Number", $"{activeVessel.SimulationObject.Telemetry.MachNumber:N2}");
    //    DrawEntry("Atm. Density", $"{activeVessel.SimulationObject.Telemetry.AtmosphericDensity:N3}", "g/L");
    //    GetAeroStats();

    //    DrawEntry("Total Lift", $"{totalLift * 1000:N0}", "N");
    //    DrawEntry("Total Drag", $"{totalDrag * 1000:N0}", "N");

    //    DrawEntry("Lift / Drag", $"{totalLift / totalDrag:N3}");

    //    DrawSectionEnd(popoutFlt);
    //}

    //private void FillTarget(int _ = 0)
    //{
    //    DrawSectionHeader("Target", ref popoutTgt, currentTarget.DisplayName);

    //    if (currentTarget.Orbit != null)
    //    {
    //        DrawEntry("Target Ap.", MetersToDistanceString(currentTarget.Orbit.ApoapsisArl), "m");
    //        DrawEntry("Target Pe.", MetersToDistanceString(currentTarget.Orbit.PeriapsisArl), "m");

    //        if (activeVessel.Orbit.referenceBody == currentTarget.Orbit.referenceBody)
    //        {
    //            double distanceToTarget = (activeVessel.Orbit.Position - currentTarget.Orbit.Position).magnitude;
    //            DrawEntry("Distance", MetersToDistanceString(distanceToTarget), "m");
    //            double relativeVelocity = (activeVessel.Orbit.relativeVelocity - currentTarget.Orbit.relativeVelocity).magnitude;
    //            DrawEntry("Rel. Speed", $"{relativeVelocity:N1}", "m/s");
    //            OrbitTargeter targeter = activeVessel.Orbiter.OrbitTargeter;
    //            DrawEntry("Rel. Incl.", $"{targeter.AscendingNodeTarget.Inclination:N3}", "°");
    //        }
    //    }
    //    DrawSectionEnd(popoutTgt);
    //}

    private void FillManeuver(int _ = 0)
    {
        DrawSectionHeader("Maneuver", ref popoutMan);
        PatchedConicsOrbit newOrbit = activeVessel.Orbiter.ManeuverPlanSolver.PatchedConicsList.FirstOrDefault();
        DrawEntry("Projected Ap.", MetersToDistanceString(newOrbit.ApoapsisArl), "m");
        DrawEntry("Projected Pe.", MetersToDistanceString(newOrbit.PeriapsisArl), "m");
        DrawEntry("∆v required", $"{currentManeuver.BurnRequiredDV:N1}", "m/s");
        double timeUntilNode = currentManeuver.Time - GameManager.Instance.Game.UniverseModel.UniversalTime;
        DrawEntry("Time to", SecondsToTimeString(timeUntilNode), "s");
        DrawEntry("Burn Time", SecondsToTimeString(currentManeuver.BurnDuration), "s");

        DrawSectionEnd(popoutMan);
    }

    private void DrawSectionHeader(string sectionName, ref bool isPopout, string value = "")
    {
        GUILayout.BeginHorizontal();
        // Don't need popout buttons for ROC
        // isPopout = isPopout ? !CloseButton() : GUILayout.Button("⇖", popoutBtnStyle);

        GUILayout.Label($"<b>{sectionName}</b>");
        GUILayout.FlexibleSpace();
        GUILayout.Label(value, valueLabelStyle);
        GUILayout.Space(5);
        GUILayout.Label("", unitLabelStyle);
        GUILayout.EndHorizontal();
        GUILayout.Space(spacingAfterHeader);
    }

    private void DrawEntry(string entryName, string value, string unit = "")
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(entryName, nameLabelStyle);
        GUILayout.FlexibleSpace();
        GUILayout.Label(value, valueLabelStyle);
        GUILayout.Space(5);
        GUILayout.Label(unit, unitLabelStyle);
        GUILayout.EndHorizontal();
        GUILayout.Space(spacingAfterEntry);
    }

    private void DrawSectionEnd(bool isPopout)
    {
        if (isPopout)
        {
            GUI.DragWindow(new Rect(0, 0, windowWidth, windowHeight));
            GUILayout.Space(spacingBelowPopout);
        }
        else
        {
            GUILayout.Space(spacingAfterSection);
        }
    }

    private bool CloseButton()
    {
        return GUI.Button(closeBtnRect, "x", closeBtnStyle);
    }

    private string SituationToString(VesselSituations situation)
    {
        return situation switch
        {
            VesselSituations.PreLaunch => "Pre-Launch",
            VesselSituations.Landed => "Landed",
            VesselSituations.Splashed => "Splashed down",
            VesselSituations.Flying => "Flying",
            VesselSituations.SubOrbital => "Suborbital",
            VesselSituations.Orbiting => "Orbiting",
            VesselSituations.Escaping => "Escaping",
            _ => "UNKNOWN",
        };
    }

    private string SecondsToTimeString(double seconds, bool addSpacing = true)
    {
        if (seconds == Double.PositiveInfinity)
        {
            return "∞";
        }
        else if (seconds == Double.NegativeInfinity)
        {
            return "-∞";
        }

        seconds = Math.Ceiling(seconds);

        string result = "";
        string spacing = "";
        if (addSpacing)
        {
            spacing = " ";
        }

        if (seconds < 0)
        {
            result += "-";
            seconds = Math.Abs(seconds);
        }

        int days = (int)(seconds / 21600);
        int hours = (int)((seconds - (days * 21600)) / 3600);
        int minutes = (int)((seconds - (hours * 3600) - (days * 21600)) / 60);
        int secs = (int)(seconds - (days * 21600) - (hours * 3600) - (minutes * 60));

        if (days > 0)
        {
            result += $"{days}{spacing}<color=#{unitColorHex}>d</color> ";
        }

        if (hours > 0 || days > 0)
        {
            {
                result += $"{hours}{spacing}<color=#{unitColorHex}>h</color> ";
            }
        }

        if (minutes > 0 || hours > 0 || days > 0)
        {
            if (hours > 0 || days > 0)
            {
                result += $"{minutes:00.}{spacing}<color=#{unitColorHex}>m</color> ";
            }
            else
            {
                result += $"{minutes}{spacing}<color=#{unitColorHex}>m</color> ";
            }
        }

        if (minutes > 0 || hours > 0 || days > 0)
        {
            result += $"{secs:00.}";
        }
        else
        {
            result += secs;
        }

        return result;
    }

    private string MetersToDistanceString(double heightInMeters)
    {
        return $"{heightInMeters:N0}";
    }

    private string BiomeToString(BiomeSurfaceData biome)
    {
        string result = biome.type.ToString().ToLower().Replace('_', ' ');
        return result.Substring(0, 1).ToUpper() + result.Substring(1);
    }

    private string DegreesToDMS(double degreeD)
    {
        var ts = TimeSpan.FromHours(Math.Abs(degreeD));
        int degrees = (int)Math.Floor(ts.TotalHours);
        int minutes = ts.Minutes;
        int seconds = ts.Seconds;

        string result = $"{degrees:N0}<color={unitColorHex}>°</color> {minutes:00}<color={unitColorHex}>'</color> {seconds:00}<color={unitColorHex}>\"</color>";

        return result;
    }

    private void CloseWindow()
    {
        GameObject.Find("BTN-MicroEngineerBtn")?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(false);
        showGUI = false;
    }

    //private void SaveLayoutState()
    //{
    //    LayoutState state = new(this);
    //    state.Save();
    //}

    //private void LoadLayoutState()
    //{
    //    LayoutState state = LayoutState.Load();

    //    if (state != null)
    //    {
    //        showSettings = false;
    //        diveOrbit = state.ShowVes;
    //        showOrb = state.ShowOrb;
    //        showSur = state.ShowSur;
    //        showFlt = state.ShowFlt;
    //        showMan = state.ShowMan;
    //        showTgt = state.ShowTgt;
    //        occlusionModifiers = state.ShowStg;
    //        popoutSettings = state.IsPopoutSettings;
    //        popoutPar = state.IsPopoutVes;
    //        popoutOrb = state.IsPopoutOrb;
    //        popoutSur = state.IsPopoutSur;
    //        popoutFlt = state.IsPopOutFlt;
    //        popoutMan = state.IsPopOutMan;
    //        popoutTgt = state.IsPopOutTgt;
    //        popoutStg = state.IsPopOutStg;
    //        mainGuiRect.position = state.MainGuiPosition;
    //        settingsGuiRect.position = state.SettingsPosition;
    //        parGuiRect.position = state.VesPosition;
    //        orbGuiRect.position = state.OrbPosition;
    //        surGuiRect.position = state.SurPosition;
    //        fltGuiRect.position = state.FltPosition;
    //        manGuiRect.position = state.ManPosition;
    //        tgtGuiRect.position = state.TgtPosition;
    //        stgGuiRect.position = state.StgPosition;
    //    }
    //}

    /// <summary>
    /// Defines the content of the UI window drawn in the <code>OnGui</code> method.
    /// </summary>
    /// <param name="windowID"></param>
    private static void FillWindow(int windowID)
    {
        GUILayout.Label("Resonant Orbit Calculator");
        GUI.DragWindow(new Rect(0, 0, 10000, 500));
    }
}
