using System;
using System.IO;

try
{
    const string SourceData = "../../../../rsdk-source-data/";
    const string DestinationData = "../../../../sonic-hybrid/";
    const string SonicCdRsdk = SourceData + "soniccd.rsdk";
    const string Sonic1Rsdk = SourceData + "sonic1.rsdk";
    const string Sonic2Rsdk = SourceData + "sonic2.rsdk";

    if (!File.Exists(SonicCdRsdk))
        throw new FileNotFoundException(null, SonicCdRsdk);
    if (!File.Exists(Sonic1Rsdk))
        throw new FileNotFoundException(null, Sonic1Rsdk);
    if (!File.Exists(Sonic2Rsdk))
        throw new FileNotFoundException(null, Sonic2Rsdk);

    Console.WriteLine("Unpacking Sonic CD...");
    SonicHybridRsdk.UnpackScd.Program.Unpack(SonicCdRsdk, SourceData + "soniccd");

    Console.WriteLine("Unpacking Sonic the Hedgehog 1...");
    SonicHybridRsdk.UnpackS12.Program.Unpack(Sonic1Rsdk, SourceData + "sonic1");

    Console.WriteLine("Unpacking Sonic the Hedgehog 2...");
    SonicHybridRsdk.UnpackS12.Program.Unpack(Sonic2Rsdk, SourceData + "sonic2");

    Console.WriteLine("Generating Sonic Hybrid Ultimate...");
    SonicHybridRsdk.Generator.Program.Generate(SourceData, DestinationData);
}
catch (FileNotFoundException ex)
{
    Console.Error.WriteLine($"Unable to find '{ex.FileName}'");
    Environment.ExitCode = -1;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"An error occurred: {ex.Message}");
    Environment.ExitCode = -2;
}
