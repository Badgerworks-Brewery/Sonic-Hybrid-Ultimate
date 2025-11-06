using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xe.BinaryMapper;
using static SonicHybridRsdk.Generator.Global;

namespace SonicHybridRsdk.Generator
{
    interface IStageBackground
    {
    }

    // https://github.com/Rubberduckycooly/RSDK-Reverse/blob/master/RSDKv4/BackgroundLayout.cs
    record ScrollInfo
    {
        [Data] public short RelativeSpeed { get; set; }
        [Data] public byte ConstantSpeed { get; set; }
        [Data] public byte Behaviour { get; set; }

        public static ScrollInfo Read(Stream stream) =>
            Mapper.ReadObject<ScrollInfo>(stream);

        public void Write(Stream stream) =>
            Mapper.WriteObject(stream, this);
    }

    // https://github.com/Rubberduckycooly/RSDK-Reverse/blob/master/RSDKv3/BackgroundLayout.cs
    record StageBackgroundV3 : IStageBackground
    {
        public List<ScrollInfo> HLines { get; set; }
        public List<ScrollInfo> VLines { get; set; }
        public List<StageBackgroundLayerV3> Layers { get; set; }

        public StageBackgroundV3()
        {

        }

        private StageBackgroundV3(Stream stream)
        {
            var layerCount = stream.ReadByte();
            HLines = Enumerable.Range(0, stream.ReadByte())
                .Select(_ => ScrollInfo.Read(stream))
                .ToList();
            VLines = Enumerable.Range(0, stream.ReadByte())
                .Select(_ => ScrollInfo.Read(stream))
                .ToList();
            Layers = Enumerable.Range(0, layerCount)
                .Select(_ => StageBackgroundLayerV3.Read(stream))
                .ToList();

            foreach (var line in HLines)
                line.RelativeSpeed = FlipEndian(line.RelativeSpeed);
            foreach (var line in VLines)
                line.RelativeSpeed = FlipEndian(line.RelativeSpeed);
        }

        public static StageBackgroundV3 Read(Stream stream) => new(stream);

        public void Write(Stream stream)
        {
            stream.WriteByte(Convert.ToByte(Layers.Count));

            stream.WriteByte(Convert.ToByte(HLines.Count));
            foreach (var line in HLines)
                line.Write(stream);

            stream.WriteByte(Convert.ToByte(VLines.Count));
            foreach (var line in HLines)
                line.Write(stream);

            foreach (var layer in Layers)
                layer.Write(stream);
        }
    }

    // https://github.com/Rubberduckycooly/RSDK-Reverse/blob/master/RSDKv4/BackgroundLayout.cs
    record StageBackgroundV4 : IStageBackground
    {
        public List<ScrollInfo> HLines { get; set; }
        public List<ScrollInfo> VLines { get; set; }
        public List<StageBackgroundLayerV4> Layers { get; set; }

        public StageBackgroundV4()
        {

        }

        private StageBackgroundV4(Stream stream)
        {
            var layerCount = stream.ReadByte();
            HLines = Enumerable.Range(0, stream.ReadByte())
                .Select(_ => ScrollInfo.Read(stream))
                .ToList();
            VLines = Enumerable.Range(0, stream.ReadByte())
                .Select(_ => ScrollInfo.Read(stream))
                .ToList();
            Layers = Enumerable.Range(0, layerCount)
                .Select(_ => StageBackgroundLayerV4.Read(stream))
                .ToList();
        }

        public static StageBackgroundV4 Read(Stream stream) => new(stream);

        public void Write(Stream stream)
        {
            stream.WriteByte(Convert.ToByte(Layers.Count));

            stream.WriteByte(Convert.ToByte(HLines.Count));
            foreach (var line in HLines)
                line.Write(stream);

            stream.WriteByte(Convert.ToByte(VLines.Count));
            foreach (var line in VLines)
                line.Write(stream);

            foreach (var layer in Layers)
                layer.Write(stream);
        }
    }
}
