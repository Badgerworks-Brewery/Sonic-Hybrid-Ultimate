using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xe.BinaryMapper;

namespace SonicHybridRsdk.Generator
{
    record Entity
    {
        // https://github.com/Rubberduckycooly/RSDK-Reverse/blob/master/RSDKv4/Object.cs#L250
        private static readonly bool[] AttributeType = new bool[] {
            true, // state
            false, // direction
            true, // scale
            true, // rotation
            false, // drawOrder
            false, // priority
            false, // alpha
            false, // animation
            true, // animationSpeed
            false, // frame
            false, // inkEffect
            true,
            true,
            true,
            true,
        };

        private static IBinaryMapping Mapper = GameConfig.Mapper;

        [Data] public ushort AttributeFlags { get; set; }
        [Data] public byte Type { get; set; }
        [Data] public byte Subtype { get; set; }
        [Data] public int X { get; set; }
        [Data] public int Y { get; set; }
        public object[] Attributes { get; set; }

        public static Entity Read(Stream stream)
        {
            var entity = Mapper.ReadObject<Entity>(stream);
            entity.Attributes = new object[AttributeType.Length];
            var data = new byte[4];
            for (var i = 0; i < AttributeType.Length; i++)
            {
                if (0 != (entity.AttributeFlags & (1 << i)))
                {
                    entity.Attributes[i] = AttributeType[i] switch
                    {
                        false => (byte)stream.ReadByte(),
                        true => stream.Read(data) == 4 ? BitConverter.ToInt32(data) : 0
                    };
                }
            }

            return entity;
        }

        public void Write(Stream stream)
        {
            Mapper.WriteObject(stream, this);
            for (var i = 0; i < AttributeType.Length; i++)
            {
                if (0 != (AttributeFlags & (1 << i)))
                {
                    if (AttributeType[i])
                        stream.Write(BitConverter.GetBytes((int)Attributes[i]));
                    else
                        stream.WriteByte((byte)(int)Attributes[i]);
                }
            }
        }
    }

    record StageAct
    {
        private static IBinaryMapping Mapper = GameConfig.Mapper;

        [Data] public string Title { get; set; }
        [Data(Count = 5)] public byte[] Layers { get; set; }
        [Data] public ushort Width { get; set; }
        [Data] public ushort Height { get; set; }
        public ushort[] Layout { get; set; }
        public List<Entity> Entities { get; set; }

        public static StageAct Read(Stream stream)
        {
            var stage = Mapper.ReadObject<StageAct>(stream);
            stage.Layout = new ushort[stage.Width * stage.Height];
            for (var i = 0; i < stage.Width * stage.Height; i++)
                stage.Layout[i] = (ushort)(stream.ReadByte() + (stream.ReadByte() << 8));

            stage.Entities = Enumerable.Range(0, stream.ReadByte() + (stream.ReadByte() << 8))
                .Select(_ => Entity.Read(stream))
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
                entity.Write(stream);
        }
    }
}
