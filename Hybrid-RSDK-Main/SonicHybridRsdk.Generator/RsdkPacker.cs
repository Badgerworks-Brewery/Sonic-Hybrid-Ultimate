using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SonicHybridRsdk.Generator;

/// <summary>
/// Packs a directory tree into an RSDKv4 data pack ("RSDKvB" archive).
///
/// This is the inverse of <c>CheckRSDKFile()</c> in the RSDKv4 engine
/// (RSDKv4/Reader.cpp). Archive layout, exactly as the engine parses it:
///
/// <code>
///   char   signature[6]  = "RSDKvB"
///   ushort fileCount                 (little endian)
///   per file:
///     byte  hash[16]                  (raw MD5 of the lowercased archive path)
///     uint32 offset                   (little endian, absolute from file start)
///     uint32 filesize                 (little endian; bit 31 = encrypted)
///   byte[] file data, concatenated
/// </code>
///
/// The engine lowercases the requested path before hashing
/// (RSDKv4/Reader.cpp: <c>StringLowerCase(fileInfo-&gt;fileName, filePath)</c>),
/// so every path stored here must be lowercased or the lookup will miss.
/// </remarks>
public static class RsdkPacker
{
    private static readonly byte[] Signature = { (byte)'R', (byte)'S', (byte)'D', (byte)'K', (byte)'v', (byte)'B' };

    /// <summary>
    /// The engine stores the file count in a 16-bit field.
    /// </summary>
    public const int MaxFiles = ushort.MaxValue;

    private sealed class Entry
    {
        public string ArchivePath;
        public string FullPath;
        public long Length;
        public long Offset;
    }

    /// <summary>
    /// Builds <paramref name="outputPath"/> from a set of source directories.
    /// Each source directory is mounted at a prefix inside the archive, e.g.
    /// mounting "sonic-hybrid/Scripts/" at "scripts" makes GHZ/GHZSetup.txt
    /// available to the engine as "data/scripts/ghz/ghzsetup.txt".
    /// </summary>
    public static void Pack(string outputPath, params (string Directory, string ArchivePrefix)[] sources)
    {
        var entries = new List<Entry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (directory, prefix) in sources)
        {
            if (!Directory.Exists(directory))
                continue;

            var prefixTrimmed = prefix.Trim('/');
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                // Normalise to forward slashes and lowercase: the engine hashes the
                // lowercased path, and NTFS is case-insensitive but the archive is not.
                var relative = file[(directory.Length + 1)..].Replace('\\', '/');
                var archivePath = (prefixTrimmed.Length == 0
                    ? relative
                    : prefixTrimmed + "/" + relative).ToLowerInvariant();

                if (!seen.Add(archivePath))
                    continue; // first source wins, matching layered mod semantics

                var info = new FileInfo(file);
                entries.Add(new Entry
                {
                    ArchivePath = archivePath,
                    FullPath = file,
                    Length = info.Length
                });
            }
        }

        // Deterministic ordering keeps builds reproducible (and diffs meaningful).
        entries.Sort((a, b) => string.CompareOrdinal(a.ArchivePath, b.ArchivePath));

        if (entries.Count > MaxFiles)
            throw new InvalidOperationException(
                $"RSDKv4 archives support at most {MaxFiles} files, but {entries.Count} were collected. " +
                "The 16-bit file count field would silently wrap.");

        // Header: signature + count + one 24-byte record per file.
        long dataStart = Signature.Length + 2 + (long)entries.Count * 24;
        long cursor = dataStart;
        foreach (var entry in entries)
        {
            entry.Offset = cursor;
            cursor += entry.Length;
        }

        var parent = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(parent))
            Directory.CreateDirectory(parent);

        using var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new BinaryWriter(output);

        writer.Write(Signature);
        writer.Write((ushort)entries.Count);

        foreach (var entry in entries)
        {
            writer.Write(HashPath(entry.ArchivePath));
            writer.Write((uint)entry.Offset);
            writer.Write((uint)entry.Length);
        }

        var buffer = new byte[1 << 16];
        foreach (var entry in entries)
        {
            using var source = new FileStream(entry.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            int read;
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                writer.Write(buffer, 0, read);
        }

        writer.Flush();

        Console.WriteLine($"  packed {entries.Count} files -> {Path.GetFullPath(outputPath)} " +
                          $"({new FileInfo(outputPath).Length / (1024 * 1024)} MiB)");
    }

    /// <summary>
    /// Re-reads a freshly written pack and confirms the given archive paths resolve
    /// through the same MD5 lookup the engine uses. Guards against mount-point
    /// mistakes, which otherwise produce an archive that parses but never matches.
    /// </summary>
    public static void Verify(string archivePath, params string[] requiredPaths)
    {
        using var stream = File.OpenRead(archivePath);
        using var reader = new BinaryReader(stream);

        var signature = reader.ReadBytes(Signature.Length);
        if (!signature.SequenceEqual(Signature))
            throw new InvalidOperationException($"{archivePath}: bad archive signature.");

        var count = reader.ReadUInt16();
        // NB: byte[] would use reference equality inside a HashSet, so key on hex.
        var index = new HashSet<string>(StringComparer.Ordinal);
        for (var f = 0; f < count; ++f)
        {
            index.Add(Convert.ToHexString(reader.ReadBytes(16))); // hash
            reader.ReadUInt32();                                  // offset
            reader.ReadUInt32();                                  // size
        }

        if (stream.Position != Signature.Length + 2 + (long)count * 24)
            throw new InvalidOperationException($"{archivePath}: malformed header.");

        var missing = requiredPaths
            .Where(p => !index.Contains(Convert.ToHexString(HashPath(p.ToLowerInvariant()))))
            .ToList();

        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"{archivePath}: {missing.Count} required path(s) would not be found by the engine, " +
                $"e.g. {string.Join(", ", missing.Take(5))}. Check the mount prefixes.");
    }

    private static byte[] HashPath(string archivePath)
    {
        // RSDKv4 stores the path MD5 with each 32-bit word byte-swapped relative to
        // the standard digest: the engine reads four bytes at a time and rebuilds the
        // word as (b0 << 24) | (b1 << 16) | (b2 << 8) | b3 (Reader.cpp), so the
        // archive has to hold the little-endian ordering of each word. Verified
        // against the official sonic1.rsdk. Writing raw digest bytes produces an
        // archive that parses but never matches, and every file silently falls
        // back to disk.
        var digest = MD5.HashData(Encoding.UTF8.GetBytes(archivePath));
        var stored = new byte[16];
        for (var w = 0; w < 4; ++w)
            for (var b = 0; b < 4; ++b)
                stored[w * 4 + b] = digest[w * 4 + (3 - b)];
        return stored;
    }
}
