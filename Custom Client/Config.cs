using System.Text.Json;
using System;
using System.IO;
using System.Text.Json;

namespace SonicHybridUltimate
{
    public class Config
    {
        public string RSDKDataPath { get; set; } = "Hybrid-RSDK Main/Data";
        public string OxygenPath { get; set; } = "Sonic 3 AIR Main/Oxygen";
        public bool EnableDebugLogging { get; set; } = true;
        public bool AutoTransition { get; set; } = true;
        public int UpdateIntervalMs { get; set; } = 16; // ~60 FPS

        public static Config Load(string configPath = "config.json")
        {
            try
            {
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<Config>(json);
                    return config ?? new Config();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load config: {ex.Message}");
            }

            return new Config();
        }

        public void Save(string configPath = "config.json")
        {
            try
            {
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save config: {ex.Message}");
            }
        }

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
