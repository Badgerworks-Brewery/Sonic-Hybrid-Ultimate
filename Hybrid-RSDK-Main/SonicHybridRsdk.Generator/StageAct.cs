using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xe.BinaryMapper;
using static SonicHybridRsdk.Generator.Global;

namespace SonicHybridRsdk.Generator
{
    interface IStageAct
    {
        List<IEntity> Entities { get; set; }
        List<string> EntityNames { get; set; }
        ushort Height { get; set; }
        byte[] Layers { get; set; }
        ushort[] Layout { get; set; }
        string Title { get; set; }
        ushort Width { get; set; }

        void Write(Stream stream);
    }

    record StageAct : IStageAct
    {
        [Data] public string Title { get; set; }
        [Data(Count = 5)] public byte[] Layers { get; set; }
        [Data] public ushort Width { get; set; }
        [Data] public ushort Height { get; set; }
        public ushort[] Layout { get; set; }
        public List<IEntity> Entities { get; set; }
        public List<string> EntityNames { get; set; } = new List<string>();

        public static StageAct Read(Stream stream)
        {
            var stage = Mapper.ReadObject<StageAct>(stream);
            stage.Layout = new ushort[stage.Width * stage.Height];
            for (var i = 0; i < stage.Width * stage.Height; i++)
                stage.Layout[i] = (ushort)(stream.ReadByte() + (stream.ReadByte() << 8));

            stage.Entities = Enumerable.Range(0, stream.ReadByte() + (stream.ReadByte() << 8))
                .Select(_ => Entity.Read(stream))
                .Cast<IEntity>()
                .ToList();

            return stage;
        }

        public void Write(Stream stream)
        {
            Mapper.WriteObject(stream, this);
            for (var i = 0; i < Width * Height; i++)
            {
                stream.WriteByte((byte)(Layout[i] >> 0));
                stream.WriteByte((byte)(Layout[i] >> 8));
            }

            stream.WriteByte((byte)(Entities.Count >> 0));
            stream.WriteByte((byte)(Entities.Count >> 8));
            foreach (var entity in Entities)
                ((Entity)entity).Write(stream);
        }
    }

    record StageActV3 : IStageAct
    {
        [Data] public string Title { get; set; }
        [Data(Count = 5)] public byte[] Layers { get; set; }
        [Data] public byte InternalWidth { get; set; }
        [Data] public byte InternalHeight { get; set; }
        public ushort Width { get => InternalWidth; set => InternalWidth = Convert.ToByte(value); }
        public ushort Height { get => InternalHeight; set => InternalHeight = Convert.ToByte(value); }
        public ushort[] Layout { get; set; }
        public List<string> EntityNames { get; set; }
        public List<IEntity> Entities { get; set; }

        public static StageActV3 Read(Stream stream)
        {
            var stage = Mapper.ReadObject<StageActV3>(stream);
            stage.Layout = new ushort[stage.Width * stage.Height];
            for (var i = 0; i < stage.Width * stage.Height; i++)
                stage.Layout[i] = (ushort)((stream.ReadByte() << 8) + stream.ReadByte());
            stage.EntityNames = Enumerable.Range(0, stream.ReadByte())
                .Select(_ => ReadString(stream))
                .ToList();
            stage.Entities = Enumerable.Range(0, (stream.ReadByte() << 8) + stream.ReadByte())
                .Select(_ => EntityV3.Read(stream))
                .Cast<IEntity>()
                .ToList();

            return stage;
        }

        public void Write(Stream stream) =>
            throw new NotImplementedException();
    }
}
