using Microsoft.Toolkit.HighPerformance.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;

namespace FileExplorerCore.Helpers
{
	public readonly struct Utf8String : IEnumerable<char>
	{
		private readonly byte[] _data;

		public int Length => _data.Length;

		public static Utf8String Empty = new(Array.Empty<byte>());

		public Utf8String(ReadOnlySpan<char> data)
		{
			_data = new byte[data.Length];

			for (int i = 0; i < data.Length; i++)
			{
				_data[i] = (byte)data[i];
			}
		}

		public Utf8String(ReadOnlySpan<byte> data)
		{
			_data = data.ToArray();
		}

		public char this[int index] => (char)_data[index];


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
				if (Char.IsWhiteSpace(this[i]))
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
				span[i] = this[i];
			}
		}

		public override string ToString()
		{
			return String.Create(Length, this, (span, data) => data.CopyToSpan(span));
		}

		public override int GetHashCode()
		{
			return HashCode<byte>.Combine(_data);
		}

		public IEnumerator<char> GetEnumerator()
		{
			for (int i = 0; i < _data.Length; i++)
			{
				yield return (char)_data[i];
			}
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}
}
