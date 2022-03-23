using Microsoft.Toolkit.HighPerformance.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Microsoft.Toolkit.HighPerformance;

namespace FileExplorerCore.Helpers
{
	public readonly struct Utf8String : IEnumerable<char>
	{
		private readonly byte[] _data;

		public int Length => _isUtf8
			? _data.Length
			: _data.Length * 2;

		private readonly bool _isUtf8;

		public static Utf8String Empty = new(Array.Empty<byte>());

		public Utf8String(ReadOnlySpan<char> data)
		{
			_isUtf8 = true;
			
			for (var i = 0; i < data.Length; i++)
			{
				if (data[i] > Byte.MaxValue)
				{
					_isUtf8 = false;
					break;
				}
			}

			if (_isUtf8)
			{
				_data = new byte[Encoding.UTF8.GetByteCount(data)];
				Encoding.UTF8.GetBytes(data, _data);
			}
			else
			{
				_data = data
					.Cast<char, byte>()
					.ToArray();
			}
		}

		public Utf8String(ReadOnlySpan<byte> data)
		{
			_data = data.ToArray();
			_isUtf8 = true;
		}

		public char this[int index]
		{
			get
			{
				if (_isUtf8)
				{
					Span<char> chars = stackalloc char[Encoding.UTF8.GetCharCount(_data)];
					Encoding.UTF8.GetChars(_data, chars);

					return chars[index];
				}
				else
				{
					var chars = _data.AsSpan().Cast<byte, char>();

					return chars[index];
				}
			}
		}

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

			if (_isUtf8)
			{
				Span<char> chars = stackalloc char[Encoding.UTF8.GetCharCount(_data)];
				Encoding.UTF8.GetChars(_data, chars);

				for (var i = 0; i < chars.Length; i++)
				{
					if (Char.IsWhiteSpace(chars[i]))
					{
						return true;
					}
				}
			}
			else
			{
				var chars = _data.AsSpan().Cast<byte, char>();

				for (var i = 0; i < chars.Length; i++)
				{
					if (Char.IsWhiteSpace(chars[i]))
					{
						return true;
					}
				}
			}

			return false;
		}

		public void CopyToSpan(Span<char> span)
		{
			if (_isUtf8)
			{
				Span<char> chars = stackalloc char[Encoding.UTF8.GetCharCount(_data)];
				Encoding.UTF8.GetChars(_data, chars);

				chars.CopyTo(span);
			}
			else
			{
				var chars = _data.AsSpan().Cast<byte, char>();

				chars.CopyTo(span);
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
			return ToString().GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}
}
