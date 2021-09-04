using System.IO;
using System.Linq;
using static SonicHybridRsdk.Generator.Global;
using static SonicHybridRsdk.Generator.RsdkGenericImporter;

namespace SonicHybridRsdk.Generator
{
    class RsdkSonicCdImporter
    {
        public static void UseStageV3(
            Context context,
            StageType stageType,
            string name,
            int actNumber,
            string srcFolder,
            string dstFolder,
            string timeZone)
        {
            var stages = context.SrcConfig.GetStages(stageType);

            var srcStage = stages.First(x => x.Act == actNumber.ToString() && x.Path == srcFolder);
            var dstStage = new Stage
            {
                Name = actNumber > 0 ? $"{name} {actNumber} {timeZone}" : $"{name} {timeZone}",
                Act = actNumber.ToString(),
                Mode = srcStage.Mode,
                Path = dstFolder,
            };

            var srcPath = Path.Combine(context.SrcPath, "Stages", srcFolder);
            var dstPath = Path.Combine(context.DstPath, "Stages", dstFolder);
            Directory.CreateDirectory(dstPath);

            File.Copy(Path.Combine(srcPath, "16x16Tiles.gif"), Path.Combine(dstPath, "16x16Tiles.gif"), true);
            File.Copy(Path.Combine(srcPath, "128x128Tiles.bin"), Path.Combine(dstPath, "128x128Tiles.bin"), true);
            File.Copy(Path.Combine(srcPath, "CollisionMasks.bin"), Path.Combine(dstPath, "CollisionMasks.bin"), true);

            var background = OpenRead(Path.Combine(srcPath, "Backgrounds.bin"), StageBackgroundV3.Read);
            Create(Path.Combine(dstPath, "Backgrounds.bin"), stream => new StageBackgroundV4
            {
                HLines = background.HLines,
                VLines = background.VLines,
                Layers = background.Layers.Select(x => new StageBackgroundLayerV4
                {
                    Width = x.Width,
                    Height = x.Height,
                    Behaviour = x.Behaviour,
                    ConstantSpeed = x.ConstantSpeed,
                    RelativeSpeed = x.RelativeSpeed,
                    LineIndices = x.LineIndices,
                    Layout = x.Layout,
                }).ToList()
            }.Write(stream));

            PatchStageConfig(context,
                StageConfigV3.Read,
                Path.Combine(srcPath, "StageConfig.bin"),
                Path.Combine(dstPath, "StageConfig.bin"));
            PatchStage(context,
                StageActV3.Read,
                Path.Combine(srcPath, $"Act{actNumber}.bin"),
                Path.Combine(dstPath, $"Act{actNumber}.bin"),
                (context, entity, entityName) =>
                {
                    switch (entityName)
                    {
                        case "Title Card":
                            entity.PropertyValue = (byte)(actNumber > 0 ? actNumber : 4);
                            break;
                        case "Sign Post":
                        case "SignPost":
                            entity.PropertyValue = 0;
                            entity.Y -= 20; // Fix Y placement
                            break;
                        case "Flower Pod":
                            entity.Type = (byte)(context.DstObjects[entityName] + 1);
                            entity.PropertyValue = 0;
                            entity.Y += 16; // Fix Y placement
                            break;
                        default:
                            return false;
                    }

                    return true;
                },
                (context, act) =>
                {
                    // Add title card
                    act.Title = name.EndsWith(" ZONE") ? name[..^(" ZONE").Length] : name;
                    var titleCardType = (byte)(context.DstObjects["Title Card"] + 1);
                    var playerObject = act.Entities.FirstOrDefault();
                    if (playerObject?.Type == 1 && !act.Entities.Any(x => x.Type == titleCardType))
                    {
                        act.Entities.Insert(2, new Entity
                        {
                            AttributeFlags = 0,
                            Type = titleCardType,
                            PropertyValue = (byte)(actNumber > 0 ? actNumber : 4),
                            X = (short)(playerObject.X - 156),
                            Y = (short)(playerObject.Y - 8),
                        });
                    }
                });

            context.DstConfig.GetStages(stageType).Add(dstStage);
        }
    }
}
