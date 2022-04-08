using Microsoft.Toolkit.HighPerformance;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace FileExplorerCore.Helpers
{
  /// <summary>
  /// A string that uses the best encoding
  /// </summary>
  public readonly struct DynamicString : IEnumerable<char>, IEqualityComparer<DynamicString>
  {
    private readonly byte[] _data;

    public int Length { get; }

    private readonly bool _isUtf8;

    public static DynamicString Empty = new DynamicString(Array.Empty<byte>(), true);

    public DynamicString(ReadOnlySpan<char> data)
    {
      _isUtf8 = IsUTF8(data);
      Length = data.Length;

      if (_isUtf8)
      {
	      _data = new byte[data.Length];
	      var textSpan = MemoryMarshal.AsBytes(data);

	      for (var i = 0; i < _data.Length; i++)
	      {
		      _data[i] = textSpan[i << 1];
	      }
      }
      else
      {
        _data = MemoryMarshal.AsBytes(data).ToArray();
      }
    }

    private DynamicString(ReadOnlySpan<byte> data, bool isUtf8 = true)
    {
      _data = data.ToArray();
      _isUtf8 = isUtf8;

      Length = isUtf8
        ? data.Length
        : data.Length >> 1;
    }

    private DynamicString(byte[] data, bool isUtf8 = true)
    {
      _data = data;
      _isUtf8 = isUtf8;

      Length = isUtf8
        ? data.Length
        : data.Length >> 1;
    }

    private char this[int index]
    {
      get
      {
        if (index < 0 || index >= Length)
        {
          throw new ArgumentOutOfRangeException(nameof(index));
        }

        return GetElement(index);
      }
    }

    public DynamicString this[Range range]
    {
      get
      {
        var (offset, length) = range.GetOffsetAndLength(Length);

        return Substring(offset, length);
      }
    }

    public static unsafe DynamicString Create<TState>(int length, TState state, SpanAction<char, TState> action)
    {
      IntPtr array = default;

      Marshal.AllocHGlobal(length);

      var data = length <= 512
        ? stackalloc char[length]
        : new Span<char>((array = Marshal.AllocHGlobal(length * sizeof(char))).ToPointer(), length);

      action(data, state);

      var result = new DynamicString(data);

      if (array != default)
      {
        Marshal.FreeHGlobal(array);
      }

      return result;
    }

    #region Concat

    public static DynamicString Concat(DynamicString str0, DynamicString str1)
    {
      switch (str0._isUtf8)
      {
        case true when str1._isUtf8:
          {
            var data = new byte[str0.Length + str1.Length];

            str0._data.CopyTo(data, 0);
            str1._data.CopyTo(data, str0.Length);

            return new DynamicString(data, true);
          }
        case false when !str1._isUtf8:
          {
            var data = new byte[str0._data.Length + str1._data.Length];

            str0._data.CopyTo(data, 0);
            str1._data.CopyTo(data, str0._data.Length);

            return new DynamicString(data, false);
          }
        default:
          return Create(str0.Length + str1.Length, (str0, str1), (span, state) =>
          {
            state.str0.CopyToSpan(span);
            state.str1.CopyToSpan(span[state.str0.Length..]);
          });
      }
    }

    public static DynamicString Concat(DynamicString str0, DynamicString str1, DynamicString str2)
    {
      switch (str0._isUtf8)
      {
        case true when str1._isUtf8 && str2._isUtf8:
          {
            var data = new byte[str0.Length + str1.Length + str2.Length];

            str0._data.CopyTo(data, 0);
            str1._data.CopyTo(data, str0.Length);
            str2._data.CopyTo(data, str0.Length + str1.Length);

            return new DynamicString(data, true);
          }
        case false when !str1._isUtf8 && !str2._isUtf8:
          {
            var data = new byte[str0._data.Length + str1._data.Length + str2._data.Length];

            str0._data.CopyTo(data, 0);
            str1._data.CopyTo(data, str0._data.Length);
            str2._data.CopyTo(data, str0._data.Length + str1._data.Length);

            return new DynamicString(data, false);
          }
        default:
          return Create(str0.Length + str1.Length + str2.Length, (str0, str1, str2), (span, state) =>
          {
            state.str0.CopyToSpan(span);
            state.str1.CopyToSpan(span[state.str0.Length..]);
            state.str2.CopyToSpan(span[(state.str0.Length + state.str1.Length)..]);
          });
      }
    }

    public static DynamicString Concat(DynamicString str0, DynamicString str1, DynamicString str2, DynamicString str3)
    {
      switch (str0._isUtf8)
      {
        case true when str1._isUtf8 && str2._isUtf8 && str3._isUtf8:
          {
            var data = new byte[str0.Length + str1.Length + str2.Length + str3.Length];

            str0._data.CopyTo(data, 0);
            str1._data.CopyTo(data, str0.Length);
            str2._data.CopyTo(data, str0.Length + str1.Length);
            str3._data.CopyTo(data, str0.Length + str1.Length + str2.Length);

            return new DynamicString(data, true);
          }
        case false when !str1._isUtf8 && !str2._isUtf8 && !str3._isUtf8:
          {
            var data = new byte[str0._data.Length + str1._data.Length + str2._data.Length + str3._data.Length];

            str0._data.CopyTo(data, 0);
            str1._data.CopyTo(data, str0._data.Length);
            str2._data.CopyTo(data, str0._data.Length + str1._data.Length);
            str3._data.CopyTo(data, str0._data.Length + str1._data.Length + str2._data.Length);

            return new DynamicString(data, false);
          }
        default:
          return Create(str0.Length + str1.Length + str2.Length + str3.Length, (str0, str1, str2, str3), (span, state) =>
          {
            state.str0.CopyToSpan(span);
            state.str1.CopyToSpan(span[state.str0.Length..]);
            state.str2.CopyToSpan(span[(state.str0.Length + state.str1.Length)..]);
            state.str3.CopyToSpan(span[(state.str0.Length + state.str1.Length + state.str2.Length)..]);
          });
      }
    }

    public static DynamicString Concat(params DynamicString[] items)
    {
      var count = 0;

      for (var i = 0; i < items.Length; i++)
      {
        count += items[i].Length;
      }

      return Create(count, items, (span, state) =>
      {
        var tempSpan = span;

        foreach (var item in state)
        {
          item.CopyToSpan(tempSpan);

          tempSpan = tempSpan[item.Length..];
        }
      });
    }

    #endregion

    #region Trimming

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

      var length = end - start;

      if (_isUtf8)
      {
        return new DynamicString(_data.AsSpan(start, length), _isUtf8);
      }

      return new DynamicString(_data.AsSpan(start * 2, length * 2), _isUtf8);
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

      if (_isUtf8)
      {
        return new DynamicString(_data.AsSpan(start, length), _isUtf8);
      }

      return new DynamicString(_data.AsSpan(start * 2, length * 2), _isUtf8);
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

      if (_isUtf8)
      {
        return new DynamicString(_data.AsSpan(0, length), _isUtf8);
      }

      return new DynamicString(_data.AsSpan(0, length * 2), _isUtf8);
    }

    #endregion

    #region Remove

    public DynamicString Remove(int startIndex)
    {
      if (!_isUtf8)
      {
        startIndex *= 2;
      }

      return new DynamicString(_data[..startIndex], _isUtf8);
    }

    public DynamicString Remove(int startIndex, int count)
    {
      // TODO Implementation is not correct

      if (!_isUtf8)
      {
        startIndex *= 2;
        count *= 2;
      }

      return new DynamicString(_data[startIndex..(startIndex + count)], _isUtf8);
    }

    #endregion

    #region Substring

    public DynamicString Substring(int startIndex)
    {
      if (!_isUtf8)
      {
        startIndex *= 2;
      }

      return new DynamicString(_data[startIndex..], _isUtf8);
    }

    public DynamicString Substring(int startIndex, int length)
    {
      if (!_isUtf8)
      {
        startIndex *= 2;
        length *= 2;
      }

      return new DynamicString(_data[startIndex..(startIndex + length)], _isUtf8);
    }

    #endregion

    public char[] ToCharArray()
    {
      var array = new char[Length];

      CopyToSpan(array);

      return array;
    }

    public static DynamicString Join(char separator, params DynamicString[] strings)
    {
      var length = strings.Sum(s => s.Length) + strings.Length - 1;

      return Create(length, (strings, separator), (span, state) =>
      {
        var index = 0;

        foreach (var s in state.strings)
        {
          s.CopyToSpan(span[index..]);

          index += s.Length;

          if (index < span.Length)
          {
            span[index++] = state.separator;
          }
        }
      });
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
      var data = new DynamicString[Count(separator)];
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
            //for (var i = 0; i < Length; i++)
            //{
            //  if (GetElement(i) == separator)
            //  {
            //    data[i++] = new DynamicString();
            //  }
            //}

            break;
          }
      }

      return data;
    }

    public int Count(char character)
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
        Encoding.UTF8.GetChars(_data, span);
      }
      else
      {
        _data
          .AsSpan()
          .Cast<byte, char>()
          .CopyTo(span);
      }
    }

    public ReadOnlySpan<byte> AsBytes()
    {
      return _data.AsSpan();
    }

    public override string ToString()
    {
      return String.Create(Length, this, (span, data) => data.CopyToSpan(span));
    }

    public override int GetHashCode()
    {
      return HashCode.Combine(_data.GetDjb2HashCode(), _isUtf8.GetHashCode());
    }

    public IEnumerator<char> GetEnumerator()
    {
      if (_isUtf8)
      {
        for (var i = 0; i < Length; i++)
        {
          yield return GetElementUTF8(i);
        }
      }
      else
      {
        for (var i = 0; i < Length; i++)
        {
          yield return GetElementNonUTF8(i);
        }
      }
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
        return GetElementUTF8(index);
      }

      return GetElementNonUTF8(index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private char GetElementUTF8(int index)
    {
      return (char)Unsafe.AddByteOffset(ref MemoryMarshal.GetArrayDataReference(_data), (nuint)index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private char GetElementNonUTF8(int index)
    {
      return Unsafe.ReadUnaligned<char>(ref Unsafe.AddByteOffset(ref MemoryMarshal.GetArrayDataReference(_data), (nuint)(index * sizeof(char))));
    }

    private static bool IsUTF8(ReadOnlySpan<char> data)
    {
      for (var i = 0; i < data.Length; i++)
      {
        if (data[i] > Byte.MaxValue)
        {
          return false;
        }
      }

      return true;
    }
  }
}