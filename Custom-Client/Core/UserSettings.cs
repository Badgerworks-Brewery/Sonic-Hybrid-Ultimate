using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace SonicHybridUltimate.Core
{
    /// <summary>
    /// Manages user-configured settings including custom search paths
    /// </summary>
    public class UserSettings
    {
        private static readonly string SettingsFileName = "settings.json";
        private static string SettingsFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SonicHybridUltimate",
            SettingsFileName
        );

        public class Settings
        {
            /// <summary>
            /// Custom paths where the user stores their game files
            /// </summary>
            public List<string> CustomSearchPaths { get; set; } = new List<string>();
            
            /// <summary>
            /// Last used paths for quick access
            /// </summary>
            public Dictionary<string, string> LastUsedPaths { get; set; } = new Dictionary<string, string>();
            
            /// <summary>
            /// Auto-import detected games to GameData folder
            /// </summary>
            public bool AutoImportDetectedGames { get; set; } = true;
            
            /// <summary>
            /// Show startup scan results
            /// </summary>
            public bool ShowStartupScan { get; set; } = true;
            
            /// <summary>
            /// Remember window position and size
            /// </summary>
            public WindowSettings Window { get; set; } = new WindowSettings();
        }

        public class WindowSettings
        {
            public int X { get; set; } = -1;
            public int Y { get; set; } = -1;
            public int Width { get; set; } = 1024;
            public int Height { get; set; } = 768;
        }

        private Settings _currentSettings = new Settings();

        /// <summary>
        /// Load settings from disk
        /// </summary>
        public Settings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<Settings>(json);
                    if (settings != null)
                    {
                        _currentSettings = settings;
                        
                        // Clean up invalid paths
                        _currentSettings.CustomSearchPaths = _currentSettings.CustomSearchPaths
                            .Where(Directory.Exists)
                            .Distinct()
                            .ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settings: {ex.Message}");
                _currentSettings = new Settings();
            }

            return _currentSettings;
        }

        /// <summary>
        /// Save settings to disk
        /// </summary>
        public bool Save(Settings settings)
        {
            try
            {
                _currentSettings = settings;
                
                // Ensure directory exists
                var directory = Path.GetDirectoryName(SettingsFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                
                var json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(SettingsFilePath, json);
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving settings: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Add a custom search path
        /// </summary>
        public bool AddCustomPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            if (!Directory.Exists(path))
                return false;

            var fullPath = Path.GetFullPath(path);
            
            if (!_currentSettings.CustomSearchPaths.Contains(fullPath))
            {
                _currentSettings.CustomSearchPaths.Add(fullPath);
                Save(_currentSettings);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Remove a custom search path
        /// </summary>
        public bool RemoveCustomPath(string path)
        {
            var fullPath = Path.GetFullPath(path);
            var removed = _currentSettings.CustomSearchPaths.Remove(fullPath);
            
            if (removed)
            {
                Save(_currentSettings);
            }
            
            return removed;
        }

        /// <summary>
        /// Get all custom search paths
        /// </summary>
        public List<string> GetCustomPaths()
        {
            return _currentSettings.CustomSearchPaths.ToList();
        }

        /// <summary>
        /// Clear all custom search paths
        /// </summary>
        public void ClearCustomPaths()
        {
            _currentSettings.CustomSearchPaths.Clear();
            Save(_currentSettings);
        }

        /// <summary>
        /// Remember the last path used for a specific game
        /// </summary>
        public void RememberLastPath(string gameType, string filePath)
        {
            _currentSettings.LastUsedPaths[gameType] = filePath;
            Save(_currentSettings);
        }

        /// <summary>
        /// Get the last used path for a game
        /// </summary>
        public string? GetLastPath(string gameType)
        {
            if (_currentSettings.LastUsedPaths.TryGetValue(gameType, out var path))
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }
            
            return null;
        }

        /// <summary>
        /// Get current settings
        /// </summary>
        public Settings Current => _currentSettings;

        /// <summary>
        /// Reset to default settings
        /// </summary>
        public void Reset()
        {
            _currentSettings = new Settings();
            Save(_currentSettings);
        }
    }
}
