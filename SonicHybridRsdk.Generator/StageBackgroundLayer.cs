using System;
using System.IO;
using Xe.BinaryMapper;
using static SonicHybridRsdk.Generator.Global;

namespace SonicHybridRsdk.Generator
{
    interface IStageBackgroundLayer
    {
        ushort Width { get; set; }
        ushort Height { get; set; }
        short RelativeSpeed { get; set; }
        byte ConstantSpeed { get; set; }
        byte Behaviour { get; set; }
        byte[] LineIndices { get; set; }
        ushort[] Layout { get; set; }
    }

    // https://github.com/Rubberduckycooly/RSDK-Reverse/blob/master/RSDKv3/BackgroundLayout.cs
    record StageBackgroundLayerV3 : IStageBackgroundLayer
    {
        [Data] public byte InternalWidth { get; set; }
        [Data] public byte InternalHeight { get; set; }
        [Data] public byte Behaviour { get; set; }
        [Data] public short RelativeSpeed { get; set; }
        [Data] public byte ConstantSpeed { get; set; }
        public byte[] LineIndices { get; set; }
        public ushort[] Layout { get; set; }
        public ushort Width { get => InternalWidth; set => InternalWidth = Convert.ToByte(value); }
        public ushort Height { get => InternalHeight; set => InternalHeight = Convert.ToByte(value); }

        public static StageBackgroundLayerV3 Read(Stream stream)
        {
            var item = Mapper.ReadObject<StageBackgroundLayerV3>(stream);
            item.LineIndices = new byte[item.Height * 128 + 2];

            for (var lineCount = 0; stream.Position < stream.Length;)
            {
                var ch = (byte)stream.ReadByte();
                if (ch == 0xFF)
                {
                    ch = (byte)stream.ReadByte();
                    if (ch != 0xFF)
                    {
                        var length = stream.ReadByte() - 1;
                        for (var i = 0; i < length; i++)
                            item.LineIndices[lineCount++] = ch;
                    }
                    else
                        break;
                }
                else
                    item.LineIndices[lineCount++] = ch;
            }

            item.Layout = new ushort[item.Width * item.Height];
            for (var i = 0; i < item.Layout.Length; i++)
                item.Layout[i] = (ushort)((stream.ReadByte() << 8) + stream.ReadByte());

            return item;
        }

        public void Write(Stream stream) =>
            throw new NotImplementedException();
    }

    // https://github.com/Rubberduckycooly/RSDK-Reverse/blob/master/RSDKv4/BackgroundLayout.cs
    record StageBackgroundLayerV4 : IStageBackgroundLayer
    {
        [Data] public ushort Width { get; set; }
        [Data] public ushort Height { get; set; }
        [Data] public short RelativeSpeed { get; set; }
        [Data] public byte ConstantSpeed { get; set; }
        [Data] public byte Behaviour { get; set; }
        public byte[] LineIndices { get; set; }
        public ushort[] Layout { get; set; }

        public StageBackgroundLayerV4()
        {

        }

        public StageBackgroundLayerV4(Stream stream)
        {
            Width = (ushort)(stream.ReadByte() | (stream.ReadByte() << 8));
            Height = (ushort)(stream.ReadByte() | (stream.ReadByte() << 8));
            Behaviour = (byte)stream.ReadByte();
            RelativeSpeed = (short)(stream.ReadByte() | (stream.ReadByte() << 8));
            ConstantSpeed = (byte)stream.ReadByte();
            LineIndices = new byte[Height * 128 + 2];

            for (var lineCount = 0; stream.Position < stream.Length;)
            {
                var ch = (byte)stream.ReadByte();
                if (ch == 0xFF)
                {
                    ch = (byte)stream.ReadByte();
                    if (ch != 0xFF)
                    {
                        var length = stream.ReadByte() - 1;
                        for (var i = 0; i < length; i++)
                            LineIndices[lineCount++] = ch;
                    }
                    else
                        break;
                }
                else
                    LineIndices[lineCount++] = ch;
            }

            Layout = new ushort[Width * Height];
            for (var i = 0; i < Layout.Length; i++)
                Layout[i] = (ushort)(stream.ReadByte() | (stream.ReadByte() << 8));
        }

        public static StageBackgroundLayerV4 Read(Stream stream) => new(stream);

        public void Write(Stream stream)
        {
            stream.WriteByte((byte)Width);
            stream.WriteByte((byte)(Width >> 8));
            stream.WriteByte((byte)Height);
            stream.WriteByte((byte)(Height >> 8));
            stream.WriteByte(Behaviour);
            stream.WriteByte((byte)RelativeSpeed);
            stream.WriteByte((byte)(RelativeSpeed >> 8));
            stream.WriteByte(ConstantSpeed);

            var lastValue = 0;
            var lineCount = 0;
            for (int i = 0; i < Height * 128; i++)
            {
                if (LineIndices[i] != lastValue && i > 0)
                {
                    RleWrite(stream, lastValue, lineCount);
                    lineCount = 0;
                }

                lastValue = LineIndices[i];
                lineCount++;
            }

            RleWrite(stream, lastValue, lineCount);
            if (Height > 0)
                stream.WriteByte((byte)lastValue);
            stream.WriteByte(byte.MaxValue);
            stream.WriteByte(byte.MaxValue);

            for (var i = 0; i < Layout.Length; i++)
            {
                stream.WriteByte((byte)(Layout[i] & 0xff));
                stream.WriteByte((byte)((Layout[i] >> 8) & 0xff));
            }
        }

        private static void RleWrite(Stream stream, int value, int count)
        {
            if (count <= 2)
            {
                for (int y = 0; y < count; y++)
                    stream.WriteByte((byte)value);
            }
            else
            {
                while (count > 0)
                {
                    stream.WriteByte(byte.MaxValue);
                    stream.WriteByte((byte)value);
                    stream.WriteByte((byte)((count > 253) ? 254 : (count + 1)));
                    count -= 253;
                }
            }
        }
    }
}
