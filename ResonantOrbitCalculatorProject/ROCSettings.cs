using ResonantOrbitCalculator.Tools;


namespace ResonantOrbitCalculator;

public class ROCSettings
{
    public static SettingsFile s_settings_file = null;
    public static string s_settings_path;

    public static void Init(string settings_path)
    {
        s_settings_file = new SettingsFile(settings_path);
    }

    public static int window_x_pos
    {
        get => s_settings_file.GetInt("window_x_pos", 70);
        set { s_settings_file.SetInt("window_x_pos", value); }
    }

    public static int window_y_pos
    {
        get => s_settings_file.GetInt("window_y_pos", 50);
        set { s_settings_file.SetInt("window_y_pos", value); }
    }

    public static int num_sats
    {
        get => s_settings_file.GetInt("num_sats", 3);
        set { s_settings_file.SetInt("num_sats", value); }
    }

    public static int num_orb
    {
        get => s_settings_file.GetInt("num_orb", 1);
        set { s_settings_file.SetInt("num_orb", value); }
    }

    public static double tgt_altitude_km
    {
        get => s_settings_file.GetDouble("tgt_altitude_km", 600);
        set { s_settings_file.SetDouble("tgt_altitude_km", value); }
    }

    public static double occ_mod_atm
    {
        get => s_settings_file.GetDouble("occ_mod_atm", 0.75);
        set { s_settings_file.SetDouble("occ_mod_atm", value); }
    }

    public static double occ_mod_vac
    {
        get => s_settings_file.GetDouble("occ_mod_vac", 0.9);
        set { s_settings_file.SetDouble("occ_mod_vac", value); }
    }

    public static double target_lan_deg
    {
        get => s_settings_file.GetDouble("target_lan_deg", 0);
        set { s_settings_file.SetDouble("target_lan_deg", value); }
    }

    public static double interceptT
    {
        get => s_settings_file.GetDouble("interceptT", 0);
        set { s_settings_file.SetDouble("interceptT", value); }
    }

    public static double timeOffset
    {
        get => s_settings_file.GetDouble("timeOffset", 30);
        set { s_settings_file.SetDouble("timeOffset", value); }
    }
}