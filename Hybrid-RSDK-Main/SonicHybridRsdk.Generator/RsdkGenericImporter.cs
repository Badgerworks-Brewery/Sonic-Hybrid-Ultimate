using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static SonicHybridRsdk.Generator.Global;

namespace SonicHybridRsdk.Generator
{
    class RsdkGenericImporter
    {
        public static void PatchStageConfig(Context context, Func<Stream, IStageConfig> stageConfigRead, string srcFile, string dstFile)
        {
            var src = OpenRead(srcFile, stageConfigRead);
            PatchGameObjects(src.Sfx, context.Replacements);
            PatchGameObjects(src.Objects, context.Replacements);
            Create(dstFile, stream => new StageConfig
            {
                Objects = src.Objects,
                Sfx = src.Sfx,
                UnknownData = src.UnknownData,
            }.Write(stream));
        }

        public static void PatchGameObjects(List<GameObject> gameObjects, Dictionary<string, string> replacements)
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

        public static void PatchStage(
            Context context,
            Func<Stream, IStageAct> stageActRead,
            string srcFile, string dstFile,
            Func<Context, IEntity, string, bool> entityAction = null,
            Action<Context, IStageAct> postAction = null)
        {
            var act = OpenRead(srcFile, stageActRead);
            for (int i = 0; i < act.Entities.Count; i++)
            {
                var entity = act.Entities[i];
                var id = entity.Type;
                if (id < context.SrcConfig.GameObjects.Count)
                {
                    if (id == 0)
                        continue;
                    var srcObj = context.SrcObjects[id - 1].Name;
                    var dstObj = context.DstObjects[srcObj] + 1;
                    entity.Type = (byte)dstObj;
                    entityAction?.Invoke(context, entity, srcObj);
                }
                else
                {
                    if (entityAction == null ||
                        id + 1 >= act.EntityNames.Count ||
                        entityAction(context, entity, act.EntityNames[id - 1]) == false)
                    {
                        var localId = id - context.SrcConfig.GameObjects.Count;
                        entity.Type = Convert.ToByte(context.DstConfig.GameObjects.Count + localId);
                    }
                }
            }

            postAction?.Invoke(context, act);
            Create(dstFile, stream => new StageAct
            {
                Title = act.Title,
                Layers = act.Layers,
                Width = act.Width,
                Height = act.Height,
                Layout = act.Layout,
                Entities = act.Entities.Select(x => new Entity
                {
                    Type = x.Type,
                    PropertyValue = x.PropertyValue,
                    X = x.X,
                    Y = x.Y,
                    SubX = x.SubX,
                    SubY = x.SubY,
                    AttributeFlags = x.AttributeFlags,
                    Attributes = x.Attributes,
                }).Cast<IEntity>().ToList(),
            }.Write(stream));
        }
    }
}
