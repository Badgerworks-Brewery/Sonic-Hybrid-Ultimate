using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace SonicHybridUltimate.Tools
{
    public class RSDKAnalyzer
    {
        private readonly string rsdkPath;
        private readonly ILogger<RSDKAnalyzer> logger;

        public RSDKAnalyzer(ILogger<RSDKAnalyzer> logger)
        {
            this.logger = logger;
            this.rsdkPath = string.Empty;
        }

        public RSDKAnalyzer(string rsdkPath, ILogger<RSDKAnalyzer> logger)
        {
            this.rsdkPath = rsdkPath;
            this.logger = logger;
        }

        public void AnalyzeFile()
        {
            try
            {
                using (var stream = new FileStream(rsdkPath, FileMode.Open, FileAccess.Read))
                using (var reader = new BinaryReader(stream))
                {
                    // Read RSDK header
                    var signature = new string(reader.ReadChars(4));
                    logger.LogInformation("RSDK Signature: {Signature}", signature);

                    // Read version info
                    var version = reader.ReadUInt32();
                    logger.LogInformation("RSDK Version: {Version}", version);

                    // Read file count
                    var fileCount = reader.ReadUInt32();
                    logger.LogInformation("Number of files: {FileCount}", fileCount);

                    // Read file table
                    var fileTable = new List<RSDKFileEntry>();
                    for (uint i = 0; i < fileCount; i++)
                    {
                        var entry = new RSDKFileEntry
                        {
                            Offset = reader.ReadUInt32(),
                            Size = reader.ReadUInt32(),
                            Name = ReadNullTerminatedString(reader)
                        };
                        fileTable.Add(entry);
                        logger.LogInformation("File {Index}: {Name} (Size: {Size} bytes, Offset: {Offset})", i, entry.Name, entry.Size, entry.Offset);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error analyzing RSDK file");
            }
        }

        private string ReadNullTerminatedString(BinaryReader reader)
        {
            var bytes = new List<byte>();
            byte b;
            while ((b = reader.ReadByte()) != 0)
            {
                bytes.Add(b);
            }
            return Encoding.ASCII.GetString(bytes.ToArray());
        }
    }

    public class RSDKFileEntry
    {
        public uint Offset { get; set; }
        public uint Size { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
