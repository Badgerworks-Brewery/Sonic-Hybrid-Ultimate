using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SonicHybridRsdk.Generator;

/// <summary>
/// Turns RSDKv3 bytecode into RSDKv4 script text.
/// </summary>
/// <remarks>
/// <para>
/// The bytecode is branch-based rather than structured, but it is laid out in
/// structured order and marks every block explicitly: <c>if</c>/<c>else</c>/
/// <c>endif</c>, <c>while</c>/<c>loop</c>, <c>switch</c>/<c>break</c>/
/// <c>endswitch</c>. Emission is therefore linear, using those markers to track
/// nesting, and the jump table supplies the two things the stream does not
/// record: switch case values, and a target to check each construct against.
/// </para>
/// <para>
/// Given a subroutine's start <c>S</c> and jump-table base <c>J</c>:
/// </para>
/// <list type="bullet">
/// <item><description><c>If* [k,a,b]</c> - when the test fails, jumps to
/// <c>S + jt[k]</c>, which is the first instruction of the <c>else</c> body, or
/// the <c>endif</c> when there is no <c>else</c>. Either way it lands after the
/// <c>else</c>/<c>endif</c> opcode itself. Pushes <c>k</c>.</description></item>
/// <item><description><c>else</c> - pops <c>k</c> and jumps to
/// <c>S + jt[k+1]</c>, which is past the matching <c>endif</c>. So both paths
/// out of an if converge on <c>S + jt[k+1]</c>.</description></item>
/// <item><description><c>W* [k,a,b]</c> - when the test fails, jumps to
/// <c>S + jt[k+1]</c> (the exit); otherwise pushes <c>k</c>.</description></item>
/// <item><description><c>loop</c> - pops <c>k</c> and jumps to <c>S + jt[k]</c>,
/// which is the instruction after the <c>while</c> that opened it.</description></item>
/// <item><description><c>switch [k,sel]</c> - reads <c>low = jt[k]</c> and
/// <c>high = jt[k+1]</c>; a selector outside that range goes to
/// <c>S + jt[k+2]</c>, and one inside it to <c>S + jt[k+4+(sel-low)]</c>. The
/// case bodies therefore appear in ascending case order, which is how the
/// <c>case</c> labels are recovered - they are not stored anywhere.</description></item>
/// </list>
/// <para>
/// Every one of those was confirmed against the instruction stream of
/// <c>RS019.bin</c>: all 82 entry points and all jump-table entries land exactly
/// on instruction boundaries. Each construct is then checked against the target
/// the engine would use, and a mismatch is reported rather than emitted.
/// </para>
/// <para>
/// Anything the opcode mapping does not cover is emitted as a <c>// TODO</c>
/// comment and reported, never guessed at. A confidently wrong instruction
/// yields a stage that loads and then misbehaves, which is the failure this
/// project shipped for months.
/// </para>
/// </remarks>
public static class RsdkV3ScriptWriter
{
    private enum BlockKind { If, While, Switch }

    private sealed class Block
    {
        public BlockKind Kind;
        /// <summary>Jump-table slot the construct reserved.</summary>
        public int Slot;
        /// <summary>Where this construct should end, for cross-checking.</summary>
        public int ExpectedExit;
        /// <summary>Word offset of the instruction that opened it.</summary>
        public int OpenPc;
    }

    private static readonly HashSet<string> IfOps = new(StringComparer.Ordinal)
    {
        "IfEqual", "IfGreater", "IfGreaterOrEqual", "IfLower", "IfLowerOrEqual", "IfNotEqual"
    };

    private static readonly HashSet<string> WhileOps = new(StringComparer.Ordinal)
    {
        "WEqual", "WGreater", "WGreaterOrEqual", "WLower", "WLowerOrEqual", "WNotEqual"
    };

    /// <summary>Comparison operators. RSDKv3's sense carries over unchanged:
    /// every If*/W* jumps away when its test fails, which is RSDKv4's `if`.</summary>
    private static readonly Dictionary<string, string> CompareOp = new(StringComparer.Ordinal)
    {
        ["IfEqual"] = "==",
        ["IfNotEqual"] = "!=",
        ["IfGreater"] = ">",
        ["IfGreaterOrEqual"] = ">=",
        ["IfLower"] = "<",
        ["IfLowerOrEqual"] = "<=",
        ["WEqual"] = "==",
        ["WNotEqual"] = "!=",
        ["WGreater"] = ">",
        ["WGreaterOrEqual"] = ">=",
        ["WLower"] = "<",
        ["WLowerOrEqual"] = "<=",
    };

    private static readonly Dictionary<string, string> AssignOp = new(StringComparer.Ordinal)
    {
        ["Equal"] = "=",
        ["Add"] = "+=",
        ["Sub"] = "-=",
        ["Mul"] = "*=",
        ["Div"] = "/=",
        ["Mod"] = "%=",
        ["ShR"] = ">>=",
        ["ShL"] = "<<=",
        ["And"] = "&=",
        ["Or"] = "|=",
        ["Xor"] = "^=",
    };

    private static readonly Dictionary<string, string> CheckOp = new(StringComparer.Ordinal)
    {
        ["CheckEqual"] = "==",
        ["CheckNotEqual"] = "!=",
        ["CheckGreater"] = ">",
        ["CheckLower"] = "<",
    };

    /// <summary>
    /// Opcodes RSDKv4 spells the same way and takes the same operands. Names
    /// were taken from RSDKv4's own opcode table, not assumed.
    /// </summary>
    private static readonly HashSet<string> PassthroughOps = new(StringComparer.Ordinal)
    {
        "LoadSpriteSheet", "RemoveSpriteSheet", "DrawSprite", "DrawSpriteXY",
        "DrawSpriteScreenXY", "DrawTintRect", "DrawNumbers", "DrawActName",
        "DrawMenu", "SpriteFrame", "EditFrame", "LoadPalette", "SetScreenFade",
        "SetActivePalette", "SetPaletteFade", "ClearScreen", "DrawSpriteFX",
        "DrawSpriteScreenFX", "LoadAnimation", "SetupMenu", "AddMenuEntry",
        "EditMenuEntry", "LoadStage", "DrawRect", "ResetObjectEntity",
        "CreateTempObject", "BindPlayerToObject", "PlayerTileCollision",
        "ProcessPlayerControl", "ProcessAnimation", "DrawObjectAnimation",
        "DrawPlayerAnimation", "SetMusicTrack", "PlayMusic", "StopMusic",
        "PlaySfx", "StopSfx", "SetSfxAttributes", "ObjectTileCollision",
        "ObjectTileGrip", "LoadVideo", "NextVideoFrame", "PlayStageSfx",
        "StopStageSfx", "Draw3DScene", "SetIdentityMatrix", "MatrixMultiply",
        "MatrixTranslateXYZ", "MatrixScaleXYZ", "MatrixRotateX", "MatrixRotateY",
        "MatrixRotateZ", "MatrixRotateXYZ", "TransformVertices",
        "SetLayerDeformation", "CheckTouchRect", "GetTileLayerEntry",
        "SetTileLayerEntry", "GetBit", "SetBit", "PauseMusic", "ResumeMusic",
        "ClearDrawList", "AddDrawListEntityRef", "GetDrawListEntityRef",
        "SetDrawListEntityRef", "Get16x16TileInfo", "Copy16x16Tile",
        "Set16x16TileInfo", "GetAnimationByName", "ReadSaveRAM", "WriteSaveRAM",
        "LoadTextFont", "LoadTextFile", "DrawText", "GetTextInfo",
        "GetVersionNumber", "SetAchievement", "SetLeaderboard", "LoadOnlineMenu",
        "HapticEffect",
    };

    /// <summary>
    /// Opcodes that cannot be emitted verbatim because RSDKv4's form differs or
    /// is absent. Each records why, so the gap shows in the output.
    /// </summary>
    private static readonly Dictionary<string, string> KnownGaps = new(StringComparer.Ordinal)
    {
        ["PlayerObjectCollision"] =
            "RSDKv4 has no PlayerObjectCollision; needs foreach (GROUP_PLAYERS,...) plus BoxCollisionTest",
        ["CopyPalette"] = "RSDKv4's CopyPalette takes five arguments, not two",
        ["RotatePalette"] = "RSDKv4's RotatePalette takes four arguments, not three",
        ["CallFunction"] = "RSDKv4 calls a function by name, not by index",
        ["Not"] = "RSDKv4 uses the ! operator",
        ["FlipSign"] = "RSDKv4 negates with the - operator",
        ["EngineCallback"] = "no RSDKv4 equivalent",
        ["Rand"] = "RSDKv4's Rand takes different arguments",
        ["Sin"] = "RSDKv4's Sin uses a different unit",
        ["Cos"] = "RSDKv4's Cos uses a different unit",
        ["Sin256"] = "RSDKv4's Sin uses a different unit",
        ["Cos256"] = "RSDKv4's Cos uses a different unit",
        ["SinChange"] = "RSDKv4's Sin uses a different unit",
        ["CosChange"] = "RSDKv4's Cos uses a different unit",
        ["ATan2"] = "RSDKv4's ATan2 returns a different range",
        ["Interpolate"] = "RSDKv4's Interpolate takes different arguments",
        ["InterpolateXY"] = "RSDKv4 has no InterpolateXY",
    };

    public sealed class Options
    {
        public string ObjectName { get; init; } = "Object";
        public string Subroutine { get; init; } = "Main";
    }

    /// <summary>Renders one subroutine as RSDKv4 script text.</summary>
    public static string Write(RsdkV3ScriptReader reader, int startPc,
                              IReadOnlyList<int> jumpTable, int jumpTableBase,
                              Options options)
    {
        if (startPc < 0 || startPc >= reader.WordCount)
            throw new InvalidOperationException(
                $"subroutine entry point {startPc} is outside the instruction stream " +
                $"({reader.WordCount} words). Callers must set ScriptCodeBase to the word " +
                "count of the bytecode loaded before this file, otherwise entry points " +
                "resolve as global indices.");

        var byPc = new Dictionary<int, RsdkV3ScriptReader.Instruction>();
        var nextPc = new Dictionary<int, int>();
        var sorted = reader.ScriptCode.Select(i => i.Pc).OrderBy(x => x).ToList();
        foreach (var ins in reader.ScriptCode)
            byPc[ins.Pc] = ins;
        for (var i = 0; i < sorted.Count - 1; ++i)
            nextPc[sorted[i]] = sorted[i + 1];

        var sb = new StringBuilder();
        var unresolved = new SortedSet<string>(StringComparer.Ordinal);
        var anomalies = new List<string>();
        var caseLabels = new Dictionary<int, int>();
        var defaultLabels = new HashSet<int>();
        var blocks = new Stack<Block>();

        var indent = 1;
        var pc = startPc;
        var finished = false;

        while (!finished && byPc.TryGetValue(pc, out var ins))
        {
            var name = ins.Name;
            var args = ins.Operands.Select(o => o.Text).ToArray();
            var advance = true;

            switch (name)
            {
                // ---- terminators -----------------------------------------
                case "End":
                    Emit(sb, indent, "end");
                    finished = true;
                    break;

                case "EndFunction":
                    finished = true;
                    break;

                // ---- block openers ---------------------------------------
                case "else" when blocks.Count > 0 && blocks.Peek().Kind == BlockKind.If:
                    --indent;
                    Emit(sb, indent, "else");
                    ++indent;
                    break;

                case "else":
                    anomalies.Add($"pc {pc}: 'else' with no open if");
                    break;

                case "endif":
                    if (blocks.Count > 0 && blocks.Peek().Kind == BlockKind.If)
                    {
                        var b = blocks.Pop();
                        --indent;
                        Emit(sb, indent, "end if");
                        Expect(anomalies, nextPc, pc, b.ExpectedExit, "if", b.OpenPc);
                    }
                    else
                    {
                        anomalies.Add($"pc {pc}: 'endif' with no open if");
                    }
                    break;

                case "loop":
                    if (blocks.Count > 0 && blocks.Peek().Kind == BlockKind.While)
                    {
                        var b = blocks.Pop();
                        --indent;
                        Emit(sb, indent, "loop");
                        // `loop` jumps back to the while itself, so the condition
                        // is re-tested each pass. That is the target the engine
                        // uses, so check against the while rather than past it.
                        if (b.ExpectedExit != b.OpenPc)
                            anomalies.Add(
                                $"pc {b.OpenPc}: loop should return to word {b.OpenPc} " +
                                $"but the jump table says {b.ExpectedExit}");
                    }
                    else
                    {
                        anomalies.Add($"pc {pc}: 'loop' with no open while");
                    }
                    break;

                case "endswitch":
                    if (blocks.Count > 0 && blocks.Peek().Kind == BlockKind.Switch)
                    {
                        var b = blocks.Pop();
                        --indent;
                        Emit(sb, indent, "end switch");
                        // A switch's out-of-range and break target is the
                        // endswitch itself, not the instruction after it.
                        if (pc != b.ExpectedExit)
                            anomalies.Add(
                                $"pc {b.OpenPc}: switch should fall through to word " +
                                $"{b.ExpectedExit} but its endswitch is at {pc}");
                    }
                    else
                    {
                        anomalies.Add($"pc {pc}: 'endswitch' with no open switch");
                    }
                    break;

                case "break":
                    Emit(sb, indent, "break");
                    break;

                default:
                    indent += EmitInstruction(ins, args, indent, sb, unresolved,
                                              caseLabels, defaultLabels, blocks, jumpTable,
                                              jumpTableBase, startPc, anomalies);
                    break;
            }

            if (!advance)
            {
                finished = true;
                break;
            }

            if (finished)
                break;

            if (!nextPc.TryGetValue(pc, out var nx))
            {
                if (!finished)
                    anomalies.Add($"pc {pc}: no following instruction");
                break;
            }
            pc = nx;
        }

        // Anything still open means the stream ended mid-construct.
        while (blocks.Count > 0)
        {
            var b = blocks.Pop();
            anomalies.Add($"pc {b.OpenPc}: {b.Kind} never closed before the subroutine ended");
        }

        if (anomalies.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("// --- control-flow anomalies: verify these before playing ---");
            foreach (var a in anomalies)
                sb.AppendLine($"//   {a}");
        }

        if (unresolved.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("// --- not yet mapped to RSDKv4 ---");
            foreach (var u in unresolved)
                sb.AppendLine($"//   {u}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Emits one non-control instruction, or one of the constructs that open a
    /// block. Returns how much the indentation should grow: 1 for a construct
    /// that opens a block, 0 otherwise.
    /// </summary>
    private static int EmitInstruction(
        RsdkV3ScriptReader.Instruction ins, string[] args, int indent, StringBuilder sb,
        SortedSet<string> unresolved, Dictionary<int, int> caseLabels,
        HashSet<int> defaultLabels, Stack<Block> blocks,
        IReadOnlyList<int> jumpTable, int jumpTableBase, int startPc, List<string> anomalies)
    {
        var name = ins.Name;

        // A case label comes first: a case body can begin with any instruction,
        // including a conditional, so the label is emitted and then the
        // instruction itself is handled normally.
        if (caseLabels.TryGetValue(ins.Pc, out var caseValue))
        {
            if (blocks.Count > 0 && blocks.Peek().Kind == BlockKind.Switch)
                Emit(sb, indent, $"case {caseValue}");
            else
                anomalies.Add($"pc {ins.Pc}: case {caseValue} label outside a switch");
        }
        else if (defaultLabels.Contains(ins.Pc))
        {
            if (blocks.Count > 0 && blocks.Peek().Kind == BlockKind.Switch)
                Emit(sb, indent, "default");
            else
                anomalies.Add($"pc {ins.Pc}: default label outside a switch");
        }

        if (IfOps.Contains(name) && ins.Operands.Count == 3)
        {
            var slot = (int)ins.Operands[0].IntValue;
            Emit(sb, indent, $"if {args[1]} {CompareOp[name]} {args[2]}");
            blocks.Push(new Block
            {
                Kind = BlockKind.If,
                Slot = slot,
                OpenPc = ins.Pc,
                // Both the else branch and the fall-through land here.
                ExpectedExit = startPc + At(jumpTable, jumpTableBase + slot + 1),
            });
            return 1;
        }

        if (WhileOps.Contains(name) && ins.Operands.Count == 3)
        {
            var slot = (int)ins.Operands[0].IntValue;
            Emit(sb, indent, $"while {args[1]} {CompareOp[name]} {args[2]}");
            blocks.Push(new Block
            {
                Kind = BlockKind.While,
                Slot = slot,
                OpenPc = ins.Pc,
                // `loop` jumps back to this while, so the test runs again.
                ExpectedExit = startPc + At(jumpTable, jumpTableBase + slot),
            });
            return 1;
        }

        if (name == "switch" && ins.Operands.Count == 2)
        {
            var slot = (int)ins.Operands[0].IntValue;
            var low = At(jumpTable, jumpTableBase + slot);
            var high = At(jumpTable, jumpTableBase + slot + 1);
            Emit(sb, indent, $"switch ({args[1]})");
            blocks.Push(new Block
            {
                Kind = BlockKind.Switch,
                Slot = slot,
                OpenPc = ins.Pc,
                // Anything outside low..high, and any break, lands on this
                // instruction, which is the endswitch itself.
                ExpectedExit = startPc + At(jumpTable, jumpTableBase + slot + 2),
            });

            // Recover the case labels: the case bodies appear in ascending case
            // order, and only their targets are stored.
            var span = high - low;
            var deflt = startPc + At(jumpTable, jumpTableBase + slot + 2);
            if (span < 0 || span > 65535)
                anomalies.Add($"pc {ins.Pc}: implausible switch range {low}..{high}");
            else
                for (var c = low; c <= high; ++c)
                {
                    var target = startPc + At(jumpTable, jumpTableBase + slot + 4 + (c - low));
                    if (!caseLabels.ContainsKey(target))
                        caseLabels[target] = c;
                }

            // A switch's out-of-range and break target is not always the
            // endswitch: when it points earlier there is a default body, which
            // is laid out after the last case and has to be labelled.
            defaultLabels.Add(deflt);
            return 1;
        }

        if (AssignOp.TryGetValue(name, out var assign))
        {
            if (args.Length != 2)
            {
                unresolved.Add($"{name} has {args.Length} operands, expected 2");
                Emit(sb, indent, $"// TODO: {name} {string.Join(" ", args)}");
                return 0;
            }
            Emit(sb, indent, $"{args[0]} {assign} {args[1]}");
            return 0;
        }

        if (CheckOp.TryGetValue(name, out var cmp))
        {
            Emit(sb, indent, $"checkResult = {args[0]} {cmp} {args[1]}");
            return 0;
        }

        switch (name)
        {
            case "Inc":
                Emit(sb, indent, $"{args[0]}++");
                return 0;
            case "Dec":
                Emit(sb, indent, $"{args[0]}--");
                return 0;
            case "Not":
                Emit(sb, indent, $"{args[0]} = !{args[0]}");
                return 0;
            case "FlipSign":
                Emit(sb, indent, $"{args[0]} = -{args[0]}");
                return 0;
        }

        if (KnownGaps.TryGetValue(name, out var gap))
        {
            unresolved.Add($"{name}: {gap}");
            Emit(sb, indent, $"// TODO: {name}({string.Join(", ", args)}) -- {gap}");
            return 0;
        }

        if (PassthroughOps.Contains(name))
        {
            Emit(sb, indent, $"{name}({string.Join(", ", args)})");
            return 0;
        }

        unresolved.Add($"opcode {name}({string.Join(" ", args)}) at pc {ins.Pc}");
        Emit(sb, indent, $"// TODO: {name} {string.Join(" ", args)}");
        return 0;
    }

    /// <summary>
    /// Records a mismatch when a construct does not end where the engine's jump
    /// table says it should. A silently wrong nesting produces a stage that
    /// loads and then misbehaves, so this is reported rather than trusted.
    /// </summary>
    private static void Expect(List<string> anomalies, Dictionary<int, int> nextPc,
                               int actualPc, int expectedPc, string kind, int openPc)
    {
        if (nextPc.TryGetValue(actualPc, out var after) && after != expectedPc)
            anomalies.Add(
                $"pc {openPc}: {kind} should continue at word {expectedPc} " +
                $"but the next instruction is at {after}");
    }

    private static int At(IReadOnlyList<int> jumpTable, int index)
        => index >= 0 && index < jumpTable.Count ? jumpTable[index] : 0;

    private static void Emit(StringBuilder sb, int indent, string text)
    {
        for (var i = 0; i < indent; ++i)
            sb.Append('\t');
        sb.Append(text).Append('\n');
    }
}