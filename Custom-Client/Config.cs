using System.Text.Json;

namespace SonicHybridUltimate.CustomClient
{
    public class Config
    {
        public string RSDKPath { get; set; } = "RSDKv4/RSDKv4.exe";
        public string Sonic3Path { get; set; } = "Sonic3AIR/Sonic3AIR.exe";
        public bool EnableDebugLogging { get; set; } = false;
        public Dictionary<string, string> TransitionTriggers { get; set; } = new()
        {
            { "SONIC1_TO_CD", "Final Zone Complete" },
            { "CD_TO_SONIC2", "Metallic Madness 3 Complete" },
            { "SONIC2_TO_SONIC3", "Death Egg Zone Complete" }
        };

        private static readonly string ConfigPath = "config.json";

        public static Config Load()
        {
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<Config>(json) ?? new Config();
            }
            return new Config();
        }

        public void Save()
        {
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
    }
}
