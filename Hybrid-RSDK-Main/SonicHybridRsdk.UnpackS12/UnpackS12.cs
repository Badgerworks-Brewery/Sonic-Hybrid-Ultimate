using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SonicHybridRsdk.UnpackS12
{
	public class Program
	{
        static void Main(string[] args) => Unpack(
			args[0],
			args.Length >= 2 ? args[1] : Path.GetDirectoryName(args[0]));

        public static void Unpack(string inputFileName, string outputDirectory)
		{
			const int HeaderMagicCode = 0x4B445352;
			const short HeaderVersion = 0x4276;

			using var stream = File.OpenRead(inputFileName);
			using var reader = new BinaryReader(stream);

			if (reader.ReadInt32() != HeaderMagicCode ||
				reader.ReadInt16() != HeaderVersion)
				return;

			var names = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "filelist"))
				.Select(x => new
				{
					Hash = MD5.HashData(Encoding.UTF8.GetBytes(x.ToLower())),
					Name = x
				})
				.GroupBy(x => ToString(x.Hash))
				.Select(x => x.First())
				.ToDictionary(x => ToString(x.Hash), x => x.Name);

			var entries = Enumerable.Range(0, reader.ReadInt16())
				.Select(_ => new
				{
					Hash = Swap(reader.ReadBytes(0x10)),
					Offset = reader.ReadInt32(),
					Length = reader.ReadInt32(),
				})
				.Select(x => new
				{
					Name = GetNameFromHash(names, x.Hash),
					x.Offset,
					Length = x.Length & 0x7FFFFFFF,
					IsEncrypted = x.Length < 0
				})
				.ToList();

			foreach (var entry in entries)
			{
				var fileData = reader.ReadBytes(entry.Length);
				if (entry.IsEncrypted)
					DecryptData(fileData);

                Directory.CreateDirectory(Path.Combine(outputDirectory, Path.GetDirectoryName(entry.Name)));
				File.WriteAllBytes(
					Path.Combine(outputDirectory, entry.Name),
					fileData);
			}
		}

		static void GenerateKey(byte[] Buffer, int Value)
		{
			var data = MD5.HashData(Encoding.UTF8.GetBytes(Value.ToString()));
			for (int y = 0; y < 16; y += 4)
			{
				Buffer[y + 3] = data[y + 0];
				Buffer[y + 2] = data[y + 1];
				Buffer[y + 1] = data[y + 2];
				Buffer[y + 0] = data[y + 3];
			}
		}

		// Shamelessly copied from Retrun's source code
		// Original author: Giuseppe Gatta (nextvolume)
		static void DecryptData(byte[] data)
		{
			static uint MulUnsignedHigh(uint arg1, uint arg2) =>
				(uint)(((ulong)arg1 * arg2) >> 32);

            var encryptionStringA = new byte[0x10];
			var encryptionStringB = new byte[0x10];
			var eStringNo = (uint)((data.Length / 4) & 0x7F);
			var eStringPosA = 0u;
			var eStringPosB = 8u;
			var eNybbleSwap = 0u;

            GenerateKey(encryptionStringA, data.Length);
            GenerateKey(encryptionStringB, (data.Length >> 1) + 1);

			const uint ENC_KEY_2 = 0x24924925;
			const uint ENC_KEY_1 = 0xAAAAAAAB;

			uint tempByte;
			uint key1;
			uint key2;
			uint temp1;
			uint temp2;

			for (int i = 0, length = data.Length; length != 0; length--, i++)
			{
				tempByte = eStringNo ^ encryptionStringB[eStringPosB];
				tempByte ^= data[i];
				if (eNybbleSwap == 1)   // swap nibbles: 0xAB <-> 0xBA
					tempByte = ((tempByte << 4) + (tempByte >> 4)) & 0xFF;
				tempByte ^= encryptionStringA[eStringPosA];
				data[i] = (byte)tempByte;

				eStringPosA++;
				eStringPosB++;

				if (eStringPosA <= 0x0F)
				{
					if (eStringPosB > 0x0C)
					{
						eStringPosB = 0;
						eNybbleSwap ^= 0x01;
					}
				}
				else if (eStringPosB <= 0x08)
				{
					eStringPosA = 0;
					eNybbleSwap ^= 0x01;
				}
				else
				{
					eStringNo += 2;
					eStringNo &= 0x7F;

					if (eNybbleSwap != 0)
					{
						key1 = MulUnsignedHigh(ENC_KEY_1, eStringNo);
						key2 = MulUnsignedHigh(ENC_KEY_2, eStringNo);
						eNybbleSwap = 0;

						temp1 = key2 + (eStringNo - key2) / 2;
						temp2 = key1 / 8 * 3;

						eStringPosA = eStringNo - temp1 / 4 * 7;
						eStringPosB = eStringNo - temp2 * 4 + 2;
					}
					else
					{
						key1 = MulUnsignedHigh(ENC_KEY_1, eStringNo);
						key2 = MulUnsignedHigh(ENC_KEY_2, eStringNo);
						eNybbleSwap = 1;

						temp1 = key2 + (eStringNo - key2) / 2;
						temp2 = key1 / 8 * 3;

						eStringPosB = eStringNo - temp1 / 4 * 7;
						eStringPosA = eStringNo - temp2 * 4 + 3;
					}
				}
			}
		}

		static string GetNameFromHash(Dictionary<string, string> dictionary, byte[] hash)
		{
			var key = ToString(hash);
			return dictionary.TryGetValue(key, out var name) ? name : key;
		}

		static byte[] Swap(byte[] data)
		{
            for (var i = 0; i < data.Length - 3; i += 4)
            {
                var ch1 = data[i + 0];
                var ch2 = data[i + 1];
                data[i + 0] = data[i + 3];
                data[i + 1] = data[i + 2];
                data[i + 2] = ch2;
                data[i + 3] = ch1;
            }

            return data;
		}

		static string ToString(byte[] data) =>
			string.Join(string.Empty, data.Select(x => x.ToString("X02")));
	}
}