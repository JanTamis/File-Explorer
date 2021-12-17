using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace FileExplorerCore.Helpers
{
	public static class CompressHelper
	{
		public static byte[] Compress(string text)
		{
			var data = GetBytes(text);
			var output = new MemoryStream();

			using (var dstream = new DeflateStream(output, CompressionLevel.Fastest, false))
			{
				dstream.Write(data, 0, data.Length);
			}
			return output.ToArray();
		}

		public static string Decompress(byte[] data)
		{
			var input = new MemoryStream(data);
			var output = new MemoryStream();

			using (var dstream = new DeflateStream(input, CompressionMode.Decompress))
			{
				dstream.CopyTo(output);
			}

			var buffer = new ArraySegment<byte>(output.GetBuffer(), 0, (int)output.Length);

			return new string(GetString(buffer.AsSpan()));
		}

		public static byte[] GetBytes(string str)
		{
			byte[] bytes = new byte[str.Length * sizeof(char)];

			Buffer.BlockCopy(str.ToCharArray(), 0, bytes, 0, bytes.Length);

			return bytes;
		}

		public static ReadOnlySpan<byte> GetBytes(ReadOnlySpan<char> str)
		{
			return MemoryMarshal.AsBytes(str);
		}

		public static ReadOnlySpan<char> GetString(ReadOnlySpan<byte> bytes)
		{
			return MemoryMarshal.Cast<byte, char>(bytes);
		}
	}
}
