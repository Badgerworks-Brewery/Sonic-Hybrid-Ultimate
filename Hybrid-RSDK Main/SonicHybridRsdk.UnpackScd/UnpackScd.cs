using System.IO;
using System.Linq;
using System.Text;

namespace SonicHybridRsdk.UnpackScd
{
	public class Program
	{
		static void Main(string[] args) => Unpack(
			args[0],
			args.Length >= 2 ? args[1] : Path.GetDirectoryName(args[0]));

		public static void Unpack(string inputFileName, string outputDirectory)
		{
			using var stream = File.OpenRead(inputFileName);
			using var reader = new BinaryReader(stream);

			var filesystemOffset = reader.ReadInt32();
			var directories = Enumerable.Range(0, reader.ReadInt16())
				.Select(_ => new
				{
					Name = ReadDeobfuscatedString(reader),
					Offset = reader.ReadInt32(),
				})
				.ToList();

			for (int i = 0; i < directories.Count; i++)
			{
				var directory = directories[i];
				var endPosition = i < directories.Count - 1 ?
					directories[i + 1].Offset + filesystemOffset :
					stream.Position;

				stream.Position = filesystemOffset + directory.Offset;
				while (stream.Position < endPosition)
				{
					var fileName = ReadDeobfuscatedStringAlt(reader);
					var fileSize = reader.ReadInt32();
					var fileData = reader.ReadBytes(fileSize);
					DecryptData(fileData);

					Directory.CreateDirectory(Path.Combine(outputDirectory, directory.Name));
					File.WriteAllBytes(
						Path.Combine(outputDirectory, directory.Name, fileName),
						fileData);
				}
			}
		}

		static string ReadDeobfuscatedString(BinaryReader reader)
		{
			var length = reader.ReadByte();
			var sb = new StringBuilder(length);
			for (var i = 0; i < length; i++)
			{
				var ch = reader.ReadByte();
				sb.Append((char)(ch ^ (0xFF - length)));
			}

			return sb.ToString();
		}

		static string ReadDeobfuscatedStringAlt(BinaryReader reader)
		{
			var length = reader.ReadByte();
			var sb = new StringBuilder(length);
			for (var i = 0; i < length; i++)
			{
				var ch = reader.ReadByte();
				sb.Append((char)(ch ^ 0xFF));
			}

			return sb.ToString();
		}

		static void DecryptData(byte[] data)
		{
			const string Key1 = "4RaS9D7KaEbxcp2o5r6t";
			const string Key2 = "3tRaUxLmEaSn";

			var keySeed = (data.Length & 0x1fc) >> 2;
			var keyIndex2 = (keySeed % 9) + 1;
			var keyIndex1 = (keySeed % keyIndex2) + 1;
			var swapFlag = false;

			for (var i = 0; i < data.Length; i++)
			{
				data[i] ^= (byte)(Key2[keyIndex2++] ^ keySeed);
				if (swapFlag)
					data[i] = (byte)((data[i] >> 4) | ((data[i] & 0xf) << 4));
				data[i] ^= (byte)Key1[keyIndex1++];

				if (keyIndex1 >= Key1.Length && keyIndex2 >= Key2.Length)
				{
					keySeed = (keySeed + 1) & 0x7F;
					swapFlag = !swapFlag;
					if (swapFlag)
					{
						keyIndex1 = (keySeed % 15) + 3;
						keyIndex2 = (keySeed % 7) + 1;
					}
					else
					{
						keyIndex1 = (keySeed % 12) + 6;
						keyIndex2 = (keySeed % 5) + 4;
					}
				}

				if (keyIndex1 >= Key1.Length)
				{
					keyIndex1 = 1;
					swapFlag = !swapFlag;
				}

				if (keyIndex2 >= Key2.Length)
				{
					keyIndex2 = 1;
					swapFlag = !swapFlag;
				}
			}
		}
	}
}