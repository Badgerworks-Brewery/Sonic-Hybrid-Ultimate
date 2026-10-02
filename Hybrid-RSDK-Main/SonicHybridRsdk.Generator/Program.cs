using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static SonicHybridRsdk.Generator.Global;
using static SonicHybridRsdk.Generator.RsdkGenericImporter;
using static SonicHybridRsdk.Generator.RsdkSonicCdImporter;

namespace SonicHybridRsdk.Generator
{
    enum StageType
    {
        StagesPresentation,
        StagesRegular,
        StagesSpecial,
    }

    class Context
    {
        public string SrcPath { get; init; }
        public string DstPath { get; init; }
        public IGameConfig SrcConfig { get; init; }
        public IGameConfig DstConfig { get; init; }
        public Dictionary<int, GameObject> SrcObjects { get; init; }
        public Dictionary<string, int> DstObjects { get; init; }
        public Dictionary<string, string> Replacements { get; init; }
    }

    public class Program
    {
        static void Main(string[] args) => Generate(args[0], args[1]);

        /// <summary>
        /// Copies the three games' spritesheets, giving each game its own copy of
        /// any file the games share a path for.
        /// </summary>
        /// <remarks>
        /// Copying the Sprites folder wholesale in S1, CD, S2 order silently lets
        /// the last writer win. 18 paths exist in more than one game - every
        /// player sheet, plus Global/Items*, LevelSelect/Icons, Ending/*, Title
        /// and Special/Objects - so Sonic 1 ended up drawing itself with Sonic 2's
        /// sprites, which are laid out differently and simply look wrong.
        ///
        /// Object scripts reference sheets by path, so the fix is to stop the
        /// overwrite rather than to guess which sheet "should" win: the earliest
        /// game keeps the original path and later games get an S1/S2-suffixed one.
        /// Which game a stage belongs to is decided by the bytecode that runs it,
        /// not here, so this function only guarantees nothing is lost.
        ///
        /// A path is treated as shared when the files actually differ, so
        /// byte-identical sheets (Global/Items3.gif, LevelSelect/Text.gif,
        /// Players/KTE2.gif) are not needlessly duplicated.
        /// </remarks>
        private static void CopySprites(
            string sonic1Path, string sonicCdPath, string sonic2Path, string sonicHybridPath)
        {
            var games = new (string Path, string Tag)[]
            {
                (sonic1Path, "S1"),
                (sonicCdPath, "CD"),
                (sonic2Path, "S2"),
            };

            var written = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (path, tag) in games)
            {
                var src = Path.Combine(path, "Sprites");
                if (!Directory.Exists(src))
                    continue;

                foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
                {
                    var relative = file.Substring(src.Length).TrimStart(
                        Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                    // Later games get their own copy when an earlier one already
                    // provided a *different* file at this path.
                    var destination = relative;
                    if (written.TryGetValue(relative, out var previous))
                    {
                        var previousBytes = File.ReadAllBytes(previous);
                        var currentBytes = File.ReadAllBytes(file);
                        if (previousBytes.SequenceEqual(currentBytes))
                            continue; // identical, so sharing is harmless

                        var extension = Path.GetExtension(relative);
                        var stem = relative.Substring(0, relative.Length - extension.Length);
                        destination = $"{stem}_{tag}{extension}";
                    }

                    var destinationPath = Path.Combine(sonicHybridPath, "Sprites", destination);
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                    File.Copy(file, destinationPath, true);
                    written[relative] = destinationPath;
                }
            }
        }

        public static void CopyResources(string sourceDataRsdk, string destinationDataRsdk)
        {
            var sonic1Path = Path.Combine(sourceDataRsdk, "sonic1/Data");
            var sonicCdPath = Path.Combine(sourceDataRsdk, "soniccd/Data");
            var sonic2Path = Path.Combine(sourceDataRsdk, "sonic2/Data");
            var sonicHybridPath = Path.Combine(destinationDataRsdk, "Data");
            var sonicHybridCustomPath = Path.Combine(destinationDataRsdk, "Data-Custom");

            foreach (var folder in new string[]
            {
                "Animations",
                "Game",
                "Music",
                "Palettes",
                "SoundFX",
            })
            {
                Copy(Path.Combine(sonic1Path, folder), Path.Combine(sonicHybridPath, folder));
                Copy(Path.Combine(sonicCdPath, folder), Path.Combine(sonicHybridPath, folder));
                Copy(Path.Combine(sonic2Path, folder), Path.Combine(sonicHybridPath, folder));
            }

            CopySprites(sonic1Path, sonicCdPath, sonic2Path, sonicHybridPath);

            foreach (var (SourcePath, DestinationPath) in new (string, string)[]
            {
                ("Animations/MetalSonic.Ani", "Animations/MetalSonicBoss.Ani"),
            })
                File.Copy(
                    Path.Combine(sonicCdPath, SourcePath),
                    Path.Combine(sonicHybridPath, DestinationPath),
                    true);

            foreach (var folderPath in Directory.GetDirectories(sonicHybridCustomPath))
            {
                var folderName = Path.GetFileName(folderPath);
                Copy(
                    Path.Combine(sonicHybridCustomPath, folderName),
                    Path.Combine(sonicHybridPath, Path.GetFileName(folderPath)));
            }
        }

        public static void Generate(string sourceDataRsdk, string destinationDataRsdk)
        {
            CopyResources(sourceDataRsdk, destinationDataRsdk);

            var sonic1Path = Path.Combine(sourceDataRsdk, "sonic1/Data");
            var sonicCdPath = Path.Combine(sourceDataRsdk, "soniccd/Data");
            var sonic2Path = Path.Combine(sourceDataRsdk, "sonic2/Data");
            var sonicHybridPath = Path.Combine(destinationDataRsdk, "Data");

            var sonic1Config = OpenRead(Path.Combine(sonic1Path, "Game/GameConfig.bin"), GameConfig.Read);
            var sonicCdConfig = OpenRead(Path.Combine(sonicCdPath, "Game/GameConfig.bin"), GameConfigV3.Read);
            var sonic2Config = OpenRead(Path.Combine(sonic2Path, "Game/GameConfig.bin"), GameConfig.Read);

            var sonicHybridConfig = new GameConfig
            {
                Name = "Sonic Hybrid Ultimate",
                Description = $"Hack by Xeeynamo\n\n{sonic1Config.Description}",
                PaletteData = sonic2Config.PaletteData,
                StagesPresentation = new List<Stage>(),
                StagesRegular = new List<Stage>(),
                StagesBonus = new List<Stage>(),
                StagesSpecial = new List<Stage>(),
            };

            // The object table has to line up index-for-index with the merged bytecode,
            // which is Sonic 2's 39 global scripts followed by Sonic 1's 38. RSDKv4
            // pairs config entry i with script slot i+1 (Scene.cpp:664,691), so
            // deduping by name would shift every later object onto the wrong
            // script.
            //
            // The two games share 33 of their object names - "HUD", "Ring",
            // "Star Post" and so on - but those are *different* objects with
            // different scripts, so both are kept and the later one wins when a
            // name is looked up. Order is: all of Sonic 2's, then all of
            // Sonic 1's.
            var hybridObjects = new List<GameObject>();
            hybridObjects.AddRange(sonic2Config.GameObjects);
            hybridObjects.AddRange(sonic1Config.GameObjects);
            sonicHybridConfig.GameObjects = hybridObjects;

            // Name -> object index. Duplicates resolve to the last (Sonic 1's),
            // so Sonic 1 stages reach Sonic 1 scripts and Sonic 2 stages, whose
            // layouts reference the earlier indices directly, keep theirs.
            var dicHybridObjects = new Dictionary<string, int>();
            for (var i = 0; i < hybridObjects.Count; ++i)
                dicHybridObjects[hybridObjects[i].Name] = i;

            dicHybridObjects["Lamp Post"] = dicHybridObjects["Star Post"]; // Sonic 1
            dicHybridObjects["LampPost"] = dicHybridObjects["Star Post"]; // Sonic CD
            dicHybridObjects["SignPost"] = dicHybridObjects["Sign Post"]; // Sonic CD
            dicHybridObjects["Flower Pod"] = dicHybridObjects["Animal Prison"]; // Sonic CD
            dicHybridObjects["Future Post"] = dicHybridObjects["Star Post"]; // TODO HACK
            dicHybridObjects["Past Post"] = dicHybridObjects["Star Post"]; // TODO HACK
            dicHybridObjects["Transporter"] = dicHybridObjects["Ring"]; // TODO HACK
            dicHybridObjects["Goal Post"] = dicHybridObjects["Ring"]; // TODO HACK
            dicHybridObjects["MSProjector"] = dicHybridObjects["Ring"]; // TODO HACK

            var context1 = new Context
            {
                SrcPath = sonic1Path,
                DstPath = sonicHybridPath,
                SrcConfig = sonic1Config,
                DstConfig = sonicHybridConfig,
                SrcObjects = sonic1Config.GameObjects.Select((x, i) => (Id: i, Obj: x)).ToDictionary(x => x.Id, x => x.Obj),
                DstObjects = dicHybridObjects,
                Replacements = new()
                {
                    ["Special/PlayerObject.txt"] = "Special/PlayerObject1.txt",
                    ["Special/SpecialSetup.txt"] = "Special/SpecialSetup1.txt",
                    ["Special/SpecialFinish.txt"] = "Special/SpecialFinish1.txt",
                    ["Special/ChaosEmerald.txt"] = "Special/ChaosEmerald1.txt",
                }
            };

            var contextCd = new Context
            {
                SrcPath = sonicCdPath,
                DstPath = sonicHybridPath,
                SrcConfig = sonicCdConfig,
                DstConfig = sonicHybridConfig,
                SrcObjects = sonicCdConfig.GameObjects.Select((x, i) => (Id: i, Obj: x)).ToDictionary(x => x.Id, x => x.Obj),
                DstObjects = dicHybridObjects,
                Replacements = new()
                {
                }
            };

            var context2 = new Context
            {
                SrcPath = sonic2Path,
                DstPath = sonicHybridPath,
                SrcConfig = sonic2Config,
                DstConfig = sonicHybridConfig,
                SrcObjects = sonic2Config.GameObjects.Select((x, i) => (Id: i, Obj: x)).ToDictionary(x => x.Id, x => x.Obj),
                DstObjects = dicHybridObjects,
                Replacements = new()
                {
                    ["Special/PlayerObject.txt"] = "Special/PlayerObject2.txt",
                    ["Special/SpecialSetup.txt"] = "Special/SpecialSetup2.txt",
                    ["Special/SpecialFinish.txt"] = "Special/SpecialFinish2.txt",
                    ["Special/ChaosEmerald.txt"] = "Special/ChaosEmerald2.txt",
                }
            };

            var variables = new Dictionary<string, int>();
            foreach (var item in sonic1Config.Variables)
                variables[item.Name] = item.Value;
            foreach (var item in sonicCdConfig.Variables)
                variables[item.Name] = item.Value;
            foreach (var item in sonic2Config.Variables)
                variables[item.Name] = item.Value;
            variables["stage.gameid"] = 0;
            sonicHybridConfig.Variables = variables.Select(x => new Variable { Name = x.Key, Value = x.Value }).ToList();

            sonicHybridConfig.Players = sonic2Config.Players;
            sonicHybridConfig.Players.Add("METAL SONIC");

            sonicHybridConfig.SoundEffects = sonic2Config.SoundEffects;

            UseStageV4(context2, StageType.StagesPresentation, "TITLE SCREEN SONIC 2", 1, "Title", "TitleS2");
            UseStageV4(context2, StageType.StagesPresentation, "ENDING SONIC 2", 1, "Ending", "EndingS2");
            UseStageV4(context2, StageType.StagesPresentation, "STAFF CREDITS SONIC 2", 1, "Credits", "CreditsS2");
            UseStageV4(context2, StageType.StagesPresentation, "LEVEL SELECT SONIC 2", 1, "LSelect", "LSelectS2");
            UseStageV4(context2, StageType.StagesPresentation, "LEVEL SELECT 2P", 2, "LSelect", "LSelectS2");
            UseStageV4(context2, StageType.StagesPresentation, "CONTINUE SCREEN SONIC 1", 1, "Continue", "ContinueS1");

            UseStageV4(context1, StageType.StagesPresentation, "TITLE SCREEN SONIC 1", 1, "Title", "TitleS1");
            UseStageV4(context1, StageType.StagesPresentation, "ENDING SONIC 1", 1, "Ending", "EndingS1");
            UseStageV4(context1, StageType.StagesPresentation, "STAFF CREDITS SONIC 1", 1, "Credits", "CreditsS1");
            UseStageV4(context1, StageType.StagesPresentation, "UNLOCK ALL ACHIEVEMENTS", 2, "Credits", "CreditsS1");
            UseStageV4(context1, StageType.StagesPresentation, "CONTINUE SCREEN SONIC 1", 1, "Continue", "ContinueS1");
            UseStageV4(context1, StageType.StagesPresentation, "LEVEL SELECT SONIC 1", 1, "LSelect", "LSelectS1");

            UseStageV4(context1, StageType.StagesRegular, "GREEN HILL ZONE", 1, "Zone01", "ZoneGHZ");
            UseStageV4(context1, StageType.StagesRegular, "GREEN HILL ZONE", 2, "Zone01", "ZoneGHZ");
            UseStageV4(context1, StageType.StagesRegular, "GREEN HILL ZONE", 3, "Zone01", "ZoneGHZ");
            UseStageV4(context1, StageType.StagesRegular, "MARBLE ZONE", 1, "Zone02", "ZoneMZ");
            UseStageV4(context1, StageType.StagesRegular, "MARBLE ZONE", 2, "Zone02", "ZoneMZ");
            UseStageV4(context1, StageType.StagesRegular, "MARBLE ZONE", 3, "Zone02", "ZoneMZ");
            UseStageV4(context1, StageType.StagesRegular, "SPRING YARD ZONE", 1, "Zone03", "ZoneSYZ");
            UseStageV4(context1, StageType.StagesRegular, "SPRING YARD ZONE", 2, "Zone03", "ZoneSYZ");
            UseStageV4(context1, StageType.StagesRegular, "SPRING YARD ZONE", 3, "Zone03", "ZoneSYZ");
            UseStageV4(context1, StageType.StagesRegular, "LABYRINTH ZONE", 1, "Zone04", "ZoneLZ");
            UseStageV4(context1, StageType.StagesRegular, "LABYRINTH ZONE", 2, "Zone04", "ZoneLZ");
            UseStageV4(context1, StageType.StagesRegular, "LABYRINTH ZONE", 3, "Zone04", "ZoneLZ");
            UseStageV4(context1, StageType.StagesRegular, "STARLIGHT ZONE", 1, "Zone05", "ZoneSZ");
            UseStageV4(context1, StageType.StagesRegular, "STARLIGHT ZONE", 2, "Zone05", "ZoneSZ");
            UseStageV4(context1, StageType.StagesRegular, "STARLIGHT ZONE", 3, "Zone05", "ZoneSZ");
            UseStageV4(context1, StageType.StagesRegular, "SCRAP BRAIN ZONE", 1, "Zone06", "ZoneSBZ");
            UseStageV4(context1, StageType.StagesRegular, "SCRAP BRAIN ZONE", 2, "Zone06", "ZoneSBZ");
            UseStageV4(context1, StageType.StagesRegular, "SCRAP BRAIN ZONE", 4, "Zone04", "ZoneLZ", visualActNumber: 3);
            UseStageV4(context1, StageType.StagesRegular, "FINAL ZONE", 5, "Zone06", "ZoneSBZ", visualActNumber: 0);

            var SonicCDStageNames = new[]
            {
                "PALMTREE PANIC",
                "DESERT DAZZLE",
                "COLLISION CHAOS",
                "TIDAL TEMPEST",
                "QUARTZ QUADRANT",
                "WACKY WORKBENCH",
                "STARDUST SPEEDWAY",
                "METALLIC MADNESS",
            };
            var SonicCDTimeZones = new[]
            {
                "PRESENT",
                "PAST",
                "GOOD FUTURE",
                "BAD FUTURE",
            };
            for (var zone = 1; zone <= SonicCDStageNames.Length; zone++)
            {
                if (zone == 2) // Ignore R2
                    continue;

                var stageName = $"{SonicCDStageNames[zone - 1]} ZONE";
                var stageShortName = new string(stageName.Split(' ').Select(x => x.First()).ToArray());
                for (var act = 1; act <= 3; act++)
                {
                    for (var timeZoneId = 0; timeZoneId < SonicCDTimeZones.Length; timeZoneId++)
                    {
                        if (act == 3 && timeZoneId < 2) // Act 3 does not contain PRESENT or PAST
                            continue;

                        var timeZone = (char)('A' + timeZoneId);
                        var srcFolder = $"R{zone}{act}{timeZone}";
                        var dstFolder = $"Zone{stageShortName}{act}{timeZone}";
                        UseStageV3(contextCd, StageType.StagesRegular, stageName, act, srcFolder, dstFolder, SonicCDTimeZones[timeZoneId]);
                    }
                }
            }

            UseStageV4(context2, StageType.StagesRegular, "EMERALD HILL ZONE", 1, "Zone01", "ZoneEHZ");
            UseStageV4(context2, StageType.StagesRegular, "EMERALD HILL ZONE", 2, "Zone01", "ZoneEHZ");
            UseStageV4(context2, StageType.StagesRegular, "CHEMICAL PLANT ZONE", 1, "Zone02", "ZoneCPZ");
            UseStageV4(context2, StageType.StagesRegular, "CHEMICAL PLANT ZONE", 2, "Zone02", "ZoneCPZ");
            UseStageV4(context2, StageType.StagesRegular, "AQUATIC RUIN ZONE", 1, "Zone03", "ZoneARZ");
            UseStageV4(context2, StageType.StagesRegular, "AQUATIC RUIN ZONE", 2, "Zone03", "ZoneARZ");
            UseStageV4(context2, StageType.StagesRegular, "CASINO NIGHT ZONE", 1, "Zone04", "ZoneCNZ");
            UseStageV4(context2, StageType.StagesRegular, "CASINO NIGHT ZONE", 2, "Zone04", "ZoneCNZ");
            UseStageV4(context2, StageType.StagesRegular, "HILL TOP ZONE", 1, "Zone05", "ZoneHTZ");
            UseStageV4(context2, StageType.StagesRegular, "HILL TOP ZONE", 2, "Zone05", "ZoneHTZ");
            UseStageV4(context2, StageType.StagesRegular, "MYSTIC CAVE ZONE", 1, "Zone06", "ZoneMCZ");
            UseStageV4(context2, StageType.StagesRegular, "MYSTIC CAVE ZONE", 2, "Zone06", "ZoneMCZ");
            UseStageV4(context2, StageType.StagesRegular, "OIL OCEAN ZONE", 1, "Zone07", "ZoneOOZ");
            UseStageV4(context2, StageType.StagesRegular, "OIL OCEAN ZONE", 2, "Zone07", "ZoneOOZ");
            UseStageV4(context2, StageType.StagesRegular, "HIDDEN PALACE ZONE", 1, "Zone08", "ZoneHPZ");
            UseStageV4(context2, StageType.StagesRegular, "METROPOLIS ZONE", 1, "Zone09", "ZoneMPZ");
            UseStageV4(context2, StageType.StagesRegular, "METROPOLIS ZONE", 2, "Zone09", "ZoneMPZ");
            UseStageV4(context2, StageType.StagesRegular, "METROPOLIS ZONE", 3, "Zone09", "ZoneMPZ");
            UseStageV4(context2, StageType.StagesRegular, "SKY CHASE ZONE", 1, "Zone10", "ZoneSCZ", visualActNumber: 0);
            UseStageV4(context2, StageType.StagesRegular, "WING FORTRESS ZONE", 1, "Zone11", "ZoneWFZ", visualActNumber: 0);
            UseStageV4(context2, StageType.StagesRegular, "DEATH EGG ZONE", 1, "Zone12", "ZoneDEZ", visualActNumber: 0);

            for (var i = 1; i <= 8; i++)
                UseStageV4(context2, StageType.StagesSpecial, "SPECIAL STAGE", i, "Special", "Special2");
            for (var i = 1; i <= 6; i++)
                UseStageV4(context1, StageType.StagesSpecial, "SPECIAL STAGE", i, "Special", "Special1");

            Create(Path.Combine(destinationDataRsdk, "Data/Game/GameConfig.bin"), sonicHybridConfig.Write);

            // RSDKv4 runs an object only through eventObjectUpdate/Draw/Startup
            // (Script.cpp:2848-2864) or through loaded bytecode
            // (Script.cpp:3191-3209). The tracked Sonic 1/2 text scripts use
            // neither, so without this the engine parses them and calls nothing.
            //
            // GlobalCode.bin is NOT copied here. RSDKv4 loads exactly one for the
            // whole process and decides text-vs-bytecode on whether it resolves
            // (Scene.cpp:675), so the two games' containers have to be merged -
            // scripts/emit_merged_bytecode.py does that between this step and the
            // packer, along with both games' per-stage files.
        }

        /// <summary>
        /// Sonic 2 bytecode file name -> the folder name the merged stage list
        /// uses for that stage. RSDKv4 resolves bytecode as
        /// <c>Bytecode/&lt;stage folder&gt;.bin</c> (Script.cpp:3075-3085), so the
        /// files have to be renamed to match, not just copied.
        /// </summary>
        /// <remarks>
        /// GlobalCode.bin is absent from this list on purpose: it keeps its own
        /// name, and it is the file whose presence decides whether the engine
        /// takes the bytecode path at all.
        /// </remarks>
        private static readonly (string From, string To)[] Sonic2BytecodeFolders =
        {
            ("Zone01", "ZoneEHZ"),  ("Zone02", "ZoneCPZ"), ("Zone03", "ZoneARZ"),
            ("Zone04", "ZoneCNZ"),  ("Zone05", "ZoneHTZ"), ("Zone06", "ZoneMCZ"),
            ("Zone07", "ZoneOOZ"),  ("Zone08", "ZoneHPZ"), ("Zone09", "ZoneMPZ"),
            ("Zone10", "ZoneSCZ"),  ("Zone11", "ZoneWFZ"), ("Zone12", "ZoneDEZ"),
            ("Title", "TitleS2"),   ("LSelect", "LSelectS2"),
            ("Credits", "CreditsS2"), ("Ending", "EndingS2"),
            ("Continue", "ContinueS2"), ("Special", "Special2"),
        };

        /// <summary>
        /// Copies a game's RSDKv4 bytecode into the hybrid, renaming the stage
        /// files to the merged stage list's folder names.
        /// </summary>
        /// <remarks>
        /// Only one game's bytecode can be shipped at a time. RSDKv4 keys the
        /// text/bytecode choice on whether <c>Bytecode/GlobalCode.bin</c>
        /// resolves (Scene.cpp:675) - a single global file - and loads one
        /// GlobalCode for the whole process. Sonic 1 and Sonic 2 each ship their
        /// own, and they disagree about what every object type is, so merging
        /// them means rewriting each container's absolute pointers. Sonic 2 goes
        /// first because it is the larger of the two and needs no merge.
        /// </remarks>
        private static void CopyBytecodeV4(
            Context context, string destinationDataRsdk,
            (string From, string To)[] stageFolders)
        {
            // Context.SrcPath points at <game>/Data; the bytecode sits beside it, in
            // <game>/Bytecode.
            var gameRoot = Path.GetDirectoryName(context.SrcPath.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var srcByteCode = gameRoot == null
                ? null
                : Path.Combine(gameRoot, "Bytecode");
            if (srcByteCode == null || !Directory.Exists(srcByteCode))
            {
                Console.WriteLine("  no Bytecode folder - the engine will fall back to text scripts");
                return;
            }

            var dstByteCode = Path.Combine(destinationDataRsdk, "Data", "Bytecode");
            Directory.CreateDirectory(dstByteCode);

            var renamed = stageFolders.ToDictionary(x => x.From + ".bin", x => x.To + ".bin");
            int copied = 0, renamedCount = 0, missing = 0;
            var skippedGlobal = false;

            foreach (var source in Directory.GetFiles(srcByteCode, "*.bin"))
            {
                // GlobalCode.bin is handled by the merger, not copied: the two
                // games each ship one and RSDKv4 loads exactly one for the whole
                // process.
                if (string.Equals(Path.GetFileName(source), "GlobalCode.bin",
                                  StringComparison.OrdinalIgnoreCase))
                {
                    skippedGlobal = true;
                    continue;
                }

                if (renamed.TryGetValue(Path.GetFileName(source), out var mapped))
                {
                    File.Copy(source, Path.Combine(dstByteCode, mapped), true);
                    ++renamedCount;
                }
                else
                {
                    File.Copy(source, Path.Combine(dstByteCode, Path.GetFileName(source)), true);
                    ++copied;
                }
            }

            // Every zone the merged stage list points at must resolve, or that
            // stage silently gets no object scripts at all.
            foreach (var (_, to) in stageFolders)
            {
                var path = Path.Combine(dstByteCode, to + ".bin");
                if (!File.Exists(path))
                {
                    Console.WriteLine($"  WARNING: {to}.bin was not produced");
                    ++missing;
                }
            }

            if (skippedGlobal)
                Console.WriteLine("  bytecode: GlobalCode.bin left to the merger");

            Console.WriteLine($"  bytecode: {renamedCount + copied} files " +
                              $"({renamedCount} zone files renamed)" +
                              (missing > 0 ? $", {missing} zones MISSING" : ""));
        }

        private static void UseStageV4(
            Context context,
            StageType stageType,
            string name,
            int actNumber,
            string srcFolder,
            string dstFolder,
            int visualActNumber = -1)
        {
            var stages = context.SrcConfig.GetStages(stageType);
            if (visualActNumber < 0)
                visualActNumber = actNumber;

            var srcStage = stages.First(x => x.Act == actNumber.ToString() && x.Path == srcFolder);
            var dstStage = new Stage
            {
                Name = visualActNumber > 0 ? $"{name} {visualActNumber}" : name,
                Act = actNumber.ToString(),
                Mode = srcStage.Mode,
                Path = dstFolder,
            };

            var srcPath = Path.Combine(context.SrcPath, "Stages", srcFolder);
            var dstPath = Path.Combine(context.DstPath, "Stages", dstFolder);
            Directory.CreateDirectory(dstPath);

            File.Copy(Path.Combine(srcPath, "16x16Tiles.gif"), Path.Combine(dstPath, "16x16Tiles.gif"), true);
            File.Copy(Path.Combine(srcPath, "128x128Tiles.bin"), Path.Combine(dstPath, "128x128Tiles.bin"), true);
            File.Copy(Path.Combine(srcPath, "Backgrounds.bin"), Path.Combine(dstPath, "Backgrounds.bin"), true);
            File.Copy(Path.Combine(srcPath, "CollisionMasks.bin"), Path.Combine(dstPath, "CollisionMasks.bin"), true);
            PatchStageConfig(context,
                StageConfig.Read,
                Path.Combine(srcPath, "StageConfig.bin"),
                Path.Combine(dstPath, "StageConfig.bin"));
            PatchStage(context,
                StageAct.Read,
                Path.Combine(srcPath, $"Act{actNumber}.bin"),
                Path.Combine(dstPath, $"Act{actNumber}.bin"),
                (context, entity, name) =>
                {
                    switch (name)
                    {
                        case "Title Card":
                            entity.PropertyValue = (byte)(visualActNumber > 0 ? visualActNumber : 4);
                            break;
                        default:
                            return false;
                    }

                    return true;
                });

            var background = OpenRead(Path.Combine(srcPath, "Backgrounds.bin"), StageBackgroundV4.Read);
            Create(Path.Combine(dstPath, "Backgrounds.bin"), background.Write);

            context.DstConfig.GetStages(stageType).Add(dstStage);
        }
    }
}
