using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace SonicHybridUltimate.Core
{
    /// <summary>
    /// Automatically detects and registers RSDK and ROM files from the filesystem
    /// </summary>
    public class GameFileAutoDetector
    {
        private readonly ILogger<GameFileAutoDetector> _logger;
        private readonly UserSettings _userSettings;
        
        public class DetectedGame
        {
            public string FilePath { get; set; } = string.Empty;
            public string GameType { get; set; } = string.Empty;  // "sonic1", "sonic2", "soniccd", "sonic3"
            public string DisplayName { get; set; } = string.Empty;
            public long FileSize { get; set; }
            public bool IsVerified { get; set; }
        }
        
        public GameFileAutoDetector(ILogger<GameFileAutoDetector> logger, UserSettings userSettings)
        {
            _logger = logger;
            _userSettings = userSettings;
        }
        
        /// <summary>
        /// Auto-detect all game files in common locations
        /// </summary>
        public List<DetectedGame> DetectAllGames()
        {
            var detectedGames = new List<DetectedGame>();
            
            _logger.LogInformation("Starting auto-detection of game files...");
            
            // Search locations in priority order
            var searchLocations = GetSearchLocations();
            
            foreach (var location in searchLocations)
            {
                if (Directory.Exists(location))
                {
                    _logger.LogInformation("Scanning: {Location}", location);
                    var gamesInLocation = ScanDirectory(location);
                    detectedGames.AddRange(gamesInLocation);
                }
            }
            
            // Remove duplicates (keep first occurrence)
            var uniqueGames = new Dictionary<string, DetectedGame>();
            foreach (var game in detectedGames)
            {
                if (!uniqueGames.ContainsKey(game.GameType))
                {
                    uniqueGames[game.GameType] = game;
                }
            }
            
            _logger.LogInformation("Auto-detection complete. Found {Count} games", uniqueGames.Count);
            
            return uniqueGames.Values.ToList();
        }
        
        /// <summary>
        /// Get common search locations for game files
        /// </summary>
        private List<string> GetSearchLocations()
        {
            var locations = new List<string>();
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            
            // PRIORITY 1: User-configured custom paths
            var customPaths = _userSettings.GetCustomPaths();
            if (customPaths.Any())
            {
                _logger.LogInformation("Adding {Count} custom search paths", customPaths.Count);
                locations.AddRange(customPaths);
            }
            
            // PRIORITY 2: GameData folder next to executable (packaged distribution)
            locations.Add(Path.Combine(exeDir, "GameData"));
            
            // 2. Current directory
            locations.Add(Directory.GetCurrentDirectory());
            
            // 3. Executable directory
            locations.Add(exeDir);
            
            // 4. Development paths
            locations.Add(Path.Combine(exeDir, "..", "Hybrid-RSDK-Main", "rsdk-source-data"));
            locations.Add(Path.Combine(exeDir, "Hybrid-RSDK-Main", "rsdk-source-data"));
            locations.Add(Path.Combine(exeDir, "rsdk-source-data"));
            
            // 5. User's Documents folder
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            locations.Add(Path.Combine(documentsPath, "Sonic Hybrid Ultimate"));
            locations.Add(Path.Combine(documentsPath, "SEGA"));
            
            // 6. User's Desktop (common place to drop files)
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            locations.Add(desktopPath);
            
            // 7. Downloads folder
            var downloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            locations.Add(downloadsPath);
            
            // 8. Common game install locations
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                // Windows
                var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                
                locations.Add(Path.Combine(programFilesX86, "Steam", "steamapps", "common"));
                locations.Add(Path.Combine(programFiles, "Steam", "steamapps", "common"));
            }
            else
            {
                // Linux/Mac
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                locations.Add(Path.Combine(home, ".steam", "steam", "steamapps", "common"));
                locations.Add(Path.Combine(home, ".local", "share", "Steam", "steamapps", "common"));
            }
            
            // Normalize and deduplicate paths
            return locations
                .Select(p => Path.GetFullPath(p))
                .Where(Directory.Exists)
                .Distinct()
                .ToList();
        }
        
        /// <summary>
        /// Scan a directory for game files
        /// </summary>
        private List<DetectedGame> ScanDirectory(string directory)
        {
            var games = new List<DetectedGame>();
            
            try
            {
                // Search for .rsdk files
                var rsdkFiles = Directory.GetFiles(directory, "*.rsdk", SearchOption.TopDirectoryOnly);
                foreach (var file in rsdkFiles)
                {
                    var game = IdentifyRSDKFile(file);
                    if (game != null)
                    {
                        games.Add(game);
                        _logger.LogInformation("  Found: {DisplayName} at {FilePath}", game.DisplayName, game.FilePath);
                    }
                }
                
                // Search for .bin files (Sonic 3 ROM)
                var binFiles = Directory.GetFiles(directory, "*.bin", SearchOption.TopDirectoryOnly);
                foreach (var file in binFiles)
                {
                    var game = IdentifyROMFile(file);
                    if (game != null)
                    {
                        games.Add(game);
                        _logger.LogInformation("  Found: {DisplayName} at {FilePath}", game.DisplayName, game.FilePath);
                    }
                }
                
                // Also check for .md and .gen files (Genesis ROMs)
                var romFiles = Directory.GetFiles(directory, "*.md", SearchOption.TopDirectoryOnly)
                    .Concat(Directory.GetFiles(directory, "*.gen", SearchOption.TopDirectoryOnly));
                foreach (var file in romFiles)
                {
                    var game = IdentifyROMFile(file);
                    if (game != null)
                    {
                        games.Add(game);
                        _logger.LogInformation("  Found: {DisplayName} at {FilePath}", game.DisplayName, game.FilePath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error scanning directory: {Directory}", directory);
            }
            
            return games;
        }
        
        /// <summary>
        /// Identify an RSDK file by analyzing its contents
        /// </summary>
        private DetectedGame? IdentifyRSDKFile(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists || fileInfo.Length < 1024)
                    return null;
                
                var fileName = Path.GetFileNameWithoutExtension(filePath).ToLower();
                
                // Check file name first
                if (fileName.Contains("sonic1") || fileName.Contains("sonic_1") || fileName == "data")
                {
                    // Try to verify it's actually Sonic 1 by checking content
                    if (VerifyRSDKContent(filePath, "Sonic1"))
                    {
                        return new DetectedGame
                        {
                            FilePath = filePath,
                            GameType = "sonic1",
                            DisplayName = "Sonic the Hedgehog 1",
                            FileSize = fileInfo.Length,
                            IsVerified = true
                        };
                    }
                }
                
                if (fileName.Contains("sonic2") || fileName.Contains("sonic_2"))
                {
                    if (VerifyRSDKContent(filePath, "Sonic2"))
                    {
                        return new DetectedGame
                        {
                            FilePath = filePath,
                            GameType = "sonic2",
                            DisplayName = "Sonic the Hedgehog 2",
                            FileSize = fileInfo.Length,
                            IsVerified = true
                        };
                    }
                }
                
                if (fileName.Contains("soniccd") || fileName.Contains("sonic_cd") || fileName.Contains("cd"))
                {
                    if (VerifyRSDKContent(filePath, "SonicCD"))
                    {
                        return new DetectedGame
                        {
                            FilePath = filePath,
                            GameType = "soniccd",
                            DisplayName = "Sonic CD",
                            FileSize = fileInfo.Length,
                            IsVerified = true
                        };
                    }
                }
                
                // If filename doesn't match, try to identify by analyzing content
                var gameType = IdentifyRSDKByContent(filePath);
                if (!string.IsNullOrEmpty(gameType))
                {
                    return new DetectedGame
                    {
                        FilePath = filePath,
                        GameType = gameType,
                        DisplayName = GetDisplayName(gameType),
                        FileSize = fileInfo.Length,
                        IsVerified = true
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error identifying RSDK file: {FilePath}", filePath);
            }
            
            return null;
        }
        
        /// <summary>
        /// Identify a ROM file (Sonic 3 & Knuckles)
        /// </summary>
        private DetectedGame? IdentifyROMFile(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists)
                    return null;
                
                var fileName = Path.GetFileNameWithoutExtension(filePath).ToLower();
                
                // Check for Sonic 3 & Knuckles indicators
                if (fileName.Contains("sonic3") || fileName.Contains("sonic_3") || 
                    fileName.Contains("s3k") || fileName.Contains("sonic&knuckles") ||
                    fileName.Contains("sonic_and_knuckles"))
                {
                    // Verify file size (S3&K ROM should be around 4MB)
                    if (fileInfo.Length >= 2 * 1024 * 1024 && fileInfo.Length <= 8 * 1024 * 1024)
                    {
                        return new DetectedGame
                        {
                            FilePath = filePath,
                            GameType = "sonic3",
                            DisplayName = "Sonic 3 & Knuckles",
                            FileSize = fileInfo.Length,
                            IsVerified = VerifyROMContent(filePath)
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error identifying ROM file: {FilePath}", filePath);
            }
            
            return null;
        }
        
        /// <summary>
        /// Verify RSDK file content by checking for known strings
        /// </summary>
        private bool VerifyRSDKContent(string filePath, string expectedGame)
        {
            try
            {
                using var fs = File.OpenRead(filePath);
                using var reader = new BinaryReader(fs);
                
                // Read first 4KB to look for game identifiers
                var header = reader.ReadBytes(Math.Min(4096, (int)fs.Length));
                var headerText = System.Text.Encoding.ASCII.GetString(header);
                
                switch (expectedGame)
                {
                    case "Sonic1":
                        return headerText.Contains("GHZ") || headerText.Contains("Green Hill");
                    case "Sonic2":
                        return headerText.Contains("EHZ") || headerText.Contains("Emerald Hill");
                    case "SonicCD":
                        return headerText.Contains("PPZ") || headerText.Contains("Palmtree Panic");
                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Identify RSDK file by analyzing its content
        /// </summary>
        private string IdentifyRSDKByContent(string filePath)
        {
            try
            {
                using var fs = File.OpenRead(filePath);
                using var reader = new BinaryReader(fs);
                
                var header = reader.ReadBytes(Math.Min(8192, (int)fs.Length));
                var headerText = System.Text.Encoding.ASCII.GetString(header);
                
                // Look for distinctive level names
                if (headerText.Contains("GHZ") || headerText.Contains("Green Hill"))
                    return "sonic1";
                if (headerText.Contains("EHZ") || headerText.Contains("Emerald Hill"))
                    return "sonic2";
                if (headerText.Contains("PPZ") || headerText.Contains("Palmtree Panic"))
                    return "soniccd";
            }
            catch
            {
                // Ignore errors
            }
            
            return string.Empty;
        }
        
        /// <summary>
        /// Verify ROM file is a valid Genesis ROM
        /// </summary>
        private bool VerifyROMContent(string filePath)
        {
            try
            {
                using var fs = File.OpenRead(filePath);
                using var reader = new BinaryReader(fs);
                
                // Genesis ROMs typically start with specific headers
                var header = reader.ReadBytes(512);
                
                // Check for "SEGA" string at offset 0x100
                if (header.Length >= 0x110)
                {
                    var segaString = System.Text.Encoding.ASCII.GetString(header, 0x100, 4);
                    if (segaString == "SEGA")
                        return true;
                }
                
                // Also acceptable if file size is reasonable for a Genesis ROM
                return fs.Length >= 2 * 1024 * 1024 && fs.Length <= 8 * 1024 * 1024;
            }
            catch
            {
                return false;
            }
        }
        
        private string GetDisplayName(string gameType)
        {
            return gameType switch
            {
                "sonic1" => "Sonic the Hedgehog 1",
                "sonic2" => "Sonic the Hedgehog 2",
                "soniccd" => "Sonic CD",
                "sonic3" => "Sonic 3 & Knuckles",
                _ => "Unknown Game"
            };
        }
        
        /// <summary>
        /// Copy detected files to the GameData folder for organized storage
        /// </summary>
        public bool ImportDetectedGame(DetectedGame game, string targetDirectory)
        {
            try
            {
                if (!Directory.Exists(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }
                
                var targetFileName = game.GameType switch
                {
                    "sonic1" => "sonic1.rsdk",
                    "sonic2" => "sonic2.rsdk",
                    "soniccd" => "soniccd.rsdk",
                    "sonic3" => "sonic3.bin",
                    _ => Path.GetFileName(game.FilePath)
                };
                
                var targetPath = Path.Combine(targetDirectory, targetFileName);
                
                // Don't copy if source and target are the same
                if (Path.GetFullPath(game.FilePath) == Path.GetFullPath(targetPath))
                {
                    _logger.LogInformation("File already in correct location: {FilePath}", targetPath);
                    return true;
                }
                
                // Copy the file
                File.Copy(game.FilePath, targetPath, overwrite: true);
                _logger.LogInformation("Imported {GameType} to {TargetPath}", game.GameType, targetPath);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import game file: {FilePath}", game.FilePath);
                return false;
            }
        }
    }
}
