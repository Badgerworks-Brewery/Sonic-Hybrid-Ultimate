using System.Collections.Generic;
using System.IO;

namespace SonicHybridRsdk.Generator
{
    class StageConfig
    {
        public byte[] UnknownData { get; }
        public List<GameObject> Sfx { get; }
        public List<GameObject> Objects { get; }

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

        public static StageConfig Read(Stream stream) => new StageConfig(stream);
    }
}
