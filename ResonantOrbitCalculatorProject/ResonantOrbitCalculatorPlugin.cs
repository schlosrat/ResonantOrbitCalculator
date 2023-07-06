using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
// using FlightPlan.UI;
using HarmonyLib;
using KSP.Game;
using KSP.Sim.impl;
using KSP.Sim.Maneuver;
using KSP.UI.Binding;
using ResonantOrbitCalculator.UI;
using SpaceWarp;
using SpaceWarp.API.Assets;
using SpaceWarp.API.Mods;
using SpaceWarp.API.UI.Appbar;
using System.Reflection;
using UnityEngine;
using static KSP.Rendering.Planets.PQSData;
// using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ResonantOrbitCalculator;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency(SpaceWarpPlugin.ModGuid, SpaceWarpPlugin.ModVer)]
public class ResonantOrbitCalculatorPlugin : BaseSpaceWarpPlugin
{
  public static ResonantOrbitCalculatorPlugin Instance { get; set; }

  // These are useful in case some other mod wants to add a dependency to this one
  public const string ModGuid = MyPluginInfo.PLUGIN_GUID;
  public const string ModName = MyPluginInfo.PLUGIN_NAME;
  public const string ModVer = MyPluginInfo.PLUGIN_VERSION;

  private ConfigEntry<KeyboardShortcut> _keybind;
  private ConfigEntry<KeyboardShortcut> _keybind2;

  // Control game input state while user has clicked into a TextField.
  private bool gameInputState = true;
  public List<String> inputFields = new List<String>();
  static bool loaded = false;
  private bool interfaceEnabled = false;
  private bool GUIenabled = true;
  private Rect windowRect;
  private int windowWidth = Screen.width / 5; //384px on 1920x1080
  private int windowHeight = Screen.height / 3; //360px on 1920x1080

  // Configuration parameters
  public bool dive { get; private set; }
  public bool occlusion { get; private set; }
  // private ConfigEntry<bool> dive, occlusion;

  // Local variables (should these be at this level?)
  private string numSatellites = "3";         // String number of satellites to deploy (>= 2)
                                              // private int numSats = 3;                 // Integer number of satellites to deploy (>= 2)
  private string numOrbits = "1";             // String number of resonant orbit passes between deployments (>= 2)
                                              // private int numOrb = 1;                  // Integer number of resonant orbit passes between deployments (>= 1)
  private string resonanceStr;                // String resonant factor relating the deploy orbit and the destiantion orbit
  private double resonance;                   // Double resonant factor relating the deploy orbit and the destiantion orbit 
  private string targetAltitude = "600";      // String planned altitide for deployed satellites (destiantion orbit)
  private double target_alt_km = 600;         // Double planned altitide for deployed satellites (destiantion orbit)
  private double satPeriod;                   // The period of the destination orbit
  private double xferPeriod;                  // The period of the resonant deploy orbit (xferPeriod = resonance*satPeriod)
  private double Ap2;                         // The resonant deploy orbit apoapsis
  private double Pe2;                         // The resonant deploy orbit periapsis
                                              // private double occModAtm = 0.75;            // Double Occlusion Modifier for Atmosphere
                                              // private double occModVac = 0.9;             // Double Occlusion Modifier for Vacuume
                                              // private string occModAtmStr = "0.75";       // String Occlusion Modifier for Atmosphere
                                              // private string occModVacStr = "0.9";        // String Occlusion Modifier for Vacuume 
  private bool nSatUp, nSatDown, nOrbUp, nOrbDown, setTgtPe, setTgtAp, setTgtSync, setTgtSemiSync, setTgtMinLOS, fixPe, fixAp;
  private double synchronousPeriod;           // Syncronous Orbital period about the main body (not sayin its even possible...)
  private double semiSynchronousPeriod;       // Semi-Syncronous Orbital period about the main body (not sayin its even possible...)
  private double synchronousAlt;              // Syncronous Orbital altitude about the main body (not sayin its even possible...)
  private double semiSynchronousAlt;          // Semi-Syncronous Orbital altitude about the main body (not sayin its even possible...)
  private double minLOSAlt;                   // Minimum LOS Orbit Altitude (only defined if 3 or more satellites in constelation)

  // Dictionaries used for toggle button management to function like radio buttons. If no "radio buttons", then this can go.
  private Dictionary<string, bool> _toggles = new();
  private Dictionary<string, bool> _previousToggles = new();
  private readonly Dictionary<string, bool> _initialToggles = new()
  {
    { "fixPe", false },
    { "fixAp", false }
  };

  // private readonly int windowWidth = 320;
  // private readonly int windowHeight = 700;
  public Rect mainGuiRect, settingsGuiRect, parGuiRect, orbGuiRect, surGuiRect, fltGuiRect, manGuiRect, tgtGuiRect, stgGuiRect;

  // private GameInstance game;

  public bool diveOrbit; // Adapt a show* variable to track if we're doing a diving orbit or not
  public bool occlusionModifiers; // Adapt a show* variable to track if we're applying occlusion modifiers or not
                                  // public bool showMan = true; // Adapt a show* variable to track if we've got a maneuver setup for the orbit

  public bool popoutSettings, popoutPar, popoutOrb, popoutSur, popoutMan, popoutTgt, popoutFlt, popoutStg;

  private VesselComponent activeVessel;
  // private SimulationObjectModel currentTarget;
  private ManeuverNodeData currentManeuver;
  public ManeuverNodeData currentNode = null;


  // App bar button(s)
  private const string ToolbarFlightButtonID = "BTN-ResonantOrbitCalculatorFlight";
  // private const string ToolbarOABButtonID = "BTN-ResonantOrbitCalculatorOAB";

  private static string _assemblyFolder;
  private static string AssemblyFolder =>
      _assemblyFolder ?? (_assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

  private static string _settingsPath;
  private static string SettingsPath =>
      _settingsPath ?? (_settingsPath = Path.Combine(AssemblyFolder, "settings.json"));

  //public ManualLogSource logger;
  public new static ManualLogSource Logger { get; set; }

  /// <summary>
  /// Runs when the mod is first initialized.
  /// </summary>
  public override void OnInitialized()
  {
    base.OnInitialized();

    _keybind = Config.Bind(
    new ConfigDefinition("Keybindings", "First Keybind"),
    new KeyboardShortcut(KeyCode.R, KeyCode.LeftAlt),
    new ConfigDescription("Keybind to open mod window")
    );

    _keybind2 = Config.Bind(
    new ConfigDefinition("Keybindings", "Second Keybind"),
    new KeyboardShortcut(KeyCode.R, KeyCode.RightAlt, KeyCode.AltGr),
    new ConfigDescription("Keybind to open mod window")
    );
    ROCSettings.Init(SettingsPath);

    // Setup the list of input field names (most are the same as the entry string text displayed in the GUI window)
    inputFields.Add("Target Altitude");
    inputFields.Add("Atm");
    inputFields.Add("Vac");

    Instance = this;

    // game = GameManager.Instance.Game;
    Logger = base.Logger;
    // Subscribe to messages that indicate it's OK to raise the GUI
    // StateChanges.FlightViewEntered += message => GUIenabled = true;
    // StateChanges.Map3DViewEntered += message => GUIenabled = true;

    // Subscribe to messages that indicate it's not OK to raise the GUI
    // StateChanges.FlightViewLeft += message => GUIenabled = false;
    // StateChanges.Map3DViewLeft += message => GUIenabled = false;
    // StateChanges.VehicleAssemblyBuilderEntered += message => GUIenabled = false;
    // StateChanges.KerbalSpaceCenterStateEntered += message => GUIenabled = false;
    // StateChanges.BaseAssemblyEditorEntered += message => GUIenabled = false;
    // StateChanges.MainMenuStateEntered += message => GUIenabled = false;
    // StateChanges.ColonyViewEntered += message => GUIenabled = false;
    // StateChanges.TrainingCenterEntered += message => GUIenabled = false;
    // StateChanges.MissionControlEntered += message => GUIenabled = false;
    // StateChanges.TrackingStationEntered += message => GUIenabled = false;
    // StateChanges.ResearchAndDevelopmentEntered += message => GUIenabled = false;
    // StateChanges.LaunchpadEntered += message => GUIenabled = false;
    // StateChanges.RunwayEntered += message => GUIenabled = false;

    Logger.LogInfo("Loaded");
    if (loaded)
    {
      Destroy(this);
    }
    loaded = true;

    // Initialize the toggle button dictionaries
    _toggles = new Dictionary<string, bool>(_initialToggles);
    _previousToggles = new Dictionary<string, bool>(_initialToggles);

    gameObject.hideFlags = HideFlags.HideAndDontSave;
    DontDestroyOnLoad(gameObject);

    // Register Flight AppBar button
    Appbar.RegisterAppButton(
        "Resonant Orbit Calc.",
        ToolbarFlightButtonID,
        AssetManager.GetAsset<Texture2D>($"{SpaceWarpMetadata.ModID}/images/icon.png"),
        ToggleButton);

    // Register all Harmony patches in the project
    Harmony.CreateAndPatchAll(typeof(ResonantOrbitCalculatorPlugin).Assembly);

    // Fetch a configuration value or create a default one if it does not exist
    var defaultValue = true;
    var diveConfig = Config.Bind<bool>("Settings section", "Diving Resonant Transfer Orbit", defaultValue, "Use a diving (vs. climbing) resonant transfer orbit");
    var occlusionConfig = Config.Bind<bool>("Settings section", "Occlusion Modifiers", defaultValue, "Apply Occlusion modifiers for Min LOS Orbit Altitude");

    dive = diveConfig.Value;
    occlusion = occlusionConfig.Value;

    // Log the config value into <KSP2 Root>/BepInEx/LogOutput.log
    Logger.LogInfo($"Dive: {dive}");
    Logger.LogInfo($"Occlusion: {occlusion}");

    diveOrbit = dive;
    occlusionModifiers = occlusion;
  }

  private void ToggleButton(bool toggle)
  {
    interfaceEnabled = toggle;
    GameObject.Find(ToolbarFlightButtonID)?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(interfaceEnabled);
  }

  void Awake()
  {
    windowRect = new Rect((Screen.width * 0.7f) - (windowWidth / 2), (Screen.height / 2) - (windowHeight / 2), 0, 0);
  }

  void Update()
  {
    if ((_keybind != null && _keybind.Value.IsDown()) || (_keybind2 != null && _keybind2.Value.IsDown()))
    {
      ToggleButton(!interfaceEnabled);
      if (_keybind != null && _keybind.Value.IsDown())
        Logger.LogDebug($"Update: UI toggled with _keybind, hotkey {_keybind.Value}");
      if (_keybind2 != null && _keybind2.Value.IsDown())
        Logger.LogDebug($"Update: UI toggled with _keybind2, hotkey {_keybind2.Value}");
    }
  }

  void save_rect_pos()
  {
    ROCSettings.window_x_pos = (int)windowRect.xMin;
    ROCSettings.window_y_pos = (int)windowRect.yMin;
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
  private void OnGUI()
  {
    GUIenabled = false;
    var gameState = Game?.GlobalGameState?.GetState();
    if (gameState == GameState.Map3DView) GUIenabled = true;
    if (gameState == GameState.FlightView) GUIenabled = true;
    //if (Game.GlobalGameState.GetState() == GameState.TrainingCenter) GUIenabled = false;
    //if (Game.GlobalGameState.GetState() == GameState.TrackingStation) GUIenabled = false;
    //if (Game.GlobalGameState.GetState() == GameState.VehicleAssemblyBuilder) GUIenabled = false;
    //// if (Game.GlobalGameState.GetState() == GameState.MissionControl) GUIenabled = false;
    //if (Game.GlobalGameState.GetState() == GameState.Loading) GUIenabled = false;
    //if (Game.GlobalGameState.GetState() == GameState.KerbalSpaceCenter) GUIenabled = false;
    //if (Game.GlobalGameState.GetState() == GameState.Launchpad) GUIenabled = false;
    //if (Game.GlobalGameState.GetState() == GameState.Runway) GUIenabled = false;
    // activeVessel = Game?.ViewController?.GetActiveVehicle(true)?.GetSimVessel(true);

    activeVessel = GameManager.Instance?.Game?.ViewController?.GetActiveVehicle(true)?.GetSimVessel(true);

    // Set the UI
    if (interfaceEnabled && GUIenabled && activeVessel != null)
    {
      ROCStyles.Init();
      ResonantOrbitCalculator.UI.UIWindow.check_main_window_pos(ref windowRect);
      GUI.skin = ROCStyles.skin;
      windowRect = GUILayout.Window(
          GUIUtility.GetControlID(FocusType.Passive),
          windowRect,
          FillMainGUI,
          "<color=#696DFF>RESONANT ORBIT CALC</color>",
          GUILayout.Height(windowHeight),
          GUILayout.Width(windowWidth));
      save_rect_pos();
      // Draw the tool tip if needed
      ToolTipsManager.DrawToolTips();

      // check editor focus and unset Input if needed
      UI_Fields.CheckEditor();

      // currentTarget = activeVessel?.TargetObject;
      currentManeuver = GameManager.Instance?.Game?.SpaceSimulation.Maneuvers.GetNodesForVessel(activeVessel.GlobalId).FirstOrDefault();

      // mainGuiRect.position = ClampToScreen(mainGuiRect.position, mainGuiRect.size);

      if (gameInputState && inputFields.Contains(GUI.GetNameOfFocusedControl()))
      {
        Logger.LogInfo($"[Flight Plan]: Disabling Game Input: Focused Item '{GUI.GetNameOfFocusedControl()}'");
        gameInputState = false;
        GameManager.Instance.Game.Input.Disable();
      }
      else if (!gameInputState && !inputFields.Contains(GUI.GetNameOfFocusedControl()))
      {
        Logger.LogInfo($"[Flight Plan]: Enabling Game Input: FYI, Focused Item '{GUI.GetNameOfFocusedControl()}'");
        gameInputState = true;
        GameManager.Instance.Game.Input.Enable();
      }
    }
    else
    {
      if (!gameInputState)
      {
        Logger.LogInfo($"[Flight Plan]: Enabling Game Input due to GUI disabled: FYI, Focused Item '{GUI.GetNameOfFocusedControl()}'");
        gameInputState = true;
        GameManager.Instance.Game.Input.Enable();
      }
    }
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
      //try { ROCSettings.occ_mod_atm = double.Parse(occModAtmStr); }
      //catch { ROCSettings.occ_mod_atm = 1.0; }
      //try { ROCSettings.occ_mod_vac = double.Parse(occModVacStr); }
      //catch { ROCSettings.occ_mod_vac = 1.0; }
      if (hasAtmo)
      {
        occMod = ROCSettings.occ_mod_atm;
      }
      else
      {
        occMod = ROCSettings.occ_mod_vac;
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
      return (radius * occModCalc(hasAtmo)) / (Math.Cos(0.5 * (2.0 * Math.PI / numSat))) - radius;
    }
    else
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
    TopButtons.Init(windowRect.width);
    if (TopButtons.IconButton(ROCStyles.cross))
      CloseWindow();

    // Add a MNC_info icon to the upper left corner of the GUI
    GUI.Label(new Rect(9, 2, 29, 29), ROCStyles.icon, ROCStyles.icons_label);

    // GUILayout.Space(10);

    updateToggleButtons();

    FillParameters();
    FillCurrentOrbit();
    FillNewOrbit();

    if (currentManeuver != null)
    {
      FillManeuver();
    }

    UI_Tools.Separator();

    DrawGUIStatus();

    GUI.DragWindow(new Rect(0, 0, windowWidth, windowHeight));
  }

  private void FillParameters(int _ = 0)
  {
    synchronousPeriod = activeVessel.mainBody.rotationPeriod;
    semiSynchronousPeriod = activeVessel.mainBody.rotationPeriod / 2;
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
      m = ROCSettings.num_sats * ROCSettings.num_orb;
      n = m - 1;
    }
    else // If not
    {
      m = ROCSettings.num_sats * ROCSettings.num_orb;
      n = m + 1;
    }
    resonance = (double)n / m;
    resonanceStr = String.Format("{0}/{1}", n, m);

    // Compute the minimum LOS altitude
    minLOSAlt = minLOSCalc(ROCSettings.num_sats, activeVessel.mainBody.radius, activeVessel.mainBody.hasAtmosphere);

    DrawEntry2Button("Payloads:", ref nSatDown, "-", ref nSatUp, "+", numSatellites);
    DrawEntry2Button("Deploy Orbits:", ref nOrbDown, "-", ref nOrbUp, "+", numOrbits);
    DrawEntry("Orbital Resonance", resonanceStr, " ");
    // DrawEntry("Resonance val", resonance.ToString());

    // Logger.LogInfo($"FillParameters: tgt_altitude_km = {target_alt_km} km");
    DrawEntryTextField("Target Altitude", ref targetAltitude, "km"); // Tried" ROCSettings.tgt_altitude_km 
    double.TryParse(targetAltitude, out target_alt_km);
    //catch { targetAlt = 0; }

    satPeriod = periodCalc(target_alt_km * 1000 + activeVessel.mainBody.radius);
    DrawEntry("Period", $"{SecondsToTimeString(satPeriod)}", "s");
    if (synchronousAlt > 0)
    {
      DrawEntryButton("Synchronous Alt", ref setTgtSync, "⦾", $"{MetersToDistanceString(synchronousAlt / 1000)}", "km");
      DrawEntryButton("Semi Synchronous Alt", ref setTgtSemiSync, "⦾", $"{MetersToDistanceString(semiSynchronousAlt / 1000)}", "km");
    }
    else if (semiSynchronousAlt > 0)
    {
      DrawEntry("Synchronous Alt", "Outside SOI", " ");
      DrawEntryButton("Semi Synchronous Alt", ref setTgtSemiSync, "⦾", $"{MetersToDistanceString(semiSynchronousAlt / 1000)}", "km");
    }
    else
    {
      DrawEntry("Synchronous Alt", "Outside SOI", " ");
      DrawEntry("Semi Synchronous Alt", "Outside SOI", " ");
    }
    DrawEntry("SOI Alt", $"{MetersToDistanceString(activeVessel.mainBody.sphereOfInfluence / 1000)}", "km");
    if (minLOSAlt > 0)
    {
      DrawEntryButton("Min LOS Orbit Alt", ref setTgtMinLOS, "⦾", $"{MetersToDistanceString(minLOSAlt / 1000)}", "km");
    }
    else
    {
      DrawEntry("Min LOS Orbit Alt", "Undefined", "km");
    }
    DrawSoloToggle("<b>Occlusion</b>", ref occlusionModifiers);
    if (occlusionModifiers)
    {
      ROCSettings.occ_mod_atm = ROCSettings.occ_mod_atm = DrawEntryTextField("Atm", ROCSettings.occ_mod_atm);
      //try { occModAtm = double.Parse(occModAtmStr); }
      //catch { occModAtm = 1.0; }
      ROCSettings.occ_mod_vac = ROCSettings.occ_mod_vac = DrawEntryTextField("Vac", ROCSettings.occ_mod_vac);
      //try { occModVac = double.Parse(occModVacStr); }
      //catch { occModVac = 1.0; }
    }

    DrawSectionEnd();

    handleButtons();
  }

  // This method sould be called at the top of FillWindow to enable toggle buttons to work like radio buttons
  private void updateToggleButtons()
  {
    // Make toggle buttons behave like radio buttons
    int numChecked = _toggles.Count(item => item.Value); // how many are selected now (could be 0, 1, or 2)
    int oldNumChecked = _previousToggles.Count(item => item.Value); // how many were selected before (could be 0 or 1)
    if (numChecked == 0)
    {
      if (oldNumChecked > 0) // if the selected action has been deselected
        _previousToggles = new Dictionary<string, bool>(_initialToggles);
    }
    else if (numChecked == 1)
    {
      if (oldNumChecked == 0) // We gone from none selected to 1 selected
      {
        var selected = _toggles.FirstOrDefault(item => item.Value).Key;
        _previousToggles[selected] = true; // record the new selection in the previous list
      }
      else if (oldNumChecked == 1) // they should be the same, let's check
      {
        var oldSelected = _previousToggles.FirstOrDefault(item => item.Value).Key;
        var newSelected = _toggles.FirstOrDefault(item => item.Value).Key;
        if (oldSelected != newSelected)
        {
          Logger.LogWarning($"Selection Mismatch: Previously {oldSelected} was selected, but now {newSelected} is selected. Correcting previous list.");
          _previousToggles[oldSelected] = false; // update the previous list to deselect the previous selection
          _previousToggles[newSelected] = true;  // update the previous list to select the new selection
        }
      }
      else // We shouldn't get here, but if there's more than one thing selected in the previous list and only one in the current list then fix it
      {
        _previousToggles = new Dictionary<string, bool>(_initialToggles);
        var newSelected = _toggles.FirstOrDefault(item => item.Value).Key;
        _previousToggles[newSelected] = true;
      }
    }
    else if (numChecked == 2)
    {
      if (oldNumChecked == 0) // This should not happen, report it and clear everything
      {
        Logger.LogError($"Selection Mismatch: Two or more things selected with zero previously. Resetting all.");
        _toggles = new Dictionary<string, bool>(_initialToggles);
        _previousToggles = new Dictionary<string, bool>(_initialToggles);
        clearToggleStates();
      }
      else if (oldNumChecked == 1) // We've selected something new
      {
        var oldSelected = _previousToggles.FirstOrDefault(item => item.Value).Key;
        _toggles[oldSelected] = false; // deselect the previous selection
        setToggleState(oldSelected, false);
        var newSelected = _toggles.FirstOrDefault(item => item.Value).Key;
        _previousToggles[newSelected] = true; // update the previous list to select the new selection
      }
      else // We shouldn't get here, but if there's more than one thing selected in the previous list and two in the current list then clear everything
      {
        Logger.LogError($"Selection Mismatch: Two or more things selected with two or more previously. Resetting all.");
        _toggles = new Dictionary<string, bool>(_initialToggles);
        _previousToggles = new Dictionary<string, bool>(_initialToggles);
        clearToggleStates();
      }
    }
    else // We should not be able to get here! Deselect everything...
    {
      Logger.LogError($"Selection Mismatch: More than two things selected! Resetting all.");
      _toggles = new Dictionary<string, bool>(_initialToggles);
      _previousToggles = new Dictionary<string, bool>(_initialToggles);
      clearToggleStates();
    }

    // Make toggle buttons behave like radio buttons
    numChecked = _toggles.Count(item => item.Value); // how many are selected now (could be 0, 1)
    oldNumChecked = _previousToggles.Count(item => item.Value); // how many were selected before (should match numChecked)
    string selectedAction = null;
    if (numChecked == 1)
      selectedAction = _toggles.FirstOrDefault(item => item.Value).Key;
    else if (numChecked > 1)
    {
      Logger.LogError($"Selection Mismatch: Two or more things selected after update. Resetting all.");
      _toggles = new Dictionary<string, bool>(_initialToggles);
      _previousToggles = new Dictionary<string, bool>(_initialToggles);
      clearToggleStates();
    }
  }

  // This method is called by updateToggleButtons to set the sate of a toggle button
  private void setToggleState(string key, bool value)
  {
    if (key == "fixPe")
      fixPe = value;
    if (key == "fixAp")
      fixAp = value;
  }

  // This method is called by updateToggleButtons to clear the state of all toggle buttons
  private void clearToggleStates()
  {
    fixPe = false;
    fixAp = false;
  }


  private void handleButtons()
  {
    if (nSatDown || nSatUp || nOrbDown || nOrbUp || setTgtPe || setTgtAp || setTgtSync || setTgtSemiSync || setTgtMinLOS || fixPe || fixAp)
    {
      // burnParams = Vector3d.zero;
      if (nSatDown && ROCSettings.num_sats > 2)
      {
        ROCSettings.num_sats--;
        numSatellites = ROCSettings.num_sats.ToString();
      }
      else if (nSatUp)
      {
        ROCSettings.num_sats++;
        numSatellites = ROCSettings.num_sats.ToString();
      }
      else if (nOrbDown && ROCSettings.num_orb > 1)
      {
        ROCSettings.num_orb--;
        numOrbits = ROCSettings.num_orb.ToString();
      }
      else if (nOrbUp)
      {
        ROCSettings.num_orb++;
        numOrbits = ROCSettings.num_orb.ToString();
      }
      else if (setTgtPe)
      {
        // Logger.LogInfo($"handleButtons: Setting tgt_altitude_km to Periapsis {activeVessel.Orbit.PeriapsisArl / 1000.0} km");
        target_alt_km = activeVessel.Orbit.PeriapsisArl / 1000.0;
        targetAltitude = target_alt_km.ToString("N3");
        // Logger.LogInfo($"handleButtons: tgt_altitude_km set to {targetAltitude} km");
      }
      else if (setTgtAp)
      {
        // Logger.LogInfo($"handleButtons: Setting tgt_altitude_km to Apoapsis {activeVessel.Orbit.ApoapsisArl / 1000.0} km");
        target_alt_km = activeVessel.Orbit.ApoapsisArl / 1000.0;
        targetAltitude = target_alt_km.ToString("N3");
        // Logger.LogInfo($"handleButtons: tgt_altitude_km set to {targetAltitude} km");

      }
      else if (setTgtSync)
      {
        // Logger.LogInfo($"handleButtons: Setting tgt_altitude_km to synchronousAlt {synchronousAlt / 1000.0} km");
        target_alt_km = synchronousAlt / 1000.0;
        targetAltitude = target_alt_km.ToString("N3");
        // Logger.LogInfo($"handleButtons: tgt_altitude_km set to {targetAltitude} km");

      }
      else if (setTgtSemiSync)
      {
        // Logger.LogInfo($"handleButtons: Setting tgt_altitude_km to semiSynchronousAlt {semiSynchronousAlt / 1000.0} km");
        target_alt_km = semiSynchronousAlt / 1000.0;
        targetAltitude = target_alt_km.ToString("N3");
        // Logger.LogInfo($"handleButtons: tgt_altitude_km set to {targetAltitude} km");

      }
      else if (setTgtMinLOS)
      {
        // Logger.LogInfo($"handleButtons: Setting tgt_altitude_km to minLOSAlt {minLOSAlt / 1000.0} km");
        target_alt_km = minLOSAlt / 1000.0;
        targetAltitude = target_alt_km.ToString("N3");
        // Logger.LogInfo($"handleButtons: tgt_altitude_km set to {targetAltitude} km");

      }
      if (fixPe)
        _toggles["fixPe"] = true;
      if (fixAp)
        _toggles["fixAp"] = true;
    }
  }

  private void FillCurrentOrbit(int _ = 0)
  {
    DrawSectionHeader("Current Orbit");

    DrawEntry("Period", $"{SecondsToTimeString(activeVessel.Orbit.period)}", "s");
    DrawEntryButton("Apoapsis", ref setTgtAp, "⦾", $"{MetersToDistanceString(activeVessel.Orbit.ApoapsisArl / 1000)}", "km");
    DrawEntryButton("Periapsis", ref setTgtPe, "⦾", $"{MetersToDistanceString(activeVessel.Orbit.PeriapsisArl / 1000)}", "km");
    DrawEntry("Time to Ap.", $"{SecondsToTimeString((activeVessel.Situation == VesselSituations.Landed || activeVessel.Situation == VesselSituations.PreLaunch) ? 0f : activeVessel.Orbit.TimeToAp)}", "s");
    DrawEntry("Time to Pe.", $"{SecondsToTimeString(activeVessel.Orbit.TimeToPe)}", "s");
    DrawEntry("Inclination", $"{activeVessel.Orbit.inclination:N3}", "°");
    DrawEntry("Eccentricity", $"{activeVessel.Orbit.eccentricity:N3}", " ");
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

    DrawSoloToggle("<b>Dive</b>", ref diveOrbit);

    // period1 = periodCalc(target_alt_km*1000 + activeVessel.mainBody.radius);
    xferPeriod = resonance * satPeriod;
    double SMA2 = SMACalc(xferPeriod);
    double sSMA = target_alt_km * 1000 + activeVessel.mainBody.radius;
    if (diveOrbit)
    {
      Ap2 = sSMA; // Diveing transfer orbits release at Apoapsis
      Pe2 = 2.0 * SMA2 - (Ap2);
    }
    else
    {
      Pe2 = sSMA; // Non-diving transfer orbits release at Periapsis
      Ap2 = 2.0 * SMA2 - (Pe2);
    }
    double ce = (Ap2 - Pe2) / (Ap2 + Pe2);
    DrawEntry("Period", $"{SecondsToTimeString(xferPeriod)}", "s");
    DrawEntry("Apoapsis", $"{MetersToDistanceString((Ap2 - activeVessel.mainBody.radius) / 1000)}", "km");
    DrawEntry("Periapsis", $"{MetersToDistanceString((Pe2 - activeVessel.mainBody.radius) / 1000)}", "km");
    DrawEntry("Eccentricity", ce.ToString("N3"), " ");
    double dV = burnCalc(sSMA, sSMA, 0, Ap2, SMA2, ce, activeVessel.mainBody.gravParameter);
    DrawEntry("Injection Δv", dV.ToString("N3"), "m/s");

    DrawSectionEnd();
  }

  private void FillManeuver(int _ = 0)
  {
    DrawSectionHeader("Maneuver");

    PatchedConicsOrbit newOrbit = activeVessel.Orbiter.ManeuverPlanSolver.PatchedConicsList.FirstOrDefault();
    DrawEntry("Projected Ap.", MetersToDistanceString(newOrbit.ApoapsisArl / 1000), "km");
    DrawEntry("Projected Pe.", MetersToDistanceString(newOrbit.PeriapsisArl / 1000), "km");
    DrawEntry("∆v required", $"{currentManeuver.BurnRequiredDV:N1}", "m/s");
    double timeUntilNode = currentManeuver.Time - GameManager.Instance.Game.UniverseModel.UniversalTime;
    DrawEntry("Time to", SecondsToTimeString(timeUntilNode), "s");
    DrawEntry("Burn Time", SecondsToTimeString(currentManeuver.BurnDuration), "s");

    DrawSectionEnd();
  }

  private void DrawToggleButton(string runString, ref bool button, string stopString = "")
  {
    if (stopString.Length < 1)
      stopString = runString;
    button = UI_Tools.ToggleButton(button, runString, stopString);
  }

  private void DrawSoloToggle(string toggleStr, ref bool toggle)
  {
    GUILayout.Space(ROCStyles.spacingAfterSection);
    GUILayout.BeginHorizontal();
    toggle = GUILayout.Toggle(toggle, toggleStr, ROCStyles.toggle); // was section_toggle
    GUILayout.FlexibleSpace();
    GUILayout.EndHorizontal();
    GUILayout.Space(-ROCStyles.spacingAfterSection);
  }

  private void DrawSectionHeader(string sectionName, string value = "", string units = "") // was (string sectionName, ref bool isPopout, string value = "")
  {
    GUILayout.BeginHorizontal();
    // Don't need popout buttons for ROC
    // isPopout = isPopout ? !CloseButton() : GUILayout.Button("⇖", popoutBtnStyle);

    GUILayout.Label($"<b>{sectionName}</b>");
    GUILayout.FlexibleSpace();
    if (value.Length > 0)
    {
      GUILayout.Label(value, ROCStyles.valueLabelStyle);
      if (units.Length > 0)
      {
        GUILayout.Space(5);
        GUILayout.Label(units, ROCStyles.unitLabelStyle);
      }
    }
    GUILayout.EndHorizontal();
    GUILayout.Space(ROCStyles.spacingAfterHeader);
  }

  private void DrawEntry(string entryName, string value, string unit = "")
  {
    GUILayout.BeginHorizontal();
    GUILayout.Label(entryName, ROCStyles.nameLabelStyle);
    GUILayout.FlexibleSpace();
    GUILayout.Label(value, ROCStyles.valueLabelStyle);
    if (unit.Length > 0)
    {
      GUILayout.Space(5);
      GUILayout.Label(unit, ROCStyles.unitLabelStyle);
    }
    GUILayout.EndHorizontal();
    GUILayout.Space(ROCStyles.spacingAfterEntry);
  }

  private void DrawEntryButton(string entryName, ref bool button, string buttonStr, string value, string unit = "")
  {
    GUILayout.BeginHorizontal();
    GUILayout.Label(entryName, ROCStyles.nameLabelStyle);
    GUILayout.FlexibleSpace();
    button = GUILayout.Button(buttonStr, ROCStyles.ctrl_button);
    GUILayout.Label(value, ROCStyles.valueLabelStyle);
    GUILayout.Space(5);
    GUILayout.Label(unit, ROCStyles.unitLabelStyle);
    GUILayout.EndHorizontal();
    GUILayout.Space(ROCStyles.spacingAfterEntry);
  }

  private void DrawEntry2Button(string entryName, ref bool button1, string button1Str, ref bool button2, string button2Str, string value, string unit = "")
  {
    GUILayout.BeginHorizontal();
    GUILayout.Label(entryName, ROCStyles.nameLabelStyle);
    GUILayout.FlexibleSpace();
    button1 = GUILayout.Button(button1Str, ROCStyles.ctrl_button);
    button2 = GUILayout.Button(button2Str, ROCStyles.ctrl_button);
    GUILayout.Label(value, ROCStyles.valueLabelStyle);
    GUILayout.Space(5);
    GUILayout.Label(unit, ROCStyles.unitLabelStyle);
    GUILayout.EndHorizontal();
    GUILayout.Space(ROCStyles.spacingAfterEntry);
  }

  private void DrawEntryTextField(string entryName, ref string textEntry, string unit = "")
  {
    double num;
    Color normal;

    GUILayout.BeginHorizontal();
    GUILayout.Label(entryName, ROCStyles.nameLabelStyle);
    GUILayout.FlexibleSpace();
    normal = GUI.color;
    bool parsed = double.TryParse(textEntry, out num);
    if (!parsed) GUI.color = Color.red;
    GUI.SetNextControlName(entryName);
    textEntry = GUILayout.TextField(textEntry, ROCStyles.textInputStyle);
    GUI.color = normal;
    GUILayout.Space(5);
    GUILayout.Label(unit, ROCStyles.unitLabelStyle);
    GUILayout.EndHorizontal();
    GUILayout.Space(ROCStyles.spacingAfterEntry);
  }

  private double DrawEntryTextField(string entryName, double value, string unit = "")
  {
    GUILayout.BeginHorizontal();
    GUILayout.Label(entryName, ROCStyles.nameLabelStyle);
    GUILayout.FlexibleSpace();
    GUI.SetNextControlName(entryName);
    value = UI_Fields.DoubleField(entryName, value);
    GUILayout.Space(5);
    GUILayout.Label(unit, ROCStyles.unitLabelStyle);
    GUILayout.EndHorizontal();
    GUILayout.Space(ROCStyles.spacingAfterEntry);
    return value;
  }

  private double DrawEntryTextField(string entryName, ref double value, string unit = "")
  {
    GUILayout.BeginHorizontal();
    GUILayout.Label(entryName, ROCStyles.nameLabelStyle);
    GUILayout.FlexibleSpace();
    GUI.SetNextControlName(entryName);
    value = UI_Fields.DoubleField(entryName, value);
    GUILayout.Space(5);
    GUILayout.Label(unit, ROCStyles.unitLabelStyle);
    GUILayout.EndHorizontal();
    GUILayout.Space(ROCStyles.spacingAfterEntry);
    return value;
  }

  private int DrawEntryTextField(string entryName, int value, string unit = "")
  {
    GUILayout.BeginHorizontal();
    GUILayout.Label(entryName, ROCStyles.nameLabelStyle);
    GUILayout.FlexibleSpace();
    GUI.SetNextControlName(entryName);
    value = UI_Fields.IntField(entryName, value);
    GUILayout.Space(5);
    GUILayout.Label(unit, ROCStyles.unitLabelStyle);
    GUILayout.EndHorizontal();
    GUILayout.Space(ROCStyles.spacingAfterEntry);
    return value;
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
    GUILayout.Space(ROCStyles.spacingAfterSection);
    //}
  }

  public ROCOtherModsInterface other_mods = null;

  private void DrawGUIStatus() // double UT
  {
    //// Indicate status of last GUI function
    //float transparency = 1;
    //if (UT > statusTime) transparency = (float)MuUtils.Clamp(1 - (UT - statusTime) / statusFadeTime.Value, 0, 1);

    //var status_style = FPStyles.status;
    ////if (status == Status.VIRGIN)
    ////    status_style = FPStyles.label;
    //if (status == Status.OK)
    //    status_style.normal.textColor = new Color(0, 1, 0, transparency); // FPStyles.phase_ok;
    //if (status == Status.WARNING)
    //    status_style.normal.textColor = new Color(1, 1, 0, transparency); // FPStyles.phase_warning;
    //if (status == Status.ERROR)
    //    status_style.normal.textColor = new Color(1, 0, 0, transparency); // FPStyles.phase_error;

    double errorPe = (Pe2 - activeVessel.Orbit.Periapsis) / 1000;
    double errorAp = (Ap2 - activeVessel.Orbit.Apoapsis) / 1000;
    string fixPeStr, fixApStr;

    GUILayout.Space(-ROCStyles.spacingAfterSection);

    if (errorPe > 0)
      fixPeStr = $"Raise Pe by {errorPe:N2} km to {((Pe2 - activeVessel.mainBody.radius) / 1000):N2} km";
    else
      fixPeStr = $"Lower Pe by {(-errorPe):N2} km to {((Pe2 - activeVessel.mainBody.radius) / 1000):N2} km";
    if (errorAp > 0)
      fixApStr = $"Raise Ap by {errorAp:N2} km to {((Ap2 - activeVessel.mainBody.radius) / 1000):N2} km";
    else
      fixApStr = $"Lower Ap by {(-errorAp):N2} km to {((Ap2 - activeVessel.mainBody.radius) / 1000):N2} km";
    if (activeVessel.Orbit.Apoapsis < Pe2)
    {
      DrawSoloToggle(fixApStr, ref fixAp);
      fixPe = false;
      _toggles["fixPe"] = false;
    }
    else if (activeVessel.Orbit.Periapsis > Ap2)
    {
      DrawSoloToggle(fixPeStr, ref fixPe);
      fixAp = false;
      _toggles["fixAp"] = false;
    }
    else
    {
      DrawSoloToggle(fixPeStr, ref fixPe);
      DrawSoloToggle(fixApStr, ref fixAp);
    }

    GUILayout.Space(ROCStyles.spacingAfterSection);

    UI_Tools.Separator();
    //DrawSectionHeader("Status:", statusText, status_style);

    // Indication to User that its safe to type, or why vessel controls aren't working

    if (other_mods == null)
    {
      // init mode detection only when first needed
      other_mods = new ROCOtherModsInterface();
      other_mods.CheckModsVersions();
    }

    other_mods.OnGUI(currentNode);
    // GUILayout.Space(ROCStyles.spacingAfterEntry);

    UI_Tools.Separator();

    // Indication to User that its safe to type, or why vessel controls aren't working
    GUILayout.BeginHorizontal();
    string inputStateString = gameInputState ? "<b>Enabled</b>" : "<b>Disabled</b>";
    GUILayout.Label("Game Input: ", ROCStyles.label);
    if (gameInputState)
      GUILayout.Label(inputStateString, ROCStyles.label);
    else
      GUILayout.Label(inputStateString, ROCStyles.warning);
    GUILayout.FlexibleSpace();
    GUILayout.EndHorizontal();
  }

  public void MakeNode()
  {
    if (other_mods.FPLoaded && other_mods.NMLoaded)
    {
      if (fixPe)
      {
        other_mods.SetNewPe(activeVessel.Orbit.TimeToAp + Game.UniverseModel.UniversalTime, Pe2, -0.5);
        // FlightPlanPlugin.Instance.SetNewPe(activeVessel.Orbit.TimeToAp + Game.UniverseModel.UniversalTime, Pe2, -0.5);
      }
      else if (fixAp)
      {
        other_mods.SetNewAp(activeVessel.Orbit.TimeToPe + Game.UniverseModel.UniversalTime, Ap2, -0.5);
        // FlightPlanPlugin.Instance.SetNewAp(activeVessel.Orbit.TimeToPe + Game.UniverseModel.UniversalTime, Ap2, -0.5);
      }
    }
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
      result += $"{days}{spacing}<color=#{ROCStyles.unitColorHex}>d</color> ";
    }

    if (hours > 0 || days > 0)
    {
      {
        result += $"{hours}{spacing}<color=#{ROCStyles.unitColorHex}>h</color> ";
      }
    }

    if (minutes > 0 || hours > 0 || days > 0)
    {
      if (hours > 0 || days > 0)
      {
        result += $"{minutes:00.}{spacing}<color=#{ROCStyles.unitColorHex}>m</color> ";
      }
      else
      {
        result += $"{minutes}{spacing}<color=#{ROCStyles.unitColorHex}>m</color> ";
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

    string result = $"{degrees:N0}<color={ROCStyles.unitColorHex}>°</color> {minutes:00}<color={ROCStyles.unitColorHex}>'</color> {seconds:00}<color={ROCStyles.unitColorHex}>\"</color>";

    return result;
  }

  private void CloseWindow()
  {
    GameObject.Find("BTN-ResonantOrbitCalculatorFlight")?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(false);
    // GameObject.Find("BTN-ResonantOrbitCalculatorOAB")?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(false);
    interfaceEnabled = false;
    Logger.LogDebug("CloseWindow: Restoring Game Input on window close.");
    GameManager.Instance.Game.Input.Enable();
    ToggleButton(interfaceEnabled);
  }

}
