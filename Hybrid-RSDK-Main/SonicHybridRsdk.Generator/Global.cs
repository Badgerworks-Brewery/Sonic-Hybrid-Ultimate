using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xe.BinaryMapper;
using static SonicHybridRsdk.Generator.Global;

namespace SonicHybridRsdk.Generator
{
    static class Global
    {
        public static IBinaryMapping Mapper;

        static Global()
        {
            Mapper = MappingConfiguration
                .DefaultConfiguration(Encoding.UTF8, true)
                .ForType<ushort>(ReadUInt16BigEndian, WriteUInt16BigEndian)
                .ForType<int>(ReadInt32BigEndian, WriteInt32BigEndian)
                .ForType<string>(ReadString, WriteString)
                .Build();
        }

        public static void Copy(string srcPath, string dstPath)
        {
            Directory.CreateDirectory(dstPath);
            foreach (var filePath in Directory.EnumerateFiles(srcPath))
            {
                var dstFilePath = Path.Combine(dstPath, Path.GetFileName(filePath));
                File.Copy(filePath, dstFilePath, true);
            }

            foreach (var directoryPath in Directory.EnumerateDirectories(srcPath))
            {
                var dstDirectoryPath = Path.Combine(dstPath, Path.GetFileName(directoryPath));
                Copy(directoryPath, dstDirectoryPath);
            }
        }

        public static T OpenRead<T>(string fileName, Func<Stream, T> func)
        {
            using var stream = File.OpenRead(fileName);
            return func(stream);
        }

        public static void Create(string fileName, Action<Stream> func)
        {
            var directoryPath = Path.GetDirectoryName(fileName);
            Directory.CreateDirectory(directoryPath);

            using var stream = File.Create(fileName);
            func(stream);
        }

        public static List<Stage> GetStages(this IGameConfig config, StageType stageType) => stageType switch
        {
            StageType.StagesPresentation => config.StagesPresentation,
            StageType.StagesRegular => config.StagesRegular,
            StageType.StagesSpecial => config.StagesSpecial,
            _ => throw new ArgumentException("Stage type not recognised"),
        };

        public static List<T> ReadItems<T>(Stream stream) where T : class =>
            Enumerable.Range(0, stream.ReadByte())
                .Select(_ => Mapper.ReadObject<T>(stream))
                .ToList();

        public static void WriteItems<T>(Stream stream, List<T> items) where T : class
        {
            stream.WriteByte(Convert.ToByte(items.Count));
            foreach (var item in items)
                Mapper.WriteObject<T>(stream, item);
        }

        public static string ReadString(Stream stream)
        {
            var data = new byte[stream.ReadByte()];
            stream.Read(data);
            return Encoding.UTF8.GetString(data);
        }

        public static void WriteString(Stream stream, string str)
        {
            if (str.Length >= byte.MaxValue)
                str = str[..byte.MaxValue];

            var data = Encoding.UTF8.GetBytes(str);
            stream.WriteByte((byte)data.Length);
            stream.Write(data);
        }

        public static short FlipEndian(short value)
        {
            var data = BitConverter.GetBytes(value);
            var ch = data[0];
            data[0] = data[1];
            data[1] = ch;
            return BitConverter.ToInt16(data);
        }

        public static ushort FlipEndian(ushort value)
        {
            var data = BitConverter.GetBytes(value);
            var ch = data[0];
            data[1] = data[0];
            data[1] = ch;
            return BitConverter.ToUInt16(data);
        }

        public static ushort ReadUInt16BigEndian(Stream stream)
        {
            var data = new byte[2];
            stream.Read(data);
            return BitConverter.ToUInt16(data);
        }

        public static short ReadInt16BigEndian(Stream stream)
        {
            var data = new byte[2];
            stream.Read(data);
            return BitConverter.ToInt16(data);
        }

        public static void WriteUInt16BigEndian(Stream stream, ushort item)
        {
            var data = BitConverter.GetBytes(item);
            stream.Write(data);
        }

        public static void WriteInt16BigEndian(Stream stream, short item)
        {
            var data = BitConverter.GetBytes(item);
            stream.Write(data);
        }

        private static string ReadString(MappingReadArgs arg) =>
            ReadString(arg.Reader.BaseStream);

        private static void WriteString(MappingWriteArgs arg) =>
            WriteString(arg.Writer.BaseStream, arg.Item as string);

        private static object ReadUInt16BigEndian(MappingReadArgs arg)
        {
            var data = arg.Reader.ReadBytes(2);
            return BitConverter.ToUInt16(data);
        }

        private static void WriteUInt16BigEndian(MappingWriteArgs arg)
        {
            var data = BitConverter.GetBytes((ushort)arg.Item);
            arg.Writer.Write(data);
        }

        private static object ReadInt32BigEndian(MappingReadArgs arg)
        {
            var data = arg.Reader.ReadBytes(4);
            Array.Reverse(data);
            return BitConverter.ToInt32(data);
        }

        private static void WriteInt32BigEndian(MappingWriteArgs arg)
        {
            var data = BitConverter.GetBytes((int)arg.Item);
            Array.Reverse(data);
            arg.Writer.Write(data);
        }
    }
}
