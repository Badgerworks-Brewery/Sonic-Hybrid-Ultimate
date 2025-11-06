using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xe.BinaryMapper;
using static SonicHybridRsdk.Generator.Global;

namespace SonicHybridRsdk.Generator
{
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

    interface IGameConfig
    {
        string Description { get; set; }
        List<GameObject> GameObjects { get; set; }
        string Name { get; set; }
        byte[] PaletteData { get; }
        List<string> Players { get; set; }
        List<GameObject> SoundEffects { get; set; }
        List<Stage> StagesBonus { get; set; }
        List<Stage> StagesPresentation { get; set; }
        List<Stage> StagesRegular { get; set; }
        List<Stage> StagesSpecial { get; set; }
        List<Variable> Variables { get; set; }

        void Write(Stream stream);
    }

    record GameConfig : IGameConfig
    {
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

        public static GameConfig Read(Stream stream)
        {
            var gameConfig = Mapper.ReadObject<GameConfig>(stream);
            gameConfig.GameObjects = GameObject.Read(stream);
            gameConfig.Variables = ReadItems<Variable>(stream);
            gameConfig.SoundEffects = GameObject.Read(stream);
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
            GameObject.Write(stream, GameObjects);
            WriteItems(stream, Variables);
            GameObject.Write(stream, SoundEffects);

            stream.WriteByte(Convert.ToByte(Players.Count));
            foreach (var item in Players)
                WriteString(stream, item);

            WriteItems(stream, StagesPresentation);
            WriteItems(stream, StagesRegular);
            WriteItems(stream, StagesSpecial);
            WriteItems(stream, StagesBonus);
        }

        private static List<T> ReadItems<T>(Stream stream) where T : class =>
            Enumerable.Range(0, stream.ReadByte())
                .Select(_ => Mapper.ReadObject<T>(stream))
                .ToList();

        private static void WriteItems<T>(Stream stream, List<T> items) where T : class
        {
            stream.WriteByte(Convert.ToByte(items.Count));
            foreach (var item in items)
                Mapper.WriteObject(stream, item);
        }
    }

    record GameConfigV3 : IGameConfig
    {
        [Data] public string Name { get; set; }
        [Data] public string Path { get; set; }
        [Data] public string Description { get; set; }
        public byte[] PaletteData { get; }
        public List<GameObject> GameObjects { get; set; }
        public List<Variable> Variables { get; set; }
        public List<GameObject> SoundEffects { get; set; }
        public List<string> Players { get; set; }
        public List<Stage> StagesPresentation { get; set; }
        public List<Stage> StagesRegular { get; set; }
        public List<Stage> StagesSpecial { get; set; }
        public List<Stage> StagesBonus { get; set; }

        public static GameConfigV3 Read(Stream stream)
        {
            var gameConfig = Mapper.ReadObject<GameConfigV3>(stream);
            gameConfig.GameObjects = GameObject.Read(stream);
            gameConfig.Variables = ReadItems<Variable>(stream);
            gameConfig.SoundEffects = Enumerable.Range(0, stream.ReadByte())
                .Select(_ => ReadString(stream))
                .Select(x => new GameObject
                {
                    Name = System.IO.Path.GetFileNameWithoutExtension(x),
                    Path = x,
                })
                .ToList();
            gameConfig.Players = Enumerable.Range(0, stream.ReadByte())
                .Select(_ => ReadString(stream))
                .ToList();
            gameConfig.StagesPresentation = ReadItems<Stage>(stream);
            gameConfig.StagesRegular = ReadItems<Stage>(stream);
            gameConfig.StagesSpecial = ReadItems<Stage>(stream);
            gameConfig.StagesBonus = ReadItems<Stage>(stream);

            return gameConfig;
        }

        public void Write(Stream stream) => throw new NotImplementedException();
    }
}
