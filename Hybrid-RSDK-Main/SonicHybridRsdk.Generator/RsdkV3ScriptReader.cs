using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SonicHybridRsdk.Generator;

/// <summary>
/// Reader for RSDKv3 compiled script bytecode, as shipped in Sonic CD's
/// <c>Data/Scripts/ByteCode/*.bin</c>.
/// </summary>
/// <remarks>
/// Sonic CD has no text scripts - its gameplay ships as bytecode for the RSDKv3
/// virtual machine, so nothing in it can run on the RSDKv4 engine as-is. This
/// type parses that bytecode into structured instructions, which is the input to
/// <see cref="RsdkV3ScriptWriter"/> (which emits RSDKv4 text).
///
/// Container layout, mirrored from <c>LoadBytecode()</c> in the RSDKv3
/// decompilation (RSDKV3/RSDKv3/Script.cpp):
///
/// <code>
///   u32  scriptCodeCount
///   block-encoded words  - the opcode stream
///   u32  jumpTableCount
///   block-encoded words  - branch targets
///   u16  scriptCount
///     per script: 5 x u32   (scriptCodePtr + ptrs for Main/PlayerInteraction/Draw/Startup)
///     per script: 4 x u32   (jumpTablePtr for the same four subroutines)
///   u16  functionCount
///     per function: u32 scriptCodePtr, u32 jumpTablePtr
/// </code>
///
/// Values are block-encoded: a length byte whose bit 7 selects the width, and
/// whose low 7 bits give the element count. Wide blocks hold 32-bit
/// little-endian words, narrow blocks hold single bytes (zero-extended).
/// </remarks>
public sealed class RsdkV3ScriptReader
{
    public const int TagVariable = 1;
    public const int TagIntConstant = 2;
    public const int TagStringConstant = 3;


    public List<Instruction> ScriptCode { get; } = new();
    public List<int> JumpTable { get; } = new();
    public List<ScriptEntry> Scripts { get; } = new();
    public List<FunctionEntry> Functions { get; } = new();

    /// <summary>Name of the bytecode file this was read from, if known.</summary>
    public string? SourceName { get; private set; }

    public sealed record Operand
    {
        public string Kind { get; init; } = "";
        public string Text { get; init; } = "";
        public long IntValue { get; init; }
        public string? StringValue { get; init; }
        public override string ToString() => Text;
    }

    public sealed record Instruction(int Pc, int Opcode, string Name, IReadOnlyList<Operand> Operands);

    public sealed record ScriptEntry(string Name, uint[] ScriptCodePtrs, uint[] JumpTablePtrs);

    /// <summary>Bytes consumed by the whole container.</summary>
    public int BytesConsumed { get; private set; }

    /// <summary>Sentinel stored for a subroutine that does not exist.</summary>
    public const uint NoSubroutine = 0x3FFFF;

    /// <summary>
    /// Raw word count of the opcode stream, which is what the base offsets are
    /// measured in. This is NOT <see cref="ScriptCode"/>'s count: that is the
    /// number of decoded instructions, and each instruction spans several words
    /// (opcode plus tagged operands), so the two differ by roughly 6x.
    /// </summary>
    public int WordCount { get; private set; }

    /// <summary>Raw word count of the jump table.</summary>
    public int JumpWordCount { get; private set; }

    /// <summary>
    /// Global <c>scriptCode[]</c> index at which this file's code begins.
    /// </summary>
    /// <remarks>
    /// The engine keeps one global scriptCode array and appends each bytecode
    /// file to it, only resetting via ClearScriptData(). GS000.bin (the global
    /// object code) is loaded first, so every subsequent stage file's stored
    /// entry points are indices into that shared array - not into this file.
    /// RS019.bin, for example, stores its first entry point as 34554, which is
    /// exactly GS000's word count.
    /// </remarks>
    public int ScriptCodeBase { get; set; }

    /// <summary>Global <c>jumpTable[]</c> index at which this file's branches begin.</summary>
    public int JumpTableBase { get; set; }

    /// <summary>Converts a stored (global) pointer into a local index, or -1.</summary>
    public static int ToLocal(uint globalPointer, int baseIndex)
    {
        if (globalPointer == NoSubroutine)
            return -1;
        var local = (int)(long)globalPointer - baseIndex;
        return local < 0 ? -1 : local;
    }

    /// <summary>Resolved subroutine entry points for one script, in wire order.</summary>
    public sealed record ResolvedScript(string Name, int[] ScriptCodePtrs, int[] JumpTablePtrs);

    public ResolvedScript Resolve(ScriptEntry entry, int index)
        => new($"script{index}",
               entry.ScriptCodePtrs.Select(p => ToLocal(p, ScriptCodeBase)).ToArray(),
               entry.JumpTablePtrs.Select(p => ToLocal(p, JumpTableBase)).ToArray());

    public sealed record FunctionEntry(string Name, uint ScriptCodePtr, uint JumpTablePtr);

    /// <summary>RSDKv4 name for each RSDKv3 variable index; null where none exists.</summary>
    private static readonly string?[] VariableNames = RsdkV3Variables.V4Names;

    private static string EnumName(int varId)
        => varId >= 0 && varId < RsdkV3Variables.EnumNames.Length
            ? RsdkV3Variables.EnumNames[varId]
            : "index " + varId;

    /// <summary>Opcode name and operand count, in opcode order.</summary>
    private static readonly (string Name, int Operands)[] Opcodes = BuildOpcodeTable();

    public static RsdkV3ScriptReader Load(string path)
    {
        var reader = new RsdkV3ScriptReader { SourceName = Path.GetFileName(path) };
        reader.Parse(File.ReadAllBytes(path));
        return reader;
    }

    private void Parse(byte[] data)
    {
        int pos = 0;
        var code = ReadBlocks(data, ref pos, (int)ReadU32(data, ref pos));
        WordCount = code.Count;
        JumpTable.AddRange(ReadBlocks(data, ref pos, (int)ReadU32(data, ref pos)));
        JumpWordCount = JumpTable.Count;

        if (pos + 2 > data.Length)
            throw new InvalidDataException($"{SourceName}: no script table (file is {data.Length} bytes)");
        var scriptCount = data[pos] | (data[pos + 1] << 8);
        pos += 2;
        _scriptCount = scriptCount;
        var codePtrs = new List<uint[]>();
        for (var s = 0; s < scriptCount; ++s)
        {
            // Four scriptCode pointers per script: Main, PlayerInteraction,
            // Draw, Startup (RSDKV3 Script.cpp LoadBytecode).
            codePtrs.Add(new[]
            {
                ReadU32(data, ref pos), ReadU32(data, ref pos),
                ReadU32(data, ref pos), ReadU32(data, ref pos)
            });
        }

        var jumpPtrs = new List<uint[]>();
        for (var s = 0; s < scriptCount; ++s)
        {
            jumpPtrs.Add(new[]
            {
                ReadU32(data, ref pos), ReadU32(data, ref pos),
                ReadU32(data, ref pos), ReadU32(data, ref pos)
            });
        }

        for (var s = 0; s < scriptCount; ++s)
            Scripts.Add(new ScriptEntry($"script{s}", codePtrs[s], jumpPtrs[s]));

        var functionCount = data[pos] | (data[pos + 1] << 8);
        pos += 2;
        for (var f = 0; f < functionCount; ++f)
        {
            var codePtr = ReadU32(data, ref pos);
            var jumpPtr = ReadU32(data, ref pos);
            Functions.Add(new FunctionEntry($"func{f}", codePtr, jumpPtr));
        }

        BytesConsumed = pos;

        Decode(code);
    }

    private void Decode(List<int> code)
    {
        var pc = 0;
        while (pc < code.Count)
        {
            var start = pc;
            var opcode = code[pc++];
            if (opcode < 0 || opcode >= Opcodes.Length)
            {
                var recent = ScriptCode.Skip(Math.Max(0, ScriptCode.Count - 8))
                                   .Select(i => $"    {i.Pc}: {i.Name} {string.Join(", ", i.Operands)}");
                throw new InvalidDataException(
                    $"{SourceName}: invalid opcode {opcode} at pc {start} (stream has {code.Count} words).\n" +
                    "  last decoded instructions:\n" + string.Join("\n", recent));
            }

            var (name, count) = Opcodes[opcode];
            var operands = new List<Operand>(count);
            for (var i = 0; i < count && pc < code.Count; ++i)
                operands.Add(ReadOperand(code, ref pc));

            ScriptCode.Add(new Instruction(start, opcode, name, operands));
        }
    }

    private int Read(List<int> code, ref int pc)
    {
        if (pc >= code.Count)
            throw new InvalidDataException(
                $"{SourceName}: operand runs past the end of the script stream " +
                $"({code.Count} words) at pc {pc}");
        return code[pc++];
    }

    private Operand ReadOperand(List<int> code, ref int pc)
    {
        var tag = Read(code, ref pc);
        switch (tag)
        {
            case TagIntConstant:
                var value = Read(code, ref pc);
                return new Operand { Kind = "int", IntValue = value, Text = value.ToString() };

            case TagStringConstant:
            {
                var length = Read(code, ref pc);
                var chars = new char[length];

                // NB: the engine only advances its cursor on "c % 4 == 3" and then
                // increments once more at the end, so an N-character string consumes
                // floor(N / 4) + 1 words - not ceil(N / 4). Getting this wrong
                // desynchronises the whole stream one word at a time.
                var words = (length / 4) + 1;
                if (pc + words > code.Count)
                    throw new InvalidDataException(
                        $"{SourceName}: string constant at pc {pc - 2} runs past the end of the stream");
                for (var c = 0; c < length; ++c)
                {
                    // 4 characters per word, most significant byte first.
                    var word = code[pc + (c / 4)];
                    var shift = 24 - 8 * (c % 4);
                    chars[c] = (char)((word >> shift) & 0xFF);
                }
                pc += words;
                var text = new string(chars);
                return new Operand { Kind = "string", StringValue = text, Text = "\"" + text + "\"" };
            }

            case TagVariable:
            {
                var arrayMode = Read(code, ref pc);
                string index;
                switch (arrayMode)
                {
                    case 0:
                        index = string.Empty;
                        break;
                    case 1:
                    case 2:
                    case 3:
                    {
                        var slot = Read(code, ref pc);
                        var indirect = Read(code, ref pc) == 1;
                        var refName = indirect ? (slot == 1 ? "arrayPos0" : slot.ToString()) : slot.ToString();
                        var sign = arrayMode == 2 ? "+" : arrayMode == 3 ? "-" : "";
                        index = $"[{sign}arrayPos{(indirect ? "0" : "")}{(indirect ? "" : "")}]";
                        if (!indirect)
                            index = $"[{sign}{slot}]";
                        else
                            index = $"[{sign}arrayPos{slot - 1}]";
                        break;
                    }
                    default:
                        index = string.Empty;
                        break;
                }

                var varId = Read(code, ref pc);
                // Names come from the table generated off the engine source. A null
                // entry means RSDKv3 exposes something RSDKv4 has no equivalent for;
                // those are labelled loudly instead of being given a wrong name.
                var varName = varId >= 0 && varId < VariableNames.Length
                    ? VariableNames[varId]
                    : null;
                var full = varName is null
                    ? $"/*UNMAPPED v3 {EnumName(varId)}*/tempValue0{index}"
                    : varName + index;
                return new Operand { Kind = "var", Text = full, IntValue = varId };
            }

            default:
                throw new InvalidDataException($"{SourceName}: unknown operand tag {tag} at pc {pc - 1}");
        }
    }

    private uint ReadU32(byte[] data, ref int pos)
    {
        if (pos + 4 > data.Length)
            throw new InvalidDataException(
                $"{SourceName}: script table runs past the end of the file " +
                $"(need 4 bytes at offset {pos}, file is {data.Length} bytes; " +
                $"scriptCount={_scriptCount}, functions read so far={Functions.Count})");
        var v = (uint)(data[pos] | (data[pos + 1] << 8) | (data[pos + 2] << 16) | (data[pos + 3] << 24));
        pos += 4;
        return v;
    }

    private int _scriptCount;

    private static List<int> ReadBlocks(byte[] data, ref int pos, int count)
    {
        var outp = new List<int>(count);
        while (outp.Count < count)
        {
            if (pos >= data.Length)
                throw new InvalidDataException("truncated block stream");
            var header = data[pos++];
            var n = header & 0x7F;
            if (header >= 0x80)
            {
                for (var i = 0; i < n; ++i)
                {
                    outp.Add((int)((uint)data[pos] | ((uint)data[pos + 1] << 8) |
                                   ((uint)data[pos + 2] << 16) | ((uint)data[pos + 3] << 24)));
                    pos += 4;
                }
            }
            else
            {
                for (var i = 0; i < n; ++i)
                    outp.Add(data[pos++]);
            }
        }
        return outp;
    }

    // ------------------------------------------------------------------
    // Opcode table: copied in opcode order from the RSDKv3 decompilation's
    // `const FunctionInfo functions[]` table (RSDKV3/RSDKv3/Script.cpp).
    // The array index IS the opcode number.
    // ------------------------------------------------------------------
    private static (string, int)[] BuildOpcodeTable() => new (string, int)[]
    {
        ("End",0),("Equal",2),("Add",2),("Sub",2),("Inc",1),("Dec",1),("Mul",2),("Div",2),
        ("ShR",2),("ShL",2),("And",2),("Or",2),("Xor",2),("Mod",2),("FlipSign",1),
        ("CheckEqual",2),("CheckGreater",2),("CheckLower",2),("CheckNotEqual",2),
        ("IfEqual",3),("IfGreater",3),("IfGreaterOrEqual",3),("IfLower",3),("IfLowerOrEqual",3),
        ("IfNotEqual",3),("else",0),("endif",0),
        ("WEqual",3),("WGreater",3),("WGreaterOrEqual",3),("WLower",3),("WLowerOrEqual",3),
        ("WNotEqual",3),("loop",0),("switch",2),("break",0),("endswitch",0),
        ("Rand",2),("Sin",2),("Cos",2),("Sin256",2),("Cos256",2),("SinChange",5),("CosChange",5),
        ("ATan2",3),("Interpolate",4),("InterpolateXY",7),
        ("LoadSpriteSheet",1),("RemoveSpriteSheet",1),("DrawSprite",1),("DrawSpriteXY",3),
        ("DrawSpriteScreenXY",3),("DrawTintRect",4),("DrawNumbers",7),("DrawActName",7),
        ("DrawMenu",3),("SpriteFrame",6),("EditFrame",7),
        ("LoadPalette",5),("RotatePalette",3),("SetScreenFade",4),("SetActivePalette",3),
        ("SetPaletteFade",7),("CopyPalette",2),("ClearScreen",1),("DrawSpriteFX",4),
        ("DrawSpriteScreenFX",4),("LoadAnimation",1),("SetupMenu",4),("AddMenuEntry",3),
        ("EditMenuEntry",4),("LoadStage",0),("DrawRect",8),("ResetObjectEntity",5),
        ("PlayerObjectCollision",5),("CreateTempObject",4),("BindPlayerToObject",2),
        ("PlayerTileCollision",0),("ProcessPlayerControl",0),("ProcessAnimation",0),
        ("DrawObjectAnimation",0),("DrawPlayerAnimation",0),
        ("SetMusicTrack",3),("PlayMusic",1),("StopMusic",0),("PlaySfx",2),("StopSfx",1),
        ("SetSfxAttributes",3),("ObjectTileCollision",4),("ObjectTileGrip",4),
        ("LoadVideo",1),("NextVideoFrame",0),("PlayStageSfx",2),("StopStageSfx",1),("Not",1),
        ("Draw3DScene",0),("SetIdentityMatrix",1),("MatrixMultiply",2),("MatrixTranslateXYZ",4),
        ("MatrixScaleXYZ",4),("MatrixRotateX",2),("MatrixRotateY",2),("MatrixRotateZ",2),
        ("MatrixRotateXYZ",4),("TransformVertices",3),("CallFunction",1),("EndFunction",0),
        ("SetLayerDeformation",6),("CheckTouchRect",4),("GetTileLayerEntry",4),("SetTileLayerEntry",4),
        ("GetBit",3),("SetBit",3),("PauseMusic",0),("ResumeMusic",0),("ClearDrawList",1),
        ("AddDrawListEntityRef",2),("GetDrawListEntityRef",3),("SetDrawListEntityRef",3),
        ("Get16x16TileInfo",4),("Copy16x16Tile",2),("Set16x16TileInfo",4),("GetAnimationByName",2),
        ("ReadSaveRAM",0),("WriteSaveRAM",0),("LoadTextFont",1),("LoadTextFile",3),("DrawText",7),
        ("GetTextInfo",5),("GetVersionNumber",2),("SetAchievement",2),("SetLeaderboard",2),
        ("LoadOnlineMenu",1),("EngineCallback",1),("HapticEffect",4),
    };



}
