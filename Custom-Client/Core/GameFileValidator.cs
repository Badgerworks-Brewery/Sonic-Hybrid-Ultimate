using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;

namespace SonicHybridUltimate.Core
{
    /// <summary>
    /// Validates game files and provides helpful error messages
    /// </summary>
    public static class GameFileValidator
    {
        private static string ExeDirectory => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
        private static string GameDataDir => Path.Combine(ExeDirectory, "GameData");

        public class ValidationResult
        {
            public bool IsValid { get; set; }
            public List<string> MissingFiles { get; set; } = new List<string>();
            public List<string> FoundFiles { get; set; } = new List<string>();
            public string ErrorMessage { get; set; } = string.Empty;
            public string HelpMessage { get; set; } = string.Empty;
        }

        /// <summary>
        /// Check if all required game files exist
        /// </summary>
        public static ValidationResult ValidateGameFiles()
        {
            var result = new ValidationResult { IsValid = true };
            
            // Check for GameData directory
            bool hasGameDataDir = Directory.Exists(GameDataDir);
            
            // Required files
            var requiredFiles = new Dictionary<string, string>
            {
                { "sonic1.rsdk", "Sonic the Hedgehog 1 (mobile/remaster)" },
                { "sonic2.rsdk", "Sonic the Hedgehog 2 (mobile/remaster)" },
                { "soniccd.rsdk", "Sonic CD (mobile/remaster)" }
            };
            
            // Optional files
            var optionalFiles = new Dictionary<string, string>
            {
                { "sonic3.bin", "Sonic 3 & Knuckles ROM" }
            };
            
            // Check each file
            foreach (var file in requiredFiles)
            {
                string filePath = Path.Combine(GameDataDir, file.Key);
                if (File.Exists(filePath))
                {
                    result.FoundFiles.Add($"{file.Key} ({file.Value})");
                }
                else
                {
                    result.MissingFiles.Add($"{file.Key} ({file.Value})");
                    result.IsValid = false;
                }
            }
            
            // Check optional files
            foreach (var file in optionalFiles)
            {
                string filePath = Path.Combine(GameDataDir, file.Key);
                if (File.Exists(filePath))
                {
                    result.FoundFiles.Add($"{file.Key} ({file.Value})");
                }
            }
            
            // Generate error and help messages
            if (!result.IsValid)
            {
                result.ErrorMessage = GenerateErrorMessage(result.MissingFiles, hasGameDataDir);
                result.HelpMessage = GenerateHelpMessage();
            }
            
            return result;
        }

        private static string GenerateErrorMessage(List<string> missingFiles, bool hasGameDataDir)
        {
            var message = "Missing required game files!\n\n";
            
            if (!hasGameDataDir)
            {
                message += "The 'GameData' folder was not found.\n";
                message += $"Expected location: {GameDataDir}\n\n";
                message += "Please create this folder and add your game files.\n\n";
            }
            
            message += "Missing files:\n";
            foreach (var file in missingFiles)
            {
                message += $"  ✗ {file}\n";
            }
            
            return message;
        }

        private static string GenerateHelpMessage()
        {
            return 
                "HOW TO ADD GAME FILES:\n" +
                "━━━━━━━━━━━━━━━━━━━━━━\n\n" +
                
                $"1. Create folder: {GameDataDir}\n\n" +
                
                "2. Copy your legally obtained game files:\n" +
                "   • sonic1.rsdk - From Sonic 1 mobile/remaster\n" +
                "   • sonic2.rsdk - From Sonic 2 mobile/remaster\n" +
                "   • soniccd.rsdk - From Sonic CD mobile/remaster\n" +
                "   • sonic3.bin - Sonic 3 & Knuckles ROM (optional)\n\n" +
                
                "WHERE TO GET THESE FILES:\n" +
                "━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
                
                "Purchase from:\n" +
                "  • Steam (PC versions)\n" +
                "  • Google Play Store (Android)\n" +
                "  • Apple App Store (iOS)\n\n" +
                
                "For mobile versions:\n" +
                "  1. Install the game on your device\n" +
                "  2. Extract the Data.rsdk file from the app\n" +
                "  3. Rename to sonic1.rsdk, sonic2.rsdk, or soniccd.rsdk\n" +
                "  4. Copy to the GameData folder\n\n" +
                
                "For Sonic 3 & Knuckles:\n" +
                "  1. You need a legally obtained ROM file\n" +
                "  2. Rename it to sonic3.bin\n" +
                "  3. Copy to the GameData folder\n\n" +
                
                "⚠️  IMPORTANT:\n" +
                "Due to copyright, we cannot provide game files.\n" +
                "You must obtain them from your own purchased copies.\n\n" +
                
                "After adding files, restart Sonic Hybrid Ultimate.";
        }

        /// <summary>
        /// Check if native libraries are available
        /// </summary>
        public static ValidationResult ValidateNativeLibraries()
        {
            var result = new ValidationResult { IsValid = true };
            
            var requiredLibs = new Dictionary<string, string>
            {
                { "RSDKv4", "RSDK Engine (for Sonic 1, 2, CD)" },
                { "OxygenEngine", "Oxygen Engine (for Sonic 3 & Knuckles)" }
            };
            
            foreach (var lib in requiredLibs)
            {
                bool exists = Engines.NativeLibraryResolver.IsLibraryAvailable(lib.Key);
                if (exists)
                {
                    result.FoundFiles.Add($"{lib.Key} - {lib.Value}");
                }
                else
                {
                    result.MissingFiles.Add($"{lib.Key} - {lib.Value}");
                    result.IsValid = false;
                }
            }
            
            if (!result.IsValid)
            {
                result.ErrorMessage = GenerateNativeLibErrorMessage(result.MissingFiles);
                result.HelpMessage = GenerateNativeLibHelpMessage();
            }
            
            return result;
        }

        private static string GenerateNativeLibErrorMessage(List<string> missingLibs)
        {
            var message = "Missing native libraries!\n\n";
            message += "The following game engines could not be loaded:\n\n";
            
            foreach (var lib in missingLibs)
            {
                message += $"  ✗ {lib}\n";
            }
            
            return message;
        }

        private static string GenerateNativeLibHelpMessage()
        {
            return 
                "HOW TO FIX MISSING LIBRARIES:\n" +
                "━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
                
                "The native engine libraries are missing.\n" +
                "This usually means the build process did not complete.\n\n" +
                
                "WINDOWS:\n" +
                "  Run: build_native_libs.bat\n" +
                "  Then rebuild the project.\n\n" +
                
                "LINUX/macOS:\n" +
                "  Run: ./build_native_libs.sh\n" +
                "  Then rebuild the project.\n\n" +
                
                "OR use the packaging script:\n" +
                "  Run: ./package.sh\n" +
                "  This builds everything and creates a distribution.\n\n" +
                
                "If you downloaded a pre-built release:\n" +
                "  Make sure all DLL/SO files are in the same folder\n" +
                "  as the executable. If files are missing, re-download\n" +
                "  or rebuild from source.";
        }

        /// <summary>
        /// Show a comprehensive startup check
        /// </summary>
        public static void ShowStartupCheck(Action<string> logAction)
        {
            logAction("╔══════════════════════════════════════════╗");
            logAction("║   SONIC HYBRID ULTIMATE - STARTUP CHECK   ║");
            logAction("╚══════════════════════════════════════════╝");
            logAction("");
            
            // Check native libraries
            logAction("Checking native libraries...");
            var libResult = ValidateNativeLibraries();
            
            if (libResult.IsValid)
            {
                logAction("✓ All native libraries found");
                foreach (var lib in libResult.FoundFiles)
                {
                    logAction($"  • {lib}");
                }
            }
            else
            {
                logAction("✗ Missing native libraries:");
                foreach (var lib in libResult.MissingFiles)
                {
                    logAction($"  • {lib}");
                }
                logAction("");
                logAction("⚠️  The game may not work properly without these libraries.");
            }
            logAction("");
            
            // Check game files
            logAction("Checking game files...");
            var fileResult = ValidateGameFiles();
            
            if (fileResult.FoundFiles.Any())
            {
                logAction("✓ Found game files:");
                foreach (var file in fileResult.FoundFiles)
                {
                    logAction($"  • {file}");
                }
            }
            
            if (fileResult.MissingFiles.Any())
            {
                logAction("");
                logAction("✗ Missing game files:");
                foreach (var file in fileResult.MissingFiles)
                {
                    logAction($"  • {file}");
                }
                logAction("");
                logAction($"📁 Expected location: {GameDataDir}");
            }
            
            logAction("");
            logAction("════════════════════════════════════════════");
            
            if (fileResult.IsValid && libResult.IsValid)
            {
                logAction("✓ All checks passed! Ready to play.");
            }
            else
            {
                logAction("⚠️  Some files are missing. See messages above.");
            }
            
            logAction("════════════════════════════════════════════");
            logAction("");
        }
    }
}
