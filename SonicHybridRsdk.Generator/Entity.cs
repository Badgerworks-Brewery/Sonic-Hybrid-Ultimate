using System;
using System.IO;
using Xe.BinaryMapper;
using static SonicHybridRsdk.Generator.Global;

namespace SonicHybridRsdk.Generator
{
    interface IEntity
    {
        byte Type { get; set; }
        byte PropertyValue { get; set; }
        short X { get; set; }
        short Y { get; set; }
        short SubX { get; set; }
        short SubY { get; set; }
        ushort AttributeFlags { get; }
        object[] Attributes { get; }
    }

    record EntityV3 : IEntity
    {
        [Data] public byte Type { get; set; }
        [Data] public byte PropertyValue { get; set; }
        [Data] public short X { get; set; }
        [Data] public short Y { get; set; }
        public short SubX { get; set; }
        public short SubY { get; set; }

        public ushort AttributeFlags { get; }
        public object[] Attributes { get; }

        public static EntityV3 Read(Stream stream) =>
            Mapper.ReadObject<EntityV3>(stream);

        public void Write(Stream stream) =>
            Mapper.WriteObject(stream, this);
    }

    record Entity : IEntity
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

        [Data] public ushort AttributeFlags { get; set; }
        [Data] public byte Type { get; set; }
        [Data] public byte PropertyValue { get; set; }
        [Data] public short SubX { get; set; }
        [Data] public short X { get; set; }
        [Data] public short SubY { get; set; }
        [Data] public short Y { get; set; }
        public object[] Attributes { get; set; }

        public Entity()
        {

        }

        private Entity(Stream stream)
        {
            AttributeFlags = ReadUInt16BigEndian(stream);
            Type = (byte)stream.ReadByte();
            PropertyValue = (byte)stream.ReadByte();
            SubX = ReadInt16BigEndian(stream);
            X = ReadInt16BigEndian(stream);
            SubY = ReadInt16BigEndian(stream);
            Y = ReadInt16BigEndian(stream);

            Attributes = new object[AttributeType.Length];
            var data = new byte[4];
            for (var i = 0; i < AttributeType.Length; i++)
            {
                if (0 != (AttributeFlags & (1 << i)))
                {
                    Attributes[i] = AttributeType[i] switch
                    {
                        false => (byte)stream.ReadByte(),
                        true => stream.Read(data) == 4 ? BitConverter.ToInt32(data) : 0
                    };
                }
            }
        }

        public static Entity Read(Stream stream) => new(stream);

        public void Write(Stream stream)
        {
            WriteUInt16BigEndian(stream, AttributeFlags);
            stream.WriteByte(Type);
            stream.WriteByte(PropertyValue);
            WriteInt16BigEndian(stream, SubX);
            WriteInt16BigEndian(stream, X);
            WriteInt16BigEndian(stream, SubY);
            WriteInt16BigEndian(stream, Y);

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
}
