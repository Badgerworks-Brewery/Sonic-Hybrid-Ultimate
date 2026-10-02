using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

try
{
    // Paths are relative to Hybrid-RSDK-Main/ (where dotnet run is executed)
    const string SourceData = "rsdk-source-data/";
    const string DestinationData = "sonic-hybrid/";
    const string SonicCdRsdk = SourceData + "soniccd.rsdk";
    const string Sonic1Rsdk = SourceData + "sonic1.rsdk";
    const string Sonic2Rsdk = SourceData + "sonic2.rsdk";

    Console.WriteLine("Sonic Hybrid Ultimate - Build Tool");
    Console.WriteLine("===================================");
    Console.WriteLine();
    Console.WriteLine($"Working directory: {Directory.GetCurrentDirectory()}");
    Console.WriteLine($"Source data: {Path.GetFullPath(SourceData)}");
    Console.WriteLine($"Destination: {Path.GetFullPath(DestinationData)}");
    Console.WriteLine();

    if (!File.Exists(SonicCdRsdk))
        throw new FileNotFoundException($"Missing required file", SonicCdRsdk);
    if (!File.Exists(Sonic1Rsdk))
        throw new FileNotFoundException($"Missing required file", Sonic1Rsdk);
    if (!File.Exists(Sonic2Rsdk))
        throw new FileNotFoundException($"Missing required file", Sonic2Rsdk);

    Console.WriteLine("✓ All source files found");
    Console.WriteLine();

    Console.WriteLine("Unpacking Sonic CD...");
    SonicHybridRsdk.UnpackScd.Program.Unpack(SonicCdRsdk, SourceData + "soniccd");
    Console.WriteLine("✓ Sonic CD unpacked");
    Console.WriteLine();

    Console.WriteLine("Unpacking Sonic the Hedgehog 1...");
    SonicHybridRsdk.UnpackS12.Program.Unpack(Sonic1Rsdk, SourceData + "sonic1");
    Console.WriteLine("✓ Sonic 1 unpacked");
    Console.WriteLine();

    Console.WriteLine("Unpacking Sonic the Hedgehog 2...");
    SonicHybridRsdk.UnpackS12.Program.Unpack(Sonic2Rsdk, SourceData + "sonic2");
    Console.WriteLine("✓ Sonic 2 unpacked");
    Console.WriteLine();

    // Sonic CD ships no text scripts - its gameplay is RSDKv3 VM bytecode.
    // Verify we can read all of it before generating, so a broken reader fails
    // loudly here instead of silently shipping a pack with no CD gameplay.
    var byteCodeDir = SourceData + "soniccd/Data/Scripts/ByteCode";
    Console.WriteLine("Reading Sonic CD bytecode...");
    var report = SonicHybridRsdk.Generator.RsdkV3BytecodeReport.Run(byteCodeDir);
    if (report.Files > 0)
    {
        Console.WriteLine($"  {report.Files} files, {report.Instructions} instructions, " +
                          $"{report.Scripts} scripts, {report.Functions} functions");
    }
    if (report.Failures.Count > 0)
        throw new InvalidOperationException(
            "RSDKv3 bytecode reader failed:\n  " + string.Join("\n  ", report.Failures.Take(10)));
    if (report.Files == 0)
        Console.WriteLine("  (no bytecode found - Sonic CD stages will load without gameplay)");
    else
    {
        // Now check the decompiler, not just the reader. A reader that parses but
        // a writer that cannot emit, or an opcode nobody has mapped, would both
        // produce scripts that are quietly wrong - the exact failure this build
        // exists to prevent.
        var decomp = SonicHybridRsdk.Generator.RsdkV3BytecodeReport.RunDecompiler(byteCodeDir);
        Console.WriteLine($"  decompiled {decomp.Emitted}/{decomp.Subroutines} subroutines, " +
                          $"{decomp.WriteFailures} write failures, " +
                          $"{decomp.UnknownOpcodes} unmapped opcodes, " +
                          $"{decomp.AnomalousSubroutines} with control-flow anomalies");

        if (decomp.WriteFailures > 0 || decomp.UnknownOpcodes > 0)
            throw new InvalidOperationException(
                "RSDKv3 script writer failed:\n  " + string.Join("\n  ", decomp.Notes.Take(10)));

        // Reported, not fatal: a few shipped routines have unbalanced block
        // markers, and guessing at them would be worse than flagging them.
        if (decomp.AnomalousSubroutines > 0)
            Console.WriteLine($"  note: {decomp.AnomalousSubroutines} subroutines have " +
                              "control-flow anomalies:\n    "
                              + string.Join("\n    ", decomp.AnomalySamples.Take(5)));
    }

    Console.WriteLine("Generating Sonic Hybrid Ultimate...");
    SonicHybridRsdk.Generator.Program.Generate(SourceData, DestinationData);
    Console.WriteLine("✓ Hybrid data generated");
    Console.WriteLine();

    // The engine loads assets from a "Data.rsdk" pack. The generator emits a loose
    // "Data/" tree, and the script text lives in a separate tracked "Scripts/" folder,
    // so both have to be packed into the archive before the game can run.
    //
    // Mount points matter: the engine asks for "Data/Game/GameConfig.bin" and
    // "Data/Scripts/GHZ/GHZSetup.txt", then lowercases before hashing. Both sources
    // therefore have to land under the "data/" prefix or every lookup silently misses.
    //
    // Bytecode is the exception: RSDKv4 asks for "Bytecode/GlobalCode.bin" and
    // "Bytecode/<stage folder>.bin" (Script.cpp:3075), with no Data/ prefix. It
    // therefore needs its own "bytecode/" mount, or the engine logs
    // "Couldn't load file" and silently falls back to the text scripts - which
    // have no entry points, so objects spawn with no behaviour at all.
    // Merge the two games' global bytecode and lay down both games' per-stage
    // files. This has to happen after the C# generator has written Data/ (it
    // creates Data/Bytecode) and before packing.
    // Walk up from the output folder (bin/<config>/<tfm>) to the repo root, which
    // is the first directory containing scripts/. Hardcoding a depth broke
    // whenever the target framework folder changed.
    var repoRoot = (string?)null;
    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        if (Directory.Exists(Path.Combine(dir.FullName, "scripts")))
        {
            repoRoot = dir.FullName;
            break;
        }
    repoRoot ??= Directory.GetCurrentDirectory();
    var mergeScript = Path.Combine(repoRoot, "scripts", "emit_merged_bytecode.py");
    if (File.Exists(mergeScript))
    {
        Console.WriteLine("Merging Sonic 1 + Sonic 2 bytecode...");
        var psi = new ProcessStartInfo("python", $"\"{mergeScript}\"")
        {
            WorkingDirectory = repoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var merge = Process.Start(psi)!;
        var mergeOut = merge.StandardOutput.ReadToEnd();
        merge.WaitForExit();
        foreach (var line in mergeOut.Split('\n'))
            if (line.Trim().Length > 0)
                Console.WriteLine("  " + line.TrimEnd());
        if (merge.ExitCode != 0)
            throw new InvalidOperationException(
                "Merging the global bytecode failed:\n" + merge.StandardError.ReadToEnd());
    }
    else
    {
        Console.WriteLine("  WARNING: emit_merged_bytecode.py not found; " +
                          "no global bytecode will be shipped");
    }

    Console.WriteLine("Packing data archive...");
    SonicHybridRsdk.Generator.RsdkPacker.Pack(
        DestinationData + "Data.rsdk",
        (DestinationData + "Data", "data"),
        (DestinationData + "Data/Bytecode", "bytecode"),
        (DestinationData + "Scripts", "data/scripts"));
    Console.WriteLine("✓ Data archive packed");
    Console.WriteLine();

    if (!File.Exists(DestinationData + "Data.rsdk"))
        throw new InvalidOperationException("Packing reported success but Data.rsdk was not created.");

    // Fail loudly if the pack is missing anything the engine asks for by name.
    SonicHybridRsdk.Generator.RsdkPacker.Verify(
        DestinationData + "Data.rsdk",
        // Bytecode is what actually runs the objects. RSDKv4 takes the bytecode
        // path only if GlobalCode.bin resolves (Scene.cpp:675), and then looks up
        // Bytecode/<stage folder>.bin (Script.cpp:3075). If any of these are
        // missing the affected stage loads with no object logic and says nothing.
        "data/bytecode/globalcode.bin",
        "data/bytecode/zoneehz.bin",
        "data/bytecode/zonecpz.bin",
        "data/bytecode/zonearz.bin",
        "data/bytecode/zonescz.bin",
        "data/bytecode/zonedez.bin",
        "data/game/gameconfig.bin",
        "data/scripts/global/stagesetup.txt",
        "data/scripts/ghz/ghzsetup.txt",
        "data/scripts/ehz/ehzsetup.txt",
        "data/stages/zoneghz/backgrounds.bin",
        "data/stages/zoneehz/backgrounds.bin");
    Console.WriteLine("✓ Data archive verified against engine lookup paths");

    Console.WriteLine("===================================");
    Console.WriteLine("SUCCESS: Hybrid data created at:");
    Console.WriteLine($"  {Path.GetFullPath(DestinationData + "Data.rsdk")}");
    Console.WriteLine();
}
catch (FileNotFoundException ex)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine($"ERROR: Unable to find '{ex.FileName}'");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Please ensure you have placed the following files in rsdk-source-data/:");
    Console.Error.WriteLine("  - soniccd.rsdk (from Sonic CD)");
    Console.Error.WriteLine("  - sonic1.rsdk (from Sonic 1)");
    Console.Error.WriteLine("  - sonic2.rsdk (from Sonic 2)");
    Console.Error.WriteLine();
    Console.Error.WriteLine("See rsdk-source-data/README.md for instructions.");
    Environment.ExitCode = -1;
}
catch (Exception ex)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine($"ERROR: An error occurred during hybrid data generation:");
    Console.Error.WriteLine($"  {ex.Message}");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Stack trace:");
    Console.Error.WriteLine(ex.StackTrace);
    Environment.ExitCode = -2;
}
