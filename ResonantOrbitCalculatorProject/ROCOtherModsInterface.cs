

using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using ResonantOrbitCalculator.UI;
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

// CF : this direct dependency cause the dll to be needed during build
// it is not really needed, we can easyly hardcode the mode names
// during the introduction of K2D2 UI it cause me many trouble in naming
using K2D2;
using KSP.Sim.Maneuver;
using ManeuverNodeController;
using SpaceWarp.API.Assets;
using System.Reflection;
using UnityEngine;
using NodeManager;
using KSP.Game;
using FlightPlan;

namespace ResonantOrbitCalculator;

public class ROCOtherModsInterface
{
    ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("FlightPlanPlugin.OtherModsInterface");

    // Reflection access variables for launching MNC_info & K2-D2
    public bool FPLoaded, MNCLoaded, K2D2Loaded, NMLoaded, checkK2D2status = false;
    private PluginInfo FP_info, MNC_info, K2D2_info, NM_info;
    private Version fpMinVersion, mncMinVersion, k2d2MinVersion, nmMinVersion;
    private int fpVerCheck, mncVerCheck, k2d2VerCheck, nmVerCheck;
    private string k2d2Status;
    Type fpType, mncType, k2d2Type, nmType;
    PropertyInfo fpPropertyInfo, mncPropertyInfo, k2d2PropertyInfo, nmPropertyInfo;
    MethodInfo k2d2GetStatusMethodInfo, k2d2FlyNodeMethodInfo, k2d2ToggleMethodInfo, mncLaunchMNCMethodInfo, fpNewPeMethodInfo, fpNewApMethodInfo, nmDeleteNodesMethodInfo;
    object fpInstance, mncInstance, k2d2Instance, nmInstance;
    Texture2D fp_button_tex, mnc_button_tex, k2d2_button_tex;
    GUIContent fp_button_tex_con, mnc_button_tex_con, k2d2_button_tex_con;

    private bool launchMNC, executeNode;

    public void CheckModsVersions()
    {
        Logger.LogInfo($"FlightPlanPlugin.ModGuid = {FlightPlanPlugin.ModGuid}");
        if (Chainloader.PluginInfos.TryGetValue(FlightPlanPlugin.ModGuid, out FP_info))
        {
            FPLoaded = true;
            Logger.LogInfo("Flight Plan installed and available");
            Logger.LogInfo($"FP_info = {FP_info}");
            fpMinVersion = new Version(0, 8, 0);
            fpVerCheck = FP_info.Metadata.Version.CompareTo(fpMinVersion);
            Logger.LogInfo($"fpVerCheck = {fpVerCheck}");

            // Get MNC_info buton icon
            // fp_button_tex = AssetManager.GetAsset<Texture2D>($"{ResonantOrbitCalculatorPlugin.Instance.SpaceWarpMetadata.ModID}/images/fp_icon.png");
            // fp_button_tex_con = new GUIContent(fp_button_tex, "Create Node with Flight Plan");

            // Reflections method to attempt the same thing more cleanly
            fpType = Type.GetType($"FlightPlan.FlightPlanPlugin, {FlightPlanPlugin.ModGuid}");
            fpPropertyInfo = fpType!.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            fpInstance = fpPropertyInfo.GetValue(null);
            fpNewPeMethodInfo = fpPropertyInfo!.PropertyType.GetMethod("SetNewPe");
            fpNewApMethodInfo = fpPropertyInfo!.PropertyType.GetMethod("SetNewAp");
        }
        // else MNCLoaded = false;
        Logger.LogInfo($"MNCLoaded = {MNCLoaded}");

        Logger.LogInfo($"ManeuverNodeControllerMod.ModGuid = {ManeuverNodeControllerMod.ModGuid}");
        if (Chainloader.PluginInfos.TryGetValue(ManeuverNodeControllerMod.ModGuid, out MNC_info))
        {
            MNCLoaded = true;
            Logger.LogInfo("Maneuver Node Controller installed and available");
            Logger.LogInfo($"MNC_info = {MNC_info}");
            // mncVersion = MNC_info.Metadata.Version;
            mncMinVersion = new Version(0, 8, 3);
            mncVerCheck = MNC_info.Metadata.Version.CompareTo(mncMinVersion);
            Logger.LogInfo($"mncVerCheck = {mncVerCheck}");

            // Get MNC_info buton icon
            // mnc_button_tex = AssetManager.GetAsset<Texture2D>($"{ResonantOrbitCalculatorPlugin.Instance.SpaceWarpMetadata.ModID}/images/mnc_big_icon.png");
            // mnc_button_tex_con = new GUIContent(mnc_button_tex, "Launch Maneuver Node Controller");

            // Reflections method to attempt the same thing more cleanly
            mncType = Type.GetType($"ManeuverNodeController.ManeuverNodeControllerMod, {ManeuverNodeControllerMod.ModGuid}");
            mncPropertyInfo = mncType!.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            mncInstance = mncPropertyInfo.GetValue(null);
            mncLaunchMNCMethodInfo = mncPropertyInfo!.PropertyType.GetMethod("LaunchMNC");
        }
        // else MNCLoaded = false;
        Logger.LogInfo($"MNCLoaded = {MNCLoaded}");

        Logger.LogInfo($"K2D2_Plugin.ModGuid = {K2D2_Plugin.ModGuid}");
        if (Chainloader.PluginInfos.TryGetValue(ManeuverNodeControllerMod.ModGuid, out K2D2_info))
        {
            K2D2_info = Chainloader.PluginInfos[K2D2_Plugin.ModGuid];

            K2D2Loaded = true;
            Logger.LogInfo("K2-D2 installed and available");
            Logger.LogInfo($"K2D2 = {K2D2_info}");
            k2d2MinVersion = new Version(0, 8, 1);
            k2d2VerCheck = K2D2_info.Metadata.Version.CompareTo(k2d2MinVersion);
            Logger.LogInfo($"k2d2VerCheck = {k2d2VerCheck}");
            string tooltip;
            if (k2d2VerCheck >= 0) tooltip = "Have K2-D2 Execute this node";
            else tooltip = "Launch K2-D2";

            // Get K2-D2 buton icon
            // k2d2_button_tex = AssetManager.GetAsset<Texture2D>($"{ResonantOrbitCalculatorPlugin.Instance.SpaceWarpMetadata.ModID}/images/k2d2_big_icon.png");
            // k2d2_button_tex_con = new GUIContent(k2d2_button_tex, tooltip);

            k2d2Type = Type.GetType($"K2D2.K2D2_Plugin, {K2D2_Plugin.ModGuid}");
            k2d2PropertyInfo = k2d2Type!.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            k2d2Instance = k2d2PropertyInfo.GetValue(null);
            k2d2ToggleMethodInfo = k2d2PropertyInfo!.PropertyType.GetMethod("ToggleAppBarButton");
            k2d2FlyNodeMethodInfo = k2d2PropertyInfo!.PropertyType.GetMethod("FlyNode");
            k2d2GetStatusMethodInfo = k2d2PropertyInfo!.PropertyType.GetMethod("GetStatus");
        }
        // else K2D2Loaded = false;
        Logger.LogInfo($"K2D2Loaded = {K2D2Loaded}");

        Logger.LogInfo($"NodeManagerPlugin.ModGuid = {NodeManagerPlugin.ModGuid}");
        if (Chainloader.PluginInfos.TryGetValue(NodeManagerPlugin.ModGuid, out NM_info))
        {
            NM_info = Chainloader.PluginInfos[NodeManagerPlugin.ModGuid];

            NMLoaded = true;
            Logger.LogInfo("Node Manager installed and available");
            Logger.LogInfo($"NodeManager = {NMLoaded}");
            nmMinVersion = new Version(0, 5, 3);
            nmVerCheck = NM_info.Metadata.Version.CompareTo(nmMinVersion);
            Logger.LogInfo($"nmVerCheck = {nmVerCheck}");

            nmType = Type.GetType($"NodeManager.NodeManagerPlugin, {NodeManagerPlugin.ModGuid}");
            nmPropertyInfo = nmType!.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            nmInstance = nmPropertyInfo.GetValue(null);
            nmDeleteNodesMethodInfo = nmPropertyInfo!.PropertyType.GetMethod("DeleteNodes");
        }
        // else K2D2Loaded = false;
        Logger.LogInfo($"NMLoaded = {NMLoaded}");
    }

    public bool SetNewPe(double burnUT, double newPe, double burnOffsetFactor = -0.5)
    {
        bool result = false;
        if (FPLoaded)
        {
            // Reflections method to attempt the same thing more cleanly
            if (fpVerCheck < 0)
            {
                result = (bool)fpNewApMethodInfo!.Invoke(fpInstance, new object[] { burnUT, newPe, burnOffsetFactor });
            }
            else
            {
                result = (bool)fpNewPeMethodInfo!.Invoke(fpInstance, new object[] { burnUT, newPe, burnOffsetFactor });
                // checkK2D2status = true;
                // Extend the status time to encompass the maneuver
                // ResonantOrbitCalculatorPlugin.Instance.statusTime = ResonantOrbitCalculatorPlugin.Instance.currentNode.Time + ResonantOrbitCalculatorPlugin.Instance.currentNode.BurnDuration;
                // ResonantOrbitCalculatorPlugin.Instance.statusText = ResonantOrbitCalculatorPlugin.Instance.maneuver;
            }
        }
        return result;
    }

    public bool SetNewAp(double burnUT, double newAp, double burnOffsetFactor = -0.5)
    {
        bool result = false;
        if (FPLoaded)
        {
            // Reflections method to attempt the same thing more cleanly
            if (fpVerCheck < 0)
            {
                result = (bool)fpNewApMethodInfo!.Invoke(fpInstance, new object[] { burnUT, newAp, burnOffsetFactor });
            }
            else
            {
                result = (bool)fpNewApMethodInfo!.Invoke(fpInstance, new object[] { burnUT, newAp, burnOffsetFactor });
                // checkK2D2status = true;
                // Extend the status time to encompass the maneuver
                // ResonantOrbitCalculatorPlugin.Instance.statusTime = ResonantOrbitCalculatorPlugin.Instance.currentNode.Time + ResonantOrbitCalculatorPlugin.Instance.currentNode.BurnDuration;
                // ResonantOrbitCalculatorPlugin.Instance.statusText = ResonantOrbitCalculatorPlugin.Instance.maneuver;
            }
        }
        return result;
    }


    public void callMNC()
    {
        if (MNCLoaded && mncVerCheck >= 0)
        {
            mncLaunchMNCMethodInfo!.Invoke(mncInstance, null);
        }
    }

    public void callK2D2()
    {
        if (K2D2Loaded)
        {
            // Reflections method to attempt the same thing more cleanly
            if (k2d2VerCheck < 0)
            {
                k2d2ToggleMethodInfo!.Invoke(k2d2Instance, new object[] { true });
            }
            else
            {
                k2d2FlyNodeMethodInfo!.Invoke(k2d2Instance, null);
                checkK2D2status = true;
                // Extend the status time to encompass the maneuver
                // ResonantOrbitCalculatorPlugin.Instance.statusTime = ResonantOrbitCalculatorPlugin.Instance.currentNode.Time + ResonantOrbitCalculatorPlugin.Instance.currentNode.BurnDuration;
                // ResonantOrbitCalculatorPlugin.Instance.statusText = ResonantOrbitCalculatorPlugin.Instance.maneuver;
            }
        }
    }

    private void getK2D2Status()
    {
        if (K2D2Loaded)
        {
            if (k2d2VerCheck >= 0)
            {
                k2d2Status = (string)k2d2GetStatusMethodInfo!.Invoke(k2d2Instance, null);

                if (k2d2Status == "Done" && NMLoaded)
                {
                    if (NodeManagerPlugin.Instance.currentNode.Time < GameManager.Instance.Game.UniverseModel.UniverseTime)
                    {
                        NodeManagerPlugin.Instance.DeleteNodes(0);
                    }
                    checkK2D2status = false;
                }
            }
        }
    }

    public void OnGUI(ManeuverNodeData currentNode)
    {
        GUILayout.BeginHorizontal();
        if (ResonantOrbitCalculator.UI.UI_Tools.BigIconButton(ROCStyles.fp_icon))
            ResonantOrbitCalculatorPlugin.Instance.MakeNode();

        if (MNCLoaded && mncVerCheck >= 0)
        {
            GUILayout.FlexibleSpace();
            if (ResonantOrbitCalculator.UI.UI_Tools.BigIconButton(ROCStyles.mnc_icon))
                callMNC();
        }

        if (K2D2Loaded && NMLoaded)
        {
            if (NodeManagerPlugin.Instance.currentNode != null)
            {
                GUILayout.FlexibleSpace();
                if (ResonantOrbitCalculator.UI.UI_Tools.BigIconButton(ROCStyles.k2d2_icon))
                    callK2D2();
            }
        }
        GUILayout.EndHorizontal();

        if (checkK2D2status)
        {
            getK2D2Status();
            GUILayout.BeginHorizontal();
            ResonantOrbitCalculator.UI.UI_Tools.Label($"K2D2: {k2d2Status}");
            GUILayout.EndHorizontal();
        }
    }
}