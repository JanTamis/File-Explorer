using Microsoft.Toolkit.HighPerformance;
using Microsoft.Toolkit.HighPerformance.Helpers;
using System;

namespace FileExplorerCore.Helpers
{
	public readonly struct Utf8String
	{
		private readonly Utf8Char[] _data;

		public int Length => _data.Length;

		public static Utf8String Empty = new(Array.Empty<byte>());

		public Utf8String(ReadOnlySpan<char> data)
		{
			_data = new Utf8Char[data.Length];

			for (int i = 0; i < data.Length; i++)
			{
				_data[i] = new Utf8Char((byte)data[i]);
			}
		}

		public Utf8String(ReadOnlySpan<byte> data)
		{
			_data = data.Cast<byte, Utf8Char>().ToArray();
		}

		public Utf8Char this[int index] => _data[index];


		public bool IsEmpty()
		{
			return _data.Length is 0;
		}

		public bool IsEmptyOrWhiteSpace()
		{
			if (IsEmpty())
			{
				return true;
			}

			for (int i = 0; i < _data.Length; i++)
			{
				if (_data[i].IsWhitespace())
				{
					return true;
				}
			}

			return false;
		}

		public void CopyToSpan(Span<char> span)
		{
			for (int i = 0; i < span.Length; i++)
			{
				span[i] = _data[i];
			}
		}

		public override string ToString()
		{
			return String.Create(Length, this, (span, data) =>
			{
				data.CopyToSpan(span);
			});
		}

		public override int GetHashCode()
		{
			return HashCode<Utf8Char>.Combine(_data);
		}
	}
}
