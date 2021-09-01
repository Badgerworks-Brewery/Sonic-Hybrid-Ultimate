using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static SonicHybridRsdk.Generator.Global;

namespace SonicHybridRsdk.Generator
{
    record GameObject
    {
        public string Name { get; set; }
        public string Path { get; set; }

        public static List<GameObject> Read(Stream stream) =>
            Enumerable.Range(0, stream.ReadByte())
                .Select(x => ReadString(stream))
                .ToList()
                .Select(x => new GameObject
                {
                    Name = x,
                    Path = ReadString(stream)
                })
                .ToList();

        public static void Write(Stream stream, List<GameObject> objects)
        {
            stream.WriteByte(Convert.ToByte(objects.Count));
            foreach (var item in objects)
                WriteString(stream, item.Name);
            foreach (var item in objects)
                WriteString(stream, item.Path);
        }
    }
}
