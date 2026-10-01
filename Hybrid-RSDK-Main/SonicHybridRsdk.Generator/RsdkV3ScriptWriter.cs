using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace SonicHybridRsdk.Generator;

/// <summary>
/// Turns RSDKv3 bytecode into RSDKv4 script text.
/// </summary>
/// <remarks>
/// The bytecode is branch-based: a conditional carries a jump-table slot and
/// jumps forward past its body when the test fails. Structured control flow is
/// recovered from the jump table, which stores pairs - <c>jumpTable[k]</c> is the
/// distance to the false branch and <c>jumpTable[k+1]</c> the distance to the
/// else / loop-back target.
///
/// Output is deliberately conservative. Anything the mapping does not cover is
/// emitted as a <c># TODO</c> comment rather than guessed at, because a
/// silently wrong instruction produces a stage that loads but misbehaves -
/// which is exactly the failure this project shipped for months.
/// </remarks>
public static class RsdkV3ScriptWriter
{
    /// <summary>Opcodes that begin a conditional block.</summary>
    private static readonly HashSet<string> IfOps = new()
    {
        "IfEqual", "IfGreater", "IfGreaterOrEqual", "IfLower", "IfLowerOrEqual", "IfNotEqual"
    };

    /// <summary>Opcodes that begin a while loop.</summary>
    private static readonly HashSet<string> WhileOps = new()
    {
        "WEqual", "WGreater", "WGreaterOrEqual", "WLower", "WLowerOrEqual", "WNotEqual"
    };

    /// <summary>Comparisons whose sense is reversed relative to RSDKv4.</summary>
    private static readonly Dictionary<string, string> FlipCompare = new()
    {
        ["IfEqual"] = "IfNotEqual",
        ["IfNotEqual"] = "IfEqual",
        ["IfGreater"] = "IfLowerOrEqual",
        ["IfGreaterOrEqual"] = "IfLower",
        ["IfLower"] = "IfGreaterOrEqual",
        ["IfLowerOrEqual"] = "IfGreater",
    };

    public sealed class Options
    {
        /// <summary>Name of the object this script belongs to.</summary>
        public string ObjectName { get; init; } = "Object";
        /// <summary>Indentation width.</summary>
        public int Indent { get; init; } = 1;
    }

    /// <summary>
    /// Renders one subroutine (Main / PlayerInteraction / Draw / Setup) as
    /// RSDKv4 script text.
    /// </summary>
    public static string Write(RsdkV3ScriptReader reader, int startPc, IReadOnlyList<int> jumpTable,
                              int jumpTableBase, Options options)
    {
        if (startPc < 0 || startPc >= reader.ScriptCode.Count)
            throw new InvalidOperationException(
                $"subroutine entry point {startPc} is outside the instruction stream " +
                $"({reader.ScriptCode.Count} instructions). The container stores these as " +
                "encoded values, not plain offsets; the encoding is not decoded yet.");

        var byPc = reader.ScriptCode.ToDictionary(i => i.Pc, i => i);
        var sb = new StringBuilder();
        var indent = options.Indent;
        var unresolved = new SortedSet<string>();

        var pcs = reader.ScriptCode.Select(i => i.Pc).ToList();
        var pos = 0;
        var loopStack = new Stack<int>();

        while (pos < pcs.Count)
        {
            var ins = byPc[pcs[pos]];

            // Stop at the enclosing End / EndFunction.
            if (ins.Name is "End" or "EndFunction")
                break;

            if (ins.Operands.Count == 0 && ins.Name.StartsWith("FUNC_", StringComparison.Ordinal) == false)
            {
                // fall through to the generic handling below
            }

            Emit(sb, reader, ins, unresolved);

            // Control flow: if the instruction branched forward, emit `end if`.
            if ((IfOps.Contains(ins.Name) || WhileOps.Contains(ins.Name)) && ins.Operands.Count == 3)
            {
                var slot = (int)ins.Operands[0].IntValue;
                var falseDistance = jumpTable.Count > jumpTableBase + slot
                    ? jumpTable[jumpTableBase + slot]
                    : 0;
                var target = ins.Pc + falseDistance;

                // If there is an `else` before the target, render the else branch too.
                var elsePc = FindAt(byPc, pcs, pos + 1, target);
                if (elsePc >= 0)
                {
                    Emit(sb, reader, byPc[elsePc], unresolved);
                    EmitIndented(sb, indent, "end if");
                }
                else
                {
                    EmitIndented(sb, indent, "end if");
                }
            }
            else if (ins.Name == "endif" || ins.Name == "loop")
            {
                // handled by the block emitter above / by `loop` itself
            }

            pos++;
        }

        if (unresolved.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("// ---------------------------------------------------------------------------");
            sb.AppendLine("// Decompiler could not map these opcodes; review before playing:");
            foreach (var u in unresolved)
                sb.AppendLine($"//   {u}");
            sb.AppendLine("// ---------------------------------------------------------------------------");
        }

        return sb.ToString();
    }

    private static int FindAt(Dictionary<int, RsdkV3ScriptReader.Instruction> byPc,
                              List<int> pcs, int from, int targetPc)
    {
        for (var i = from; i < pcs.Count && pcs[i] < targetPc; ++i)
            if (byPc[pcs[i]].Name == "else")
                return i;
        return -1;
    }

    private static void Emit(StringBuilder sb, RsdkV3ScriptReader reader,
                             RsdkV3ScriptReader.Instruction ins, SortedSet<string> unresolved)
    {
        var args = ins.Operands.Select(o => o.Text).ToArray();

        switch (ins.Name)
        {
            // ---- control -------------------------------------------------
            case "else":
                EmitIndented(sb, 0, "else");
                return;

            case "loop":
                EmitIndented(sb, 0, "loop");
                return;

            // ---- arithmetic / assignment ---------------------------------
            case "Equal":
            case "Add":
            case "Sub":
            case "Mul":
            case "Div":
            case "Mod":
            case "ShR":
            case "ShL":
            case "And":
            case "Or":
            case "Xor":
            {
                var op = ins.Name switch
                {
                    "Equal" => "=", "Add" => "+=", "Sub" => "-=", "Mul" => "*=",
                    "Div" => "/=", "Mod" => "%=", "ShR" => ">>=", "ShL" => "<<=",
                    "And" => "&=", "Or" => "|=", "Xor" => "^=", _ => "=",
                };
                EmitIndented(sb, 0, $"{args[0]} {op} {args[1]}");
                return;
            }

            case "Inc":
                EmitIndented(sb, 0, $"{args[0]}++");
                return;
            case "Dec":
                EmitIndented(sb, 0, $"{args[0]}--");
                return;
            case "FlipSign":
                EmitIndented(sb, 0, $"FlipSign({args[0]})");
                return;
            case "Not":
                EmitIndented(sb, 0, $"{args[0]} = !{args[0]}");
                return;

            // ---- comparisons (result lands in checkResult) ---------------
            case "CheckEqual":
            case "CheckGreater":
            case "CheckLower":
            case "CheckNotEqual":
            {
                var op = ins.Name switch
                {
                    "CheckEqual" => "==", "CheckGreater" => ">",
                    "CheckLower" => "<", _ => "!=",
                };
                EmitIndented(sb, 0, $"checkResult = ({args[0]} {op} {args[1]})");
                return;
            }

            // ---- conditionals --------------------------------------------
            case "IfEqual":
            case "IfGreater":
            case "IfGreaterOrEqual":
            case "IfLower":
            case "IfLowerOrEqual":
            case "IfNotEqual":
            {
                // RSDKv3's If* jumps away when the test FAILS, which is exactly
                // RSDKv4's `if <test>`, so the sense carries over unchanged.
                var op = ins.Name switch
                {
                    "IfEqual" => "==", "IfNotEqual" => "!=", "IfGreater" => ">",
                    "IfGreaterOrEqual" => ">=", "IfLower" => "<", _ => "<=",
                };
                EmitIndented(sb, 0, $"if {args[1]} {op} {args[2]}");
                return;
            }

            // ---- engine calls --------------------------------------------
            case "PlayMusic":
            case "StopMusic":
            case "PauseMusic":
            case "ResumeMusic":
            case "ClearScreen":
            case "DrawSprite":
            case "LoadSpriteSheet":
            case "RemoveSpriteSheet":
            case "PlayStageSfx":
            case "StopStageSfx":
            case "PlayerTileCollision":
            case "ProcessPlayerControl":
            case "ProcessAnimation":
            case "DrawObjectAnimation":
            case "DrawPlayerAnimation":
            case "LoadStage":
            case "SetScreenFade":
            case "SetActivePalette":
            case "DrawSpriteXY":
            case "DrawSpriteScreenXY":
            case "DrawTintRect":
            case "DrawRect":
            case "DrawNumbers":
            case "DrawActName":
            case "DrawMenu":
            case "SpriteFrame":
            case "EditFrame":
            case "LoadPalette":
            case "RotatePalette":
            case "SetPaletteFade":
            case "CopyPalette":
            case "LoadAnimation":
            case "SetupMenu":
            case "AddMenuEntry":
            case "EditMenuEntry":
            case "ResetObjectEntity":
            case "PlayerObjectCollision":
            case "CreateTempObject":
            case "BindPlayerToObject":
            case "SetMusicTrack":
            case "PlaySfx":
            case "StopSfx":
            case "SetSfxAttributes":
            case "ObjectTileCollision":
            case "ObjectTileGrip":
            case "CallFunction":
            case "CheckTouchRect":
            case "GetTileLayerEntry":
            case "SetTileLayerEntry":
            case "GetBit":
            case "SetBit":
            case "ClearDrawList":
            case "AddDrawListEntityRef":
            case "GetDrawListEntityRef":
            case "SetDrawListEntityRef":
            case "Get16x16TileInfo":
            case "Copy16x16Tile":
            case "Set16x16TileInfo":
            case "GetAnimationByName":
            case "ReadSaveRAM":
            case "WriteSaveRAM":
            case "LoadTextFont":
            case "LoadTextFile":
            case "DrawText":
            case "GetTextInfo":
                EmitIndented(sb, 0, $"{ins.Name}({string.Join(", ", args)})");
                return;

            default:
                // Record rather than guess.
                unresolved.Add($"pc {ins.Pc}: {ins.Name}({string.Join(", ", args)})");
                EmitIndented(sb, 0, $"// TODO: {ins.Name} {string.Join(" ", args)}");
                return;
        }
    }

    private static void EmitIndented(StringBuilder sb, int depth, string text)
    {
        for (var i = 0; i < depth; ++i)
            sb.Append('\t');
        sb.Append(text).Append('\n');
    }
}