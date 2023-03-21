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
using System;


namespace ResonantOrbitCalculator;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency(SpaceWarpPlugin.ModGuid, SpaceWarpPlugin.ModVer)]
public class ResonantOrbitCalculatorPlugin : BaseSpaceWarpPlugin
{
    // These are useful in case some other mod wants to add a dependency to this one
    public const string ModGuid = MyPluginInfo.PLUGIN_GUID;   // munix template
    public const string ModName = MyPluginInfo.PLUGIN_NAME;   // munix template
    public const string ModVer = MyPluginInfo.PLUGIN_VERSION; // munix template

    // private bool _isWindowOpen; // munix template
    // private Rect _windowRect;   // munix template

    private const string ToolbarFlightButtonID = "BTN-ResonantOrbitCalculatorFlight";
    // private const string ToolbarOABButtonID = "BTN-ResonantOrbitCalculatorOAB"; // munix template

    public static ResonantOrbitCalculatorPlugin Instance { get; set; } // munix template

    // Local variables (should these be at this level?)
    private string numSatellites = "3";         // String number of satellites to deploy (>= 2)
    private int numSats = 3;                    // Integer number of satellites to deploy (>= 2)
    private string numOrbits = "1";             // String number of resonant orbit passes between deployments (>= 2)
    private int numOrb = 1;                     // Integer number of resonant orbit passes between deployments (>= 1)
    private string resonanceStr;                // String resonant factor relating the deploy orbit and the destiantion orbit
    private double resonance;                   // Double resonant factor relating the deploy orbit and the destiantion orbit 
    private string targetAltitude = "600000";   // String planned altitide for deployed satellites (destiantion orbit)
    private double targetAlt = 600000;          // Double planned altitide for deployed satellites (destiantion orbit)
    private double satPeriod;                   // The period of the destination orbit
    private double xferPeriod;                  // The period of the resonant deploy orbit (xferPeriod = resonance*satPeriod)
    private double Ap2;                         // The resonant deploy orbit apoapsis
    private double Pe2;                         // The resonant deploy orbit periapsis
    private double occModAtm = 0.75;            // Double Occlusion Modifier for Atmosphere
    private double occModVac = 0.9;             // Double Occlusion Modifier for Vacuume
    private string occModAtmStr = "0.75";       // String Occlusion Modifier for Atmosphere
    private string occModVacStr = "0.9";        // String Occlusion Modifier for Vacuume 
    private bool nSatUp, nSatDown, nOrbUp, nOrbDown, setTgtPe, setTgtAp, setTgtSync, setTgtSemiSync, setTgtMinLOS;
    private double synchronousPeriod;           // Syncronous Orbital period about the main body (not sayin its even possible...)
    private double semiSynchronousPeriod;       // Semi-Syncronous Orbital period about the main body (not sayin its even possible...)
    private double synchronousAlt;              // Syncronous Orbital altitude about the main body (not sayin its even possible...)
    private double semiSynchronousAlt;          // Semi-Syncronous Orbital altitude about the main body (not sayin its even possible...)
    private double minLOSAlt;                   // Minimum LOS Orbit Altitude (only defined if 3 or more satellites in constelation)

    // Hijack from VesselRenamer
    //private bool gameInputState = true;
    //public readonly string resonantOrbitCalculatorInput = "ResonantOrbitCalculator.Input";

    // Begin MicroEngineer Hijack
    private bool showGUI = false;

    private readonly int windowWidth = 320;
    private readonly int windowHeight = 700;
    public Rect mainGuiRect, settingsGuiRect, parGuiRect, orbGuiRect, surGuiRect, fltGuiRect, manGuiRect, tgtGuiRect, stgGuiRect;
    private Rect closeBtnRect;

    private GUISkin _spaceWarpUISkin;
    private GUIStyle ctrlBtnStyle;
    private GUIStyle mainWindowStyle;
    private GUIStyle textInputStyle;
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
    // private float spacingBelowPopout = 10;

    public bool diveOrbit = true; // Adapt a show* variable to track if we're doing a diving orbit or not
    public bool occlusionModifiers = true; // Adapt a show* variable to track if we're applying occlusion modifiers or not
    public bool showMan = true; // Adapt a show* variable to track if we've got a maneuver setup for the orbit

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
        // game = GameManager.Instance.Game;

        Instance = this;

        // Begin Hijack from MicroEngineer
        _spaceWarpUISkin = Skins.ConsoleSkin;

        mainWindowStyle = new GUIStyle(_spaceWarpUISkin.window)
        {
            padding = new RectOffset(8, 8, 20, 8),
            contentOffset = new Vector2(0, -22),
            fixedWidth = windowWidth
        };

        textInputStyle = new GUIStyle(_spaceWarpUISkin.textField)
        {
            alignment = TextAnchor.LowerCenter,
            padding = new RectOffset(10, 10, 0, 0),
            contentOffset = new Vector2(0, 2),
            fixedHeight = 18,
            clipping = TextClipping.Overflow,
            margin = new RectOffset(0, 0, 10, 0)
        };

        ctrlBtnStyle = new GUIStyle(_spaceWarpUISkin.button)
        {
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(0, 0, 0, 3),
            contentOffset = new Vector2(0, 2),
            fixedHeight = 16,
            fixedWidth = 16,
            fontSize = 16,
            clipping = TextClipping.Overflow,
            margin = new RectOffset(0, 0, 10, 0)
        };

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
            "Resonant Orbit Calc.",
            ToolbarFlightButtonID,
            AssetManager.GetAsset<Texture2D>($"{SpaceWarpMetadata.ModID}/images/icon.png"),
            // Toggle the GUI the MicroEngineer way
            delegate { showGUI = !showGUI; }
            // Toggle the GUI the munix way
            //isOpen =>
            //{
            //    _isWindowOpen = isOpen;
            //    GameObject.Find(ToolbarFlightButtonID)?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(isOpen);
            //}
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
        orbGuiRect.position = popoutWindowPosition;
        manGuiRect.position = popoutWindowPosition;
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
            "<color=#696DFF>// RESONANT ORBIT CALC</color>",
            mainWindowStyle,
            GUILayout.Height(0)
        );
        mainGuiRect.position = ClampToScreen(mainGuiRect.position, mainGuiRect.size);

        // End Microengineer Hijack

        // Begin VesselRenamer Hijack
        // Prevent inputs to this mod from passing through to the game
        //if (gameInputState && GUI.GetNameOfFocusedControl().Equals(resonantOrbitCalculatorInput))
        //{
        //    gameInputState = false;
        //    GameManager.Instance.Game.Input.Flight.Disable();
        //}

        //// Allow inputs to the game
        //if (!gameInputState && !GUI.GetNameOfFocusedControl().Equals(resonantOrbitCalculatorInput))
        //{
        //    gameInputState = true;
        //    GameManager.Instance.Game.Input.Flight.Enable();
        //}
        // End VesselRenamer Hijack

    }

    private Vector2 ClampToScreen(Vector2 position, Vector2 size)
    {
        float x = Mathf.Clamp(position.x, 0, Screen.width - size.x);
        float y = Mathf.Clamp(position.y, 0, Screen.height - size.y);
        return new Vector2(x, y);
    }

    private double occModCalc(bool hasAtmo)
    {
        double occMod;
        if (occlusionModifiers)
        {
            occModAtm = double.Parse(occModAtmStr);
            occModVac = double.Parse(occModVacStr);
            if (hasAtmo)
            {
                occMod = occModAtm;
            }
            else
            {
                occMod = occModVac;
            }
        }
        else
        {
            occMod = 1;
        }

        return occMod;
    }

    private double minLOSCalc(int numSat, double radius, bool hasAtmo)
    {
        if (numSat > 2)
        {
            return (radius * occModCalc(hasAtmo))/ (Math.Cos(0.5 * (2.0 * Math.PI / numSat))) - radius;
        } else
        {
            return -1;
        }
    }

    private double SMACalc(double period) // Compute SMA given orbital period
    {
        double SMA;
        SMA = Math.Pow((period * Math.Sqrt(activeVessel.mainBody.gravParameter) / (2.0 * Math.PI)), (2.0 / 3.0));
        return SMA;
    }

    private double periodCalc(double SMA) // Compute orbital period given SMA
    {
        double period;
        period = (2.0 * Math.PI * Math.Pow(SMA, 1.5)) / Math.Sqrt(activeVessel.mainBody.gravParameter);
        return period;
    }

    private double burnCalc(double sAp, double sSMA, double se, double cAp, double cSMA, double ce, double bGM)
    {
        double sta = 0;
        double cta = 0;
        if (cAp == sAp) cta = 180;
        double sr = sSMA * (1 - Math.Pow(se, 2)) / (1 + (se * Math.Cos(sta)));
        double sdv = Math.Sqrt(bGM * ((2 / sr) - (1 / sSMA)));

        double cr = cSMA * (1 - Math.Pow(ce, 2)) / (1 + (ce * Math.Cos(cta)));
        double cdv = Math.Sqrt(bGM * ((2 / sr) - (1 / cSMA)));

        return Math.Round(100 * Math.Abs(sdv - cdv)) / 100;
    }

    private void FillMainGUI(int windowID)
    {
        if (CloseButton())
        {
            CloseWindow();
        }

        GUILayout.Space(10);

        // NOTE: Repurpose toggle buttons for ROC controls
        //GUILayout.BeginHorizontal();
        //diveOrbit = GUILayout.Toggle(diveOrbit, "<b>Dive</b>", sectionToggleStyle);
        //GUILayout.Space(40);
        //occlusionModifiers = GUILayout.Toggle(occlusionModifiers, "<b>Occlusion</b>", sectionToggleStyle);
        //// GUILayout.Space(26);
        //GUILayout.EndHorizontal();

        GUILayout.Space(-3);

        GUILayout.BeginHorizontal();
        GUILayout.EndHorizontal();

        FillParameters();
        FillCurrentOrbit();
        FillNewOrbit();

        if (showMan && currentManeuver != null)
        {
            FillManeuver();
        }

        // Begin Hijack from VesselRenamer
        //GUILayout.BeginHorizontal();
        //string inputStateString = gameInputState ? "Enabled" : "Disabled";
        //GUILayout.Label($"Game Input: {inputStateString}");
        //GUILayout.EndHorizontal();

        GUI.DragWindow(new Rect(0, 0, windowWidth, windowHeight));
    }

    private void FillParameters(int _ = 0)
    {
        synchronousPeriod = activeVessel.mainBody.rotationPeriod;
        semiSynchronousPeriod = activeVessel.mainBody.rotationPeriod/2;
        synchronousAlt = SMACalc(synchronousPeriod);
        semiSynchronousAlt = SMACalc(semiSynchronousPeriod);
        int n, m;

        DrawSectionHeader("Carrier Vessel", activeVessel.DisplayName);

        DrawEntry("Situation", String.Format("{0} {1}", SituationToString(activeVessel.Situation), activeVessel.mainBody.bodyName));

        DrawSectionEnd();

        DrawSectionHeader("Mission");

        // SOI related parameters
        // activeVessel.mainBody.HasLocalSpace
        // activeVessel.mainBody.SphereOfInfluenceCalculationType
        // activeVessel.mainBody.minOrbitalDistance
        if (synchronousAlt > activeVessel.mainBody.sphereOfInfluence)
        {
            synchronousAlt = -1;
        }
        if (semiSynchronousAlt > activeVessel.mainBody.sphereOfInfluence)
        {
            semiSynchronousAlt = -1;
        }

        if (diveOrbit) // If we're going to dive under the target orbit for the deployment orbit
        {
            m = numSats * numOrb;
            n = m - 1;
        }
        else // If not
        {
            m = numSats * numOrb;
            n = m + 1;
        }
        resonance = (double)n / m;
        resonanceStr = String.Format("{0}/{1}", n, m);

        // Compute the minimum LOS altitude
        minLOSAlt = minLOSCalc(numSats, activeVessel.mainBody.radius, activeVessel.mainBody.hasAtmosphere);

        DrawEntry2Button("Payloads:", ref nSatDown, "-", ref nSatUp, "+", numSatellites);
        DrawEntry2Button("Deploy Orbits:", ref nOrbDown, "-", ref nOrbUp, "+", numOrbits);
        DrawEntry("Orbital Resonance", resonanceStr);
        // DrawEntry("Resonance val", resonance.ToString());

        DrawEntryTextField("Target Altitude", ref targetAltitude, "m");
        targetAlt = double.Parse(targetAltitude);

        satPeriod = periodCalc(targetAlt + activeVessel.mainBody.radius);
        DrawEntry("Period", $"{SecondsToTimeString(satPeriod)}", "s");
        if (synchronousAlt > 0)
        {
            DrawEntryButton("Synchronous Alt", ref setTgtSync, "⦾", $"{MetersToDistanceString(synchronousAlt)}", "m");
            DrawEntryButton("Semi Synchronous Alt", ref setTgtSemiSync, "⦾", $"{MetersToDistanceString(semiSynchronousAlt)}", "m");
        }
        else if (semiSynchronousAlt > 0)
        {
            DrawEntry("Synchronous Alt", "Outside SOI");
            DrawEntryButton("Semi Synchronous Alt", ref setTgtSemiSync, "⦾", $"{MetersToDistanceString(semiSynchronousAlt)}", "m");
        }
        else
        {
            DrawEntry("Synchronous Alt", "Outside SOI");
            DrawEntry("Semi Synchronous Alt", "Outside SOI");
        }
        DrawEntry("SOI Alt", $"{MetersToDistanceString(activeVessel.mainBody.sphereOfInfluence)}", "m");
        if (minLOSAlt > 0)
        {
            DrawEntryButton("Min LOS Orbit Alt", ref setTgtMinLOS, "⦾", $"{MetersToDistanceString(minLOSAlt)}", "m");
        } else
        {
            DrawEntry("Min LOS Orbit Alt", "Undefined", "m");
        }
        GUILayout.Space(5);
        GUILayout.BeginHorizontal();
        occlusionModifiers = GUILayout.Toggle(occlusionModifiers, "<b>Occlusion</b>", sectionToggleStyle);
        GUILayout.EndHorizontal();
        GUILayout.Space(-5);
        if (occlusionModifiers)
        {
            DrawEntryTextField("Atm", ref occModAtmStr);
            occModAtm = double.Parse(occModAtmStr);
            DrawEntryTextField("Vac", ref occModVacStr);
            occModVac = double.Parse(occModVacStr);
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

        DrawSectionEnd();

        handleButtons();
    }

    private void handleButtons()
    {
        if (nSatDown || nSatUp || nOrbDown || nOrbUp || setTgtPe || setTgtAp || setTgtSync || setTgtSemiSync || setTgtMinLOS)
        {
            // burnParams = Vector3d.zero;
            if (nSatDown && numSats > 2)
            {
                numSats--;
                numSatellites = numSats.ToString();
            }
            else if (nSatUp)
            {
                numSats++;
                numSatellites = numSats.ToString();
            }
            else if (nOrbDown && numOrb > 1)
            {
                numOrb--;
                numOrbits = numOrb.ToString();
            }
            else if (nOrbUp)
            {
                numOrb++;
                numOrbits = numOrb.ToString();
            }
            else if (setTgtPe)
            {
                targetAlt = activeVessel.Orbit.PeriapsisArl;
                targetAltitude = targetAlt.ToString("0");
            }
            else if (setTgtAp)
            {
                targetAlt = activeVessel.Orbit.ApoapsisArl;
                targetAltitude = targetAlt.ToString("0");
            }
            else if (setTgtSync)
            {
                targetAlt = synchronousAlt;
                targetAltitude = targetAlt.ToString("0");
            }
            else if (setTgtSemiSync)
            {
                targetAlt = semiSynchronousAlt;
                targetAltitude = targetAlt.ToString("0");
            }
            else if (setTgtMinLOS)
            {
                targetAlt = minLOSAlt;
                targetAltitude = targetAlt.ToString("0");
            }
        }
    }
    
    private void FillCurrentOrbit(int _ = 0)
    {
        DrawSectionHeader("Current Orbit");

        DrawEntry("Period", $"{SecondsToTimeString(activeVessel.Orbit.period)}", "s");
        DrawEntryButton("Apoapsis", ref setTgtAp, "⦾", $"{MetersToDistanceString(activeVessel.Orbit.ApoapsisArl)}", "m");
        DrawEntryButton("Periapsis", ref setTgtPe, "⦾", $"{MetersToDistanceString(activeVessel.Orbit.PeriapsisArl)}", "m");
        DrawEntry("Time to Ap.", $"{SecondsToTimeString((activeVessel.Situation == VesselSituations.Landed || activeVessel.Situation == VesselSituations.PreLaunch) ? 0f : activeVessel.Orbit.TimeToAp)}", "s");
        DrawEntry("Time to Pe.", $"{SecondsToTimeString(activeVessel.Orbit.TimeToPe)}", "s");
        DrawEntry("Inclination", $"{activeVessel.Orbit.inclination:N3}", "°");
        DrawEntry("Eccentricity", $"{activeVessel.Orbit.eccentricity:N3}");
        double secondsToSoiTransition = activeVessel.Orbit.UniversalTimeAtSoiEncounter - GameManager.Instance.Game.UniverseModel.UniversalTime;
        if (secondsToSoiTransition >= 0)
        {
            DrawEntry("SOI Trans.", SecondsToTimeString(secondsToSoiTransition), "s");
        }
        DrawSectionEnd();
    }

    private void FillNewOrbit(int _ = 0)
    {
        DrawSectionHeader("Deploy Orbit");

        GUILayout.Space(5);
        GUILayout.BeginHorizontal();
        diveOrbit = GUILayout.Toggle(diveOrbit, "<b>Dive</b>", sectionToggleStyle);
        GUILayout.EndHorizontal();
        GUILayout.Space(-5);

        // period1 = periodCalc(targetAlt + activeVessel.mainBody.radius);
        xferPeriod = resonance * satPeriod;
        double SMA2 = SMACalc(xferPeriod);
        double sSMA = targetAlt + activeVessel.mainBody.radius;
        if (diveOrbit)
        {
            Ap2 = sSMA; // Diveing transfer orbits release at Apoapsis
            Pe2 = 2.0 * SMA2 - (Ap2);
        } else
        {
            Pe2 = sSMA; // Non-diving transfer orbits release at Periapsis
            Ap2 = 2.0 * SMA2 - (Pe2);
        }
        double ce = (Ap2 - Pe2)/(Ap2 + Pe2);
        DrawEntry("Period", $"{SecondsToTimeString(xferPeriod)}", "s");
        DrawEntry("Apoapsis", $"{MetersToDistanceString(Ap2 - activeVessel.mainBody.radius)}", "m");
        DrawEntry("Periapsis", $"{MetersToDistanceString(Pe2 - activeVessel.mainBody.radius)}", "m");
        DrawEntry("Eccentricity", ce.ToString("N3"));
        double dV = burnCalc(sSMA, sSMA, 0, Ap2, SMA2, ce, activeVessel.mainBody.gravParameter);
        DrawEntry("Injection Δv", dV.ToString("N3"), "m/s");

        DrawSectionEnd();
    }

    private void FillManeuver(int _ = 0)
    {
        DrawSectionHeader("Maneuver");

        PatchedConicsOrbit newOrbit = activeVessel.Orbiter.ManeuverPlanSolver.PatchedConicsList.FirstOrDefault();
        DrawEntry("Projected Ap.", MetersToDistanceString(newOrbit.ApoapsisArl), "m");
        DrawEntry("Projected Pe.", MetersToDistanceString(newOrbit.PeriapsisArl), "m");
        DrawEntry("∆v required", $"{currentManeuver.BurnRequiredDV:N1}", "m/s");
        double timeUntilNode = currentManeuver.Time - GameManager.Instance.Game.UniverseModel.UniversalTime;
        DrawEntry("Time to", SecondsToTimeString(timeUntilNode), "s");
        DrawEntry("Burn Time", SecondsToTimeString(currentManeuver.BurnDuration), "s");

        DrawSectionEnd();
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

    private void DrawSectionHeader(string sectionName, string value = "") // was (string sectionName, ref bool isPopout, string value = "")
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
    
    private void DrawEntryButton(string entryName, ref bool button, string buttonStr, string value, string unit = "")
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(entryName, nameLabelStyle);
        GUILayout.FlexibleSpace();
        button = GUILayout.Button(buttonStr, ctrlBtnStyle);
        GUILayout.Label(value, valueLabelStyle);
        GUILayout.Space(5);
        GUILayout.Label(unit, unitLabelStyle);
        GUILayout.EndHorizontal();
        GUILayout.Space(spacingAfterEntry);
    }

    private void DrawEntry2Button(string entryName, ref bool button1, string button1Str, ref bool button2, string button2Str, string value, string unit = "")
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(entryName, nameLabelStyle);
        GUILayout.FlexibleSpace();
        button1 = GUILayout.Button(button1Str, ctrlBtnStyle);
        button2 = GUILayout.Button(button2Str, ctrlBtnStyle);
        GUILayout.Label(value, valueLabelStyle);
        GUILayout.Space(5);
        GUILayout.Label(unit, unitLabelStyle);
        GUILayout.EndHorizontal();
        GUILayout.Space(spacingAfterEntry);
    }

    private void DrawEntryTextField(string entryName, ref string textEntry, string unit = "")
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(entryName, nameLabelStyle);
        GUILayout.FlexibleSpace();
        textEntry = GUILayout.TextField(textEntry, textInputStyle);
        GUILayout.Space(5);
        GUILayout.Label(unit, unitLabelStyle);
        GUILayout.EndHorizontal();
        GUILayout.Space(spacingAfterEntry);
    }

    private void DrawSectionEnd() // was (ref bool isPopout)
    {
        //if (isPopout)
        //{
        //    GUI.DragWindow(new Rect(0, 0, windowWidth, windowHeight));
        //    GUILayout.Space(spacingBelowPopout);
        //}
        //else
        //{
            GUILayout.Space(spacingAfterSection);
        //}
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
    //private static void FillWindow(int windowID)
    //{
    //    GUILayout.Label("Resonant Orbit Calculator");
    //    GUI.DragWindow(new Rect(0, 0, 10000, 500));
    //}
}
