using Microsoft.Toolkit.HighPerformance.Helpers;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Toolkit.HighPerformance;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FileExplorerCore.Helpers
{
	/// <summary>
	/// A string that uses the best encoding
	/// </summary>
	public readonly struct DynamicString : IEnumerable<char>, IEqualityComparer<DynamicString>
	{
		private readonly byte[] _data;

		public int Length => _isUtf8
			? _data.Length
			: _data.Length * 2;

		private readonly bool _isUtf8;

		public static DynamicString Empty = new DynamicString();

		public DynamicString(ReadOnlySpan<char> data)
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
				_data = new byte[data.Length];

				for (var i = 0; i < _data.Length; i++)
				{
					_data[i] = (byte)data[i];
				}
			}
			else
			{
				_data = data
					.Cast<char, byte>()
					.ToArray();
			}
		}

		public DynamicString(ReadOnlySpan<byte> data)
		{
			_data = data.ToArray();
			_isUtf8 = true;
		}

		private char this[int index]
		{
			get
			{
				if (index < 0 || index >= Length)
				{
					throw new ArgumentOutOfRangeException(nameof(index));
				}

				if (_isUtf8)
				{
					return (char)Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_data), index);
				}

				return Unsafe.ReadUnaligned<char>(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_data), index * 2));
			}
		}

		public static DynamicString Create<TState>(int length, TState state, SpanAction<char, TState> action)
		{
			char[]? array = null;

			var data = length <= 512
				? stackalloc char[length]
				: array = ArrayPool<char>.Shared.Rent(length);

			action(data, state);

			var result = new DynamicString(data);

			if (array != null)
			{
				ArrayPool<char>.Shared.Return(array);
			}

			return result;
		}

		public static DynamicString Join(char separator, params DynamicString[] strings)
		{
			var length = strings.Sum(s => s.Length) + strings.Length - 1;

			return Create(length, strings, (span, state) =>
			{
				var index = 0;

				foreach (var s in state)
				{
					s.CopyToSpan(span[index..]);
					
					index += s.Length;

					if (index < length)
					{
						span[index++] = separator;
					}
				}
			});
		}

		public DynamicString Trim()
		{
			int start;
			int end;

			for (start = 0; start < _data.Length; start++)
			{
				if (!Char.IsWhiteSpace(GetElement(start)))
				{
					break;
				}
			}

			for (end = _data.Length - 1; end >= 0; end--)
			{
				if (!Char.IsWhiteSpace(GetElement(end)))
				{
					break;
				}
			}

			var length = end - start + 1;

			return Create(length, this, (span, item) => item.CopyToSpan(span));
		}

		public DynamicString TrimLeft()
		{
			int start;

			for (start = 0; start < _data.Length; start++)
			{
				if (!Char.IsWhiteSpace(GetElement(start)))
				{
					break;
				}
			}

			var length = start + 1;

			return Create(length, this, (span, item) => item.CopyToSpan(span));
		}

		public DynamicString TrimRight()
		{
			int end;

			for (end = _data.Length - 1; end >= 0; end--)
			{
				if (!Char.IsWhiteSpace(GetElement(end)))
				{
					break;
				}
			}

			var length = Length - end;

			return Create(length, this, (span, item) => item.CopyToSpan(span));
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

			for (var i = 0; i < Length; i++)
			{
				if (Char.IsWhiteSpace(GetElement(i)))
				{
					return true;
				}
			}

			return false;
		}

		public bool Contains(char value)
		{
			switch (_isUtf8)
			{
				case false:
					return MemoryMarshal.Cast<byte, char>(_data).Contains(value);
				case true when value <= Byte.MaxValue:
					return _data.AsSpan().Contains((byte)value);
			}

			for (var i = 0; i < Length; i++)
			{
				if (GetElement(i) == value)
				{
					return true;
				}
			}

			return false;
		}

		public int IndexOf(char value)
		{
			switch (_isUtf8)
			{
				case false:
					return MemoryMarshal.Cast<byte, char>(_data).IndexOf(value);
				case true when value <= Byte.MaxValue:
					return _data.AsSpan().IndexOf((byte)value);
			}

			for (var i = 0; i < Length; i++)
			{
				if (GetElement(i) == value)
				{
					return i;
				}
			}

			return -1;
		}

		public int IndexOf(char value, int startIndex)
		{
			if (startIndex < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(startIndex));
			}

			if (startIndex >= Length)
			{
				return -1;
			}

			switch (_isUtf8)
			{
				case false:
					return MemoryMarshal.Cast<byte, char>(_data)[startIndex..].IndexOf(value);
				case true when value <= Byte.MaxValue:
					return _data.AsSpan(startIndex).IndexOf((byte)value);
			}

			for (var i = startIndex; i < Length; i++)
			{
				if (GetElement(i) == value)
				{
					return i;
				}
			}

			return -1;
		}

		public int IndexOf(char value, int startIndex, int count)
		{
			if (startIndex < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(startIndex));
			}

			if (startIndex >= Length)
			{
				return -1;
			}

			if (count < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(count));
			}

			switch (_isUtf8)
			{
				case false:
					return MemoryMarshal.Cast<byte, char>(_data)[startIndex..].IndexOf(value);
				case true when value <= Byte.MaxValue:
					return _data.AsSpan(startIndex).IndexOf((byte)value);
			}

			for (var i = startIndex; i < Length; i++)
			{
				if (GetElement(i) == value)
				{
					return i;
				}
			}

			return -1;
		}

		public int LastIndexOf(char value)
		{
			switch (_isUtf8)
			{
				case false:
					return MemoryMarshal.Cast<byte, char>(_data).LastIndexOf(value);
				case true when value <= Byte.MaxValue:
					return _data.AsSpan().LastIndexOf((byte)value);
			}

			for (var i = Length - 1; i >= 0; i--)
			{
				if (GetElement(i) == value)
				{
					return i;
				}
			}

			return -1;
		}

		public int LastIndexOf(char value, int startIndex)
		{
			if (startIndex < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(startIndex));
			}

			if (startIndex >= Length)
			{
				return -1;
			}

			switch (_isUtf8)
			{
				case false:
					return MemoryMarshal.Cast<byte, char>(_data)[startIndex..].LastIndexOf(value);
				case true when value <= Byte.MaxValue:
					return _data.AsSpan(0, startIndex).LastIndexOf((byte)value);
			}

			for (var i = startIndex - 1; i >= 0; i--)
			{
				if (GetElement(i) == value)
				{
					return i;
				}
			}

			return -1;
		}

		public int LastIndexOf(char value, int startIndex, int count)
		{
			if (startIndex < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(startIndex));
			}

			if (startIndex >= Length)
			{
				return -1;
			}

			if (count < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(count));
			}

			switch (_isUtf8)
			{
				case false:
					return MemoryMarshal.Cast<byte, char>(_data)[startIndex..(startIndex + count)].LastIndexOf(value);
				case true when value <= Byte.MaxValue:
					return _data.AsSpan(0, startIndex).LastIndexOf((byte)value);
			}

			for (var i = startIndex - 1; i >= 0; i--)
			{
				if (GetElement(i) == value)
				{
					return i;
				}
			}

			return -1;
		}

		public int IndexOfAny(ReadOnlySpan<Char> anyOf)
		{
			if (anyOf.Length == 0)
			{
				throw new ArgumentOutOfRangeException(nameof(anyOf), "sequence must contains elements");
			}

			if (!_isUtf8)
			{
				return MemoryMarshal.Cast<byte, char>(_data).IndexOfAny(anyOf);
			}

			for (var i = 0; i < Length; i++)
			{
				var result = IndexOf(GetElement(i));

				if (result != -1)
				{
					return result;
				}
			}

			return -1;
		}

		public DynamicString[] Split(char separator)
		{
			var data = new DynamicString[CharacterCount(separator)];
			var index = 0;
			
			switch (_isUtf8)
			{
				case true when separator <= Byte.MaxValue:
				{
					foreach (var element in _data.Tokenize((byte)separator))
					{
						data[index++] = new DynamicString(element);
					}

					break;
				}
				case false when separator > Byte.MaxValue:
				{
					foreach (var element in _data.AsSpan().Cast<byte, char>().Tokenize(separator))
					{
						data[index++] = new DynamicString(element);
					}

					break;
				}
				default:
				{
					
					for (var i = 0; i < Length; i++)
					{
						if (GetElement(i) == separator)
						{
							data[i++] = new DynamicString();
						}
					}

					break;
				}
			}

			return data;
		}

		public int CharacterCount(char character)
		{
			switch (_isUtf8)
			{
				case true when character <= Byte.MaxValue:
					return _data.Count((byte)character);
				case false when character > Byte.MaxValue:
					return MemoryMarshal.Cast<byte, char>(_data).Count(character);
				default:
					var count = 0;

					for (var i = 0; i < Length; i++)
					{
						var result = GetElement(i) == character;
						
						count += Unsafe.As<bool, byte>(ref result);
					}

					return count;
			}
		}

		public void CopyToSpan(Span<char> span)
		{
			if (_isUtf8)
			{
				Span<char> chars = stackalloc char[_data.Length];
				Encoding.UTF8.GetChars(_data, chars);

				chars.CopyTo(span);
			}
			else
			{
				var chars = _data
					.AsSpan()
					.Cast<byte, char>();

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
			
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public bool Equals(DynamicString x, DynamicString y)
		{
			return x._isUtf8 == y._isUtf8 && x._data.Length == y.Length && x._data.AsSpan().SequenceEqual(y._data);
		}

		public int GetHashCode(DynamicString obj)
		{
			return obj.GetHashCode();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private char GetElement(int index)
		{
			if (_isUtf8)
			{
				return (char)Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_data), index);
			}
			
			return Unsafe.ReadUnaligned<char>(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_data), index * 2));
		}
	}
}