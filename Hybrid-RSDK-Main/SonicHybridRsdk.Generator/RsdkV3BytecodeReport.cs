using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SonicHybridRsdk.Generator;

/// <summary>
/// Sanity-checks the RSDKv3 bytecode reader against the real Sonic CD bytecode.
/// </summary>
/// <remarks>
/// This is deliberately a hard check rather than a warning: if the reader cannot
/// parse every shipped script, the decompiler cannot be trusted, and silently
/// shipping a Data.rsdk with missing Sonic CD scripts would reintroduce the
/// "levels only load the background" symptom.
/// </remarks>
public static class RsdkV3BytecodeReport
{
    public sealed record Result(int Files, int Instructions, int Scripts, int Functions, List<string> Failures);

    /// <summary>
    /// Outcome of running the script writer over every subroutine in every file.
    /// </summary>
    public sealed record DecompilerResult(
        int Subroutines, int Emitted, int WriteFailures, int UnknownOpcodes,
        int AnomalousSubroutines, List<string> Notes, List<string> AnomalySamples);

    public static Result Run(string byteCodeDirectory, bool verbose = false)
    {
        if (!Directory.Exists(byteCodeDirectory))
            return new Result(0, 0, 0, 0, new List<string> { $"{byteCodeDirectory}: not found" });

        var failures = new List<string>();
        var files = Directory.GetFiles(byteCodeDirectory, "*.bin").OrderBy(Path.GetFileName).ToList();

        int instructions = 0, scripts = 0, functions = 0;

        foreach (var file in files)
        {
            try
            {
                var reader = RsdkV3ScriptReader.Load(file);
                instructions += reader.ScriptCode.Count;
                scripts += reader.Scripts.Count;
                functions += reader.Functions.Count;

                if (reader.ScriptCode.Count == 0)
                    failures.Add($"{Path.GetFileName(file)}: decoded zero instructions");

                // The container layout is only correct if the parse lands on the
                // last byte. A wrong per-script pointer count still "parses", but
                // leaves bytes behind - which is exactly how the 5-pointer bug
                // slipped through and corrupted the tail of nine scripts.
                var length = new FileInfo(file).Length;
                if (reader.BytesConsumed != length)
                    failures.Add($"{Path.GetFileName(file)}: consumed {reader.BytesConsumed} " +
                                 $"of {length} bytes ({length - reader.BytesConsumed} left over)");

                if (verbose)
                    Console.WriteLine($"  {Path.GetFileName(file),-12} " +
                                      $"insns={reader.ScriptCode.Count,6}  " +
                                      $"scripts={reader.Scripts.Count,4}  " +
                                      $"funcs={reader.Functions.Count,5}");
            }
            catch (Exception e)
            {
                failures.Add($"{Path.GetFileName(file)}: {e.Message}");
            }
        }

        return new Result(files.Count, instructions, scripts, functions, failures);
    }

    /// <summary>
    /// Runs the script writer over every subroutine in every bytecode file.
    /// </summary>
    /// <remarks>
    /// Two things are treated as hard failures, because either means the emitted
    /// script would be wrong rather than merely incomplete:
    /// a subroutine the writer cannot emit at all, and an opcode the mapping does
    /// not know (which would otherwise be emitted as a guess). Control-flow
    /// anomalies are counted and sampled instead: the shipped bytecode contains a
    /// handful of routines whose markers do not balance, and those are reported so
    /// a human can look at them rather than silently mis-nesting them.
    /// </remarks>
    public static DecompilerResult RunDecompiler(string byteCodeDirectory, bool verbose = false)
    {
        var notes = new List<string>();
        var samples = new List<string>();

        if (!Directory.Exists(byteCodeDirectory))
            return new DecompilerResult(0, 0, 0, 0, 0,
                new List<string> { $"{byteCodeDirectory}: not found" }, samples);

        var files = Directory.GetFiles(byteCodeDirectory, "*.bin").OrderBy(Path.GetFileName).ToList();

        // GS000 is the global object code and is always loaded first, so its word
        // counts are the base offsets every later file's entry points are relative
        // to. Getting this wrong is what made every pointer look out of range.
        var globalPath = Path.Combine(byteCodeDirectory, "GS000.bin");
        if (!File.Exists(globalPath))
            return new DecompilerResult(0, 0, 0, 0, 0,
                new List<string> { $"{globalPath}: not found" }, samples);

        var global = RsdkV3ScriptReader.Load(globalPath);

        string[] subNames = { "Main", "PlayerInteraction", "Draw", "Startup" };
        int subroutines = 0, emitted = 0, writeFailures = 0, anomalous = 0;
        var unknownOpcodes = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var file in files)
        {
            RsdkV3ScriptReader reader;
            try
            {
                reader = RsdkV3ScriptReader.Load(file);
                reader.ScriptCodeBase = global.WordCount;
                reader.JumpTableBase = global.JumpWordCount;
            }
            catch (Exception e)
            {
                notes.Add($"{Path.GetFileName(file)}: load failed: {e.Message}");
                continue;
            }

            var name = Path.GetFileName(file);
            for (int i = 0; i < reader.Scripts.Count; ++i)
            {
                var resolved = reader.Resolve(reader.Scripts[i], i);
                for (int k = 0; k < 4; ++k)
                {
                    if (resolved.ScriptCodePtrs[k] < 0)
                        continue;

                    subroutines++;
                    string text;
                    try
                    {
                        text = RsdkV3ScriptWriter.Write(reader, resolved.ScriptCodePtrs[k],
                            reader.JumpTable, resolved.JumpTablePtrs[k],
                            new RsdkV3ScriptWriter.Options());
                    }
                    catch (Exception e)
                    {
                        writeFailures++;
                        notes.Add($"{name} {resolved.Name}.{subNames[k]}: {e.Message}");
                        continue;
                    }

                    emitted++;

                    bool inAnomalies = false;
                    foreach (var line in text.Split('\n'))
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("// --- control-flow anomalies",
                                StringComparison.Ordinal))
                        {
                            inAnomalies = true;
                            continue;
                        }
                        if (trimmed.StartsWith("// --- not yet mapped",
                                StringComparison.Ordinal))
                        {
                            inAnomalies = false;
                            continue;
                        }
                        if (!trimmed.StartsWith("//   ", StringComparison.Ordinal))
                            continue;

                        var body = trimmed.Substring(5);
                        int at = body.IndexOf("opcode ", StringComparison.Ordinal);
                        if (at >= 0)
                        {
                            // "opcode Name(args) at pc N": only the name matters.
                            var op = body.Substring(at + 7);
                            int paren = op.IndexOf('(');
                            unknownOpcodes.Add(paren > 0 ? op.Substring(0, paren) : op);
                        }
                        else if (inAnomalies)
                        {
                            anomalous++;
                            if (samples.Count < 20)
                                samples.Add($"{name} {resolved.Name}.{subNames[k]}: {body}");
                            break;
                        }
                    }
                }
            }
        }

        if (writeFailures > 0)
            notes.Add($"{writeFailures} subroutine(s) could not be emitted at all");
        if (unknownOpcodes.Count > 0)
            notes.Add($"unmapped opcodes: {string.Join(", ", unknownOpcodes)}");

        if (verbose)
        {
            Console.WriteLine($"  subroutines={subroutines} emitted={emitted} " +
                              $"writeFailures={writeFailures} " +
                              $"unmappedOpcodes={unknownOpcodes.Count} " +
                              $"anomalous={anomalous}");
        }

        return new DecompilerResult(subroutines, emitted, writeFailures,
            unknownOpcodes.Count, anomalous, notes, samples);
    }
}