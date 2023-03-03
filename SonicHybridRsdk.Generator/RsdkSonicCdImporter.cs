
using System.IO;
using System.Linq;
using static SonicHybridRsdk.Generator.Global;
using static SonicHybridRsdk.Generator.RsdkGenericImporter;

namespace SonicHybridRsdk.Generator
{
    class RsdkSonicCdImporter
    {
        public static void UseStageV3(Context context, StageType stageType, string name, int actNumber, string srcFolder, string dstFolder, string timeZone)
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

            using var background = OpenRead(Path.Combine(srcPath, "Backgrounds.bin"), StageBackgroundV3.Read);
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

            PatchStageConfig(context, StageConfigV3.Read, Path.Combine(srcPath, "StageConfig.bin"), Path.Combine(dstPath, "StageConfig.bin"));

            PatchStage(context, StageActV3.Read, Path.Combine(srcPath, $"Act{actNumber}.bin"), Path.Combine(dstPath, $"Act{actNumber}.bin"),
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
                           
