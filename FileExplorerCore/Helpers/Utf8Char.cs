using System;

namespace FileExplorerCore.Helpers
{
	public readonly struct Utf8Char
	{
		private readonly byte _data;

		public Utf8Char(byte data)
		{
			_data = data;
		}

		public bool IsWhitespace()
		{
			return Char.IsWhiteSpace(this);
		}

		public static implicit operator Utf8Char(char c) => new Utf8Char((byte)c);
		public static implicit operator char(Utf8Char c) => (char)c._data;
	}
}
