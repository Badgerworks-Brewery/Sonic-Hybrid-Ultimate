using System;
using System.IO;

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

    Console.WriteLine("Generating Sonic Hybrid Ultimate...");
    SonicHybridRsdk.Generator.Program.Generate(SourceData, DestinationData);
    Console.WriteLine("✓ Hybrid data generated");
    Console.WriteLine();

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
