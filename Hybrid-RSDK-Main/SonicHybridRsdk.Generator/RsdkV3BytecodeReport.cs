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
}