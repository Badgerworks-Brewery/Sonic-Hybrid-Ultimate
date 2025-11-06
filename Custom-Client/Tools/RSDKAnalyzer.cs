using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace SonicHybridUltimate.Tools
{
    public class RSDKAnalyzer
    {
        private readonly ILogger<RSDKAnalyzer> _logger;
        private readonly Dictionary<string, GameInfo> _gameDatabase;

        public RSDKAnalyzer(ILogger<RSDKAnalyzer> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _gameDatabase = InitializeGameDatabase();
        }

        public GameInfo? AnalyzeRSDKFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    _logger.LogError("RSDK file not found: {FilePath}", filePath);
                    return null;
                }

                _logger.LogInformation("Analyzing RSDK file: {FilePath}", filePath);

                using var stream = File.OpenRead(filePath);
                using var reader = new BinaryReader(stream);

                // Read RSDK header
                var signature = reader.ReadBytes(4);
                var version = reader.ReadByte();
                var gameType = reader.ReadByte();

                // Determine game based on signature and metadata
                var gameInfo = DetermineGame(signature, version, gameType, filePath);

                if (gameInfo != null)
                {
                    _logger.LogInformation("Detected game: {GameName} (Version: {Version})",
                        gameInfo.Name, gameInfo.Version);
                }
                else
                {
                    _logger.LogWarning("Could not determine game type for: {FilePath}", filePath);
                }

                return gameInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing RSDK file: {FilePath}", filePath);
                return null;
            }
        }

        private GameInfo? DetermineGame(byte[] signature, byte version, byte gameType, string filePath)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath).ToLowerInvariant();

            if (_gameDatabase.TryGetValue(fileName, out var gameInfo))
            {
                return gameInfo;
            }

            // Fallback detection based on file size or other heuristics
            var fileSize = new FileInfo(filePath).Length;

            // These are approximate sizes - adjust based on actual game files
            return fileSize switch
            {
                > 50_000_000 => new GameInfo("Sonic 2", "RSDKv4", "sonic2"),
                > 30_000_000 => new GameInfo("Sonic CD", "RSDKv3", "soniccd"),
                > 20_000_000 => new GameInfo("Sonic 1", "RSDKv4", "sonic1"),
                _ => null
            };
        }

        private Dictionary<string, GameInfo> InitializeGameDatabase()
        {
            return new Dictionary<string, GameInfo>
            {
                ["sonic1"] = new GameInfo("Sonic the Hedgehog", "RSDKv4", "sonic1"),
                ["sonic2"] = new GameInfo("Sonic the Hedgehog 2", "RSDKv4", "sonic2"),
                ["soniccd"] = new GameInfo("Sonic CD", "RSDKv3", "soniccd"),
                ["sonic3"] = new GameInfo("Sonic 3 & Knuckles", "Oxygen", "sonic3")
            };
        }
    }

    public record GameInfo(string Name, string Version, string Identifier);
}
