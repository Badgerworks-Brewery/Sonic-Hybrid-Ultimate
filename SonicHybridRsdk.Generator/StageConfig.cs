using System.Collections.Generic;
using System.IO;
using System.Linq;
using static SonicHybridRsdk.Generator.Global;

namespace SonicHybridRsdk.Generator
{
    interface IStageConfig
    {
        List<GameObject> Objects { get; }
        List<GameObject> Sfx { get; }
        byte[] UnknownData { get; }

        void Write(Stream stream);
    }

    class StageConfig : IStageConfig
    {
        public byte[] UnknownData { get; set; }
        public List<GameObject> Sfx { get; set; }
        public List<GameObject> Objects { get; set; }

        public StageConfig()
        {
        }

        private StageConfig(Stream stream)
        {
            UnknownData = new byte[0x61];
            stream.Read(UnknownData);

            Sfx = GameObject.Read(stream);
            Objects = GameObject.Read(stream);
        }

        public void Write(Stream stream)
        {
            stream.Write(UnknownData);
            GameObject.Write(stream, Sfx);
            GameObject.Write(stream, Objects);
        }

        public static StageConfig Read(Stream stream) => new(stream);
    }

    class StageConfigV3 : IStageConfig
    {
        public byte[] UnknownData { get; }
        public List<GameObject> Sfx { get; }
        public List<GameObject> Objects { get; }

        private StageConfigV3(Stream stream)
        {
            UnknownData = new byte[0x61];
            stream.Read(UnknownData);

            Objects = GameObject.Read(stream);
            Sfx = Enumerable.Range(0, stream.ReadByte())
                .Select(_ => ReadString(stream))
                .Select(x => new GameObject
                {
                    Name = Path.GetFileNameWithoutExtension(x),
                    Path = x,
                })
                .ToList();
        }

        public void Write(Stream stream)
        {
            stream.Write(UnknownData);
            GameObject.Write(stream, Objects);
        }

        public static StageConfigV3 Read(Stream stream) => new(stream);
    }
}
