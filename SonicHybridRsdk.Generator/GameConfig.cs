using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xe.BinaryMapper;

namespace SonicHybridRsdk.Generator
{
    record GameConfig
    {
        public record GameObject
        {
            public string Name { get; set; }
            public string Path { get; set; }
        }

        public record Variable
        {
            [Data] public string Name { get; set; }
            [Data] public int Value { get; set; }
        }

        public record Stage
        {
            [Data] public string Path { get; set; }
            [Data] public string Act { get; set; }
            [Data] public string Name { get; set; }
            [Data] public byte Mode { get; set; }
        }

        public static IBinaryMapping Mapper;

        [Data] public string Name { get; set; }
        [Data] public string Description { get; set; }
        [Data(Count = 0x120)] public byte[] PaletteData { get; set; }
        public List<GameObject> GameObjects { get; set; }
        public List<Variable> Variables { get; set; }
        public List<GameObject> SoundEffects { get; set; }
        public List<string> Players { get; set; }
        public List<Stage> StagesPresentation { get; set; }
        public List<Stage> StagesRegular { get; set; }
        public List<Stage> StagesSpecial { get; set; }
        public List<Stage> StagesBonus { get; set; }

        static GameConfig()
        {
            Mapper = MappingConfiguration
                .DefaultConfiguration(Encoding.UTF8, true)
                .ForType<ushort>(ReadUInt16BigEndian, WriteUInt16BigEndian)
                .ForType<int>(ReadInt32BigEndian, WriteInt32BigEndian)
                .ForType<string>(ReadString, WriteString)
                .Build();
        }

        public static GameConfig Read(Stream stream)
        {
            var gameConfig = Mapper.ReadObject<GameConfig>(stream);
            gameConfig.GameObjects = ReadGameObjects(stream);
            gameConfig.Variables = ReadItems<Variable>(stream);
            gameConfig.SoundEffects = ReadGameObjects(stream);
            gameConfig.Players = Enumerable.Range(0, stream.ReadByte())
                .Select(_ => ReadString(stream))
                .ToList();
            gameConfig.StagesPresentation = ReadItems<Stage>(stream);
            gameConfig.StagesRegular = ReadItems<Stage>(stream);
            gameConfig.StagesSpecial = ReadItems<Stage>(stream);
            gameConfig.StagesBonus = ReadItems<Stage>(stream);

            return gameConfig;
        }

        public void Write(Stream stream)
        {
            Mapper.WriteObject(stream, this);
            WriteGameObjects(stream, GameObjects);
            WriteItems(stream, Variables);
            WriteGameObjects(stream, SoundEffects);

            stream.WriteByte(Convert.ToByte(Players.Count));
            foreach (var item in Players)
                WriteString(stream, item);

            WriteItems(stream, StagesPresentation);
            WriteItems(stream, StagesRegular);
            WriteItems(stream, StagesSpecial);
            WriteItems(stream, StagesBonus);
        }

        private static List<GameObject> ReadGameObjects(Stream stream) =>
            Enumerable.Range(0, stream.ReadByte())
                .Select(x => ReadString(stream))
                .ToList()
                .Select(x => new GameObject
                {
                    Name = x,
                    Path = ReadString(stream)
                })
                .ToList();

        private static void WriteGameObjects(Stream stream, List<GameObject> objects)
        {
            stream.WriteByte(Convert.ToByte(objects.Count));
            foreach (var item in objects)
                WriteString(stream, item.Name);
            foreach (var item in objects)
                WriteString(stream, item.Path);
        }

        private static List<T> ReadItems<T>(Stream stream) where T : class =>
            Enumerable.Range(0, stream.ReadByte())
                .Select(_ => Mapper.ReadObject<T>(stream))
                .ToList();

        private static void WriteItems<T>(Stream stream, List<T> items) where T : class
        {
            stream.WriteByte(Convert.ToByte(items.Count));
            foreach (var item in items)
                Mapper.WriteObject<T>(stream, item);
        }

        private static string ReadString(Stream stream)
        {
            var data = new byte[stream.ReadByte()];
            stream.Read(data);
            return Encoding.UTF8.GetString(data);
        }

        private static void WriteString(Stream stream, string str)
        {
            if (str.Length >= byte.MaxValue)
                str = str[..byte.MaxValue];

            var data = Encoding.UTF8.GetBytes(str);
            stream.WriteByte((byte)data.Length);
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
