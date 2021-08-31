using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SonicHybridRsdk.Generator
{
    public class Program
    {
        class Context
        {
            public string SrcPath { get; init; }
            public string DstPath { get; init; }
            public GameConfig SrcConfig { get; init; }
            public GameConfig DstConfig { get; init; }
            public Dictionary<int, GameObject> SrcObjects { get; init; }
            public Dictionary<string, int> DstObjects { get; init; }
            public Dictionary<string, string> Replacements { get; init; }
        }

        static void Main(string[] args) => Generate(args[0], args[1]);

        public static void CopyResources(string sourceDataRsdk, string destinationDataRsdk)
        {
            var sonic1Path = Path.Combine(sourceDataRsdk, "sonic1/Data");
            var sonic2Path = Path.Combine(sourceDataRsdk, "sonic2/Data");
            var sonicHybridPath = Path.Combine(destinationDataRsdk, "Data");

            foreach (var folder in new string[]
            {
                "Animations",
                "Game",
                "Music",
                "Palettes",
                "SoundFX",
                "Sprites",
            })
            {
                Copy(Path.Combine(sonic1Path, folder), Path.Combine(sonicHybridPath, folder));
                Copy(Path.Combine(sonic2Path, folder), Path.Combine(sonicHybridPath, folder));
            }

            foreach (var (SourcePath, DestinationPath) in new (string, string)[]
            {
                ("Sprites/Global/Items2.gif", "Sprites/Global/Items4.gif"),
                ("Sprites/Global/Display.gif", "Sprites/Global/Display2.gif"),
            })
                File.Copy(
                    Path.Combine(sonic1Path, SourcePath),
                    Path.Combine(sonicHybridPath, DestinationPath),
                    true);
        }

        public static void Generate(string sourceDataRsdk, string destinationDataRsdk)
        {
            CopyResources(sourceDataRsdk, destinationDataRsdk);

            var sonic1Path = Path.Combine(sourceDataRsdk, "sonic1/Data");
            var sonic2Path = Path.Combine(sourceDataRsdk, "sonic2/Data");
            var sonicHybridPath = Path.Combine(destinationDataRsdk, "Data");

            var sonic1Config = OpenRead(Path.Combine(sonic1Path, "Game/GameConfig.bin"), GameConfig.Read);
            var sonic2Config = OpenRead(Path.Combine(sonic2Path, "Game/GameConfig.bin"), GameConfig.Read);

            var sonicHybridConfig = new GameConfig
            {
                Name = "Sonic Hybrid",
                Description = $"Hack by Xeeynamo\n\n{sonic1Config.Description}",
                PaletteData = sonic2Config.PaletteData,
                StagesPresentation = new List<GameConfig.Stage>(),
                StagesRegular = new List<GameConfig.Stage>(),
                StagesBonus = new List<GameConfig.Stage>(),
                StagesSpecial = new List<GameConfig.Stage>(),
            };

            var hybridObjects = new Dictionary<string, GameObject>();
            foreach (var obj in sonic2Config.GameObjects)
                hybridObjects.Add(obj.Name, obj);
            foreach (var obj in sonic1Config.GameObjects)
            {
                if (!hybridObjects.ContainsKey(obj.Name))
                    hybridObjects.Add(obj.Name, obj);
            }
            sonicHybridConfig.GameObjects = hybridObjects.Values.ToList();

            var dicHybridObjects = sonicHybridConfig.GameObjects.Select((x, i) => (Id: i, Obj: x)).ToDictionary(x => x.Obj.Name, x => x.Id);
            dicHybridObjects["Lamp Post"] = dicHybridObjects["Star Post"];

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
            foreach (var item in sonic2Config.Variables)
                variables[item.Name] = item.Value;
            sonicHybridConfig.Variables = variables.Select(x => new GameConfig.Variable { Name = x.Key, Value = x.Value }).ToList();

            sonicHybridConfig.Players = sonic2Config.Players;
            sonicHybridConfig.SoundEffects = sonic2Config.SoundEffects;

            UseStage(context2, StageType.StagesPresentation, "TITLE SCREEN SONIC 2", 1, "Title", "TitleS2");
            UseStage(context2, StageType.StagesPresentation, "ENDING SONIC 2", 1, "Ending", "EndingS2");
            UseStage(context2, StageType.StagesPresentation, "STAFF CREDITS SONIC 2", 1, "Credits", "CreditsS2");
            UseStage(context2, StageType.StagesPresentation, "LEVEL SELECT SONIC 2", 1, "LSelect", "LSelectS2");
            UseStage(context2, StageType.StagesPresentation, "LEVEL SELECT 2P", 2, "LSelect", "LSelectS2");
            UseStage(context2, StageType.StagesPresentation, "CONTINUE SCREEN SONIC 1", 1, "Continue", "ContinueS1");

            UseStage(context1, StageType.StagesPresentation, "TITLE SCREEN SONIC 1", 1, "Title", "TitleS1");
            UseStage(context1, StageType.StagesPresentation, "ENDING SONIC 1", 1, "Ending", "EndingS1");
            UseStage(context1, StageType.StagesPresentation, "STAFF CREDITS SONIC 1", 1, "Credits", "CreditsS1");
            UseStage(context1, StageType.StagesPresentation, "UNLOCK ALL ACHIEVEMENTS", 2, "Credits", "CreditsS1");
            UseStage(context1, StageType.StagesPresentation, "CONTINUE SCREEN SONIC 1", 1, "Continue", "ContinueS1");
            UseStage(context1, StageType.StagesPresentation, "LEVEL SELECT SONIC 1", 1, "LSelect", "LSelectS1");

            UseStage(context1, StageType.StagesRegular, "GREEN HILL ZONE", 1, "Zone01", "ZoneGHZ");
            UseStage(context1, StageType.StagesRegular, "GREEN HILL ZONE", 2, "Zone01", "ZoneGHZ");
            UseStage(context1, StageType.StagesRegular, "GREEN HILL ZONE", 3, "Zone01", "ZoneGHZ");
            UseStage(context1, StageType.StagesRegular, "MARBLE ZONE", 1, "Zone02", "ZoneMZ");
            UseStage(context1, StageType.StagesRegular, "MARBLE ZONE", 2, "Zone02", "ZoneMZ");
            UseStage(context1, StageType.StagesRegular, "MARBLE ZONE", 3, "Zone02", "ZoneMZ");
            UseStage(context1, StageType.StagesRegular, "SPRING YARD ZONE", 1, "Zone03", "ZoneSYZ");
            UseStage(context1, StageType.StagesRegular, "SPRING YARD ZONE", 2, "Zone03", "ZoneSYZ");
            UseStage(context1, StageType.StagesRegular, "SPRING YARD ZONE", 3, "Zone03", "ZoneSYZ");
            UseStage(context1, StageType.StagesRegular, "LABYRINTH ZONE", 1, "Zone04", "ZoneLZ");
            UseStage(context1, StageType.StagesRegular, "LABYRINTH ZONE", 2, "Zone04", "ZoneLZ");
            UseStage(context1, StageType.StagesRegular, "LABYRINTH ZONE", 3, "Zone04", "ZoneLZ");
            UseStage(context1, StageType.StagesRegular, "STARLIGHT ZONE", 1, "Zone05", "ZoneSZ");
            UseStage(context1, StageType.StagesRegular, "STARLIGHT ZONE", 2, "Zone05", "ZoneSZ");
            UseStage(context1, StageType.StagesRegular, "STARLIGHT ZONE", 3, "Zone05", "ZoneSZ");
            UseStage(context1, StageType.StagesRegular, "SCRAP BRAIN ZONE", 1, "Zone06", "ZoneSBZ");
            UseStage(context1, StageType.StagesRegular, "SCRAP BRAIN ZONE", 2, "Zone06", "ZoneSBZ");
            UseStage(context1, StageType.StagesRegular, "SCRAP BRAIN ZONE", 4, "Zone04", "ZoneSLZ", visualActNumber: 3);
            UseStage(context1, StageType.StagesRegular, "FINAL ZONE", 5, "Zone06", "ZoneSBZ", visualActNumber: 0);

            UseStage(context2, StageType.StagesRegular, "EMERALD HILL ZONE", 1, "Zone01", "ZoneEHZ");
            UseStage(context2, StageType.StagesRegular, "EMERALD HILL ZONE", 2, "Zone01", "ZoneEHZ");
            UseStage(context2, StageType.StagesRegular, "CHEMICAL PLANT ZONE", 1, "Zone02", "ZoneCPZ");
            UseStage(context2, StageType.StagesRegular, "CHEMICAL PLANT ZONE", 2, "Zone02", "ZoneCPZ");
            UseStage(context2, StageType.StagesRegular, "AQUATIC RUIN ZONE", 1, "Zone03", "ZoneARZ");
            UseStage(context2, StageType.StagesRegular, "AQUATIC RUIN ZONE", 2, "Zone03", "ZoneARZ");
            UseStage(context2, StageType.StagesRegular, "CASINO NIGHT ZONE", 1, "Zone04", "ZoneCNZ");
            UseStage(context2, StageType.StagesRegular, "CASINO NIGHT ZONE", 2, "Zone04", "ZoneCNZ");
            UseStage(context2, StageType.StagesRegular, "HILL TOP ZONE", 1, "Zone05", "ZoneHTZ");
            UseStage(context2, StageType.StagesRegular, "HILL TOP ZONE", 2, "Zone05", "ZoneHTZ");
            UseStage(context2, StageType.StagesRegular, "MYSTIC CAVE ZONE", 1, "Zone06", "ZoneMCZ");
            UseStage(context2, StageType.StagesRegular, "MYSTIC CAVE ZONE", 2, "Zone06", "ZoneMCZ");
            UseStage(context2, StageType.StagesRegular, "OIL OCEAN ZONE", 1, "Zone07", "ZoneOOZ");
            UseStage(context2, StageType.StagesRegular, "OIL OCEAN ZONE", 2, "Zone07", "ZoneOOZ");
            UseStage(context2, StageType.StagesRegular, "HIDDEN PALACE ZONE", 1, "Zone08", "ZoneHPZ");
            UseStage(context2, StageType.StagesRegular, "METROPOLIS ZONE", 1, "Zone09", "ZoneMPZ");
            UseStage(context2, StageType.StagesRegular, "METROPOLIS ZONE", 2, "Zone09", "ZoneMPZ");
            UseStage(context2, StageType.StagesRegular, "METROPOLIS ZONE", 3, "Zone09", "ZoneMPZ");
            UseStage(context2, StageType.StagesRegular, "SKY CHASE ZONE", 1, "Zone10", "ZoneSCZ", visualActNumber: 0);
            UseStage(context2, StageType.StagesRegular, "WING FORTRESS ZONE", 1, "Zone11", "ZoneWFZ", visualActNumber: 0);
            UseStage(context2, StageType.StagesRegular, "DEATH EGG ZONE", 1, "Zone12", "ZoneDEZ", visualActNumber: 0);

            for (var i = 1; i <= 8; i++)
                UseStage(context2, StageType.StagesSpecial, "SPECIAL STAGE", i, "Special", "Special2");
            for (var i = 1; i <= 6; i++)
                UseStage(context1, StageType.StagesSpecial, "SPECIAL STAGE", i, "Special", "Special1");

            Create(Path.Combine(destinationDataRsdk, "Data/Game/GameConfig.bin"), sonicHybridConfig.Write);
        }

        enum StageType
        {
            StagesPresentation,
            StagesRegular,
            StagesSpecial,
        }

        private static void UseStage(
            Context context,
            StageType stageType,
            string name,
            int actNumber,
            string srcFolder,
            string dstFolder,
            int visualActNumber = -1)
        {
            List<GameConfig.Stage> GetStages(GameConfig config, StageType stageType) => stageType switch
            {
                StageType.StagesPresentation => config.StagesPresentation,
                StageType.StagesRegular => config.StagesRegular,
                StageType.StagesSpecial => config.StagesSpecial,
                _ => throw new ArgumentException("Stage type not recognised"),
            };

            var stages = GetStages(context.SrcConfig, stageType);
            if (visualActNumber < 0)
                visualActNumber = actNumber;

            var srcStage = stages.First(x => x.Act == actNumber.ToString() && x.Path == srcFolder);
            var dstStage = new GameConfig.Stage
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
                Path.Combine(srcPath, "StageConfig.bin"),
                Path.Combine(dstPath, "StageConfig.bin"));
            PatchStage(context,
                Path.Combine(srcPath, $"Act{actNumber}.bin"),
                Path.Combine(dstPath, $"Act{actNumber}.bin"),
                visualActNumber);

            GetStages(context.DstConfig, stageType).Add(dstStage);
        }

        private static void PatchStageConfig(Context context, string srcFile, string dstFile)
        {
            var src = OpenRead(srcFile, StageConfig.Read);
            PatchGameObjects(src.Sfx, context.Replacements);
            PatchGameObjects(src.Objects, context.Replacements);
            Create(dstFile, src.Write);
        }

        private static void PatchGameObjects(List<GameObject> gameObjects, Dictionary<string, string> replacements)
        {
            if ((replacements?.Count ?? 0) == 0)
                return;

            foreach (var item in gameObjects)
            {
                if (replacements.TryGetValue(item.Name, out var newName))
                    item.Name = newName;
                if (replacements.TryGetValue(item.Path, out var newPath))
                    item.Path = newPath;
            }
        }

        private static void PatchStage(Context context, string srcFile, string dstFile, int actNumber)
        {
            var act = OpenRead(srcFile, StageAct.Read);
            for (int i = 0; i < act.Entities.Count; i++)
            {
                Entity entity = act.Entities[i];
                var id = entity.Type;
                if (id < context.SrcConfig.GameObjects.Count)
                {
                    if (id == 0)
                        continue;
                    var srcObj = context.SrcObjects[id - 1].Name;
                    var dstObj = context.DstObjects[srcObj] + 1;
                    entity.Type = (byte)dstObj;

                    switch (srcObj)
                    {
                        case "TitleCard":
                            entity.PropertyValue = (byte)(actNumber > 0 ? actNumber : 4);
                            break;
                    }
                }
                else
                {
                    var localId = id - context.SrcConfig.GameObjects.Count;
                    entity.Type = Convert.ToByte(context.DstConfig.GameObjects.Count + localId);
                }
            }

            Create(dstFile, act.Write);
        }

        private static void Copy(string srcPath, string dstPath)
        {
            Directory.CreateDirectory(dstPath);
            foreach (var filePath in Directory.EnumerateFiles(srcPath))
            {
                var dstFilePath = Path.Combine(dstPath, Path.GetFileName(filePath));
                File.Copy(filePath, dstFilePath, true);
            }

            foreach (var directoryPath in Directory.EnumerateDirectories(srcPath))
            {
                var dstDirectoryPath = Path.Combine(dstPath, Path.GetFileName(directoryPath));
                Copy(directoryPath, dstDirectoryPath);
            }
        }

        private static T OpenRead<T>(string fileName, Func<Stream, T> func)
        {
            using var stream = File.OpenRead(fileName);
            return func(stream);
        }

        private static void Create(string fileName, Action<Stream> func)
        {
            var directoryPath = Path.GetDirectoryName(fileName);
            Directory.CreateDirectory(directoryPath);
            
            using var stream = File.Create(fileName);
            func(stream);
        }
    }
}
