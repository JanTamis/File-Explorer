using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Toolkit.HighPerformance;
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

    public readonly int Length { get; }

    private readonly bool _isUtf8;

    public static DynamicString Empty = new DynamicString(stackalloc byte[0], true);

    public DynamicString(ReadOnlySpan<char> data)
    {
      _isUtf8 = IsUTF8(data);
      Length = data.Length;

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

    private DynamicString(ReadOnlySpan<byte> data, bool isUtf8 = true)
    {
      _data = data.ToArray();
      _isUtf8 = isUtf8;

      Length = isUtf8
        ? data.Length
        : data.Length / 2;
    }

    private DynamicString(byte[] data, bool isUtf8 = true)
    {
      _data = data;
      _isUtf8 = isUtf8;

      Length = isUtf8
        ? data.Length
        : data.Length / 2;
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

    //private DynamicString this[Range range]
    //{
    //  get
    //  {
    //    return GetElement(index);
    //  }
    //}

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

    #region Concat

    public static DynamicString Concat(DynamicString str0, DynamicString str1)
    {
      if (str0._isUtf8 && str1._isUtf8)
      {
        var data = new byte[str0.Length + str1.Length];

        str0._data.CopyTo(data, 0);
        str1._data.CopyTo(data, str0.Length);

        return new DynamicString(data, true);
      }

      if (!str0._isUtf8 && !str1._isUtf8)
      {
        var data = new byte[str0._data.Length + str1._data.Length];

        str0._data.CopyTo(data, 0);
        str1._data.CopyTo(data, str0._data.Length);

        return new DynamicString(data, false);
      }

      return Create(str0.Length + str1.Length, (str0, str1), (span, state) =>
      {
        state.str0.CopyToSpan(span);
        state.str1.CopyToSpan(span[state.str0.Length..]);
      });
    }

    public static DynamicString Concat(DynamicString str0, DynamicString str1, DynamicString str2)
    {
      if (str0._isUtf8 && str1._isUtf8 && str2._isUtf8)
      {
        var data = new byte[str0.Length + str1.Length + str2.Length];

        str0._data.CopyTo(data, 0);
        str1._data.CopyTo(data, str0.Length);
        str2._data.CopyTo(data, str0.Length + str1.Length);

        return new DynamicString(data, true);
      }

      if (!str0._isUtf8 && !str1._isUtf8 && !str2._isUtf8)
      {
        var data = new byte[str0._data.Length + str1._data.Length + str2._data.Length];

        str0._data.CopyTo(data, 0);
        str1._data.CopyTo(data, str0._data.Length);
        str2._data.CopyTo(data, str0._data.Length + str1._data.Length);

        return new DynamicString(data, false);
      }

      return Create(str0.Length + str1.Length + str2.Length, (str0, str1, str2), (span, state) =>
      {
        state.str0.CopyToSpan(span);
        state.str1.CopyToSpan(span[state.str0.Length..]);
        state.str2.CopyToSpan(span[(state.str0.Length + state.str1.Length)..]);
      });
    }

    public static DynamicString Concat(DynamicString str0, DynamicString str1, DynamicString str2, DynamicString str3)
    {
      if (str0._isUtf8 && str1._isUtf8 && str2._isUtf8 && str3._isUtf8)
      {
        var data = new byte[str0.Length + str1.Length + str2.Length + str3.Length];

        str0._data.CopyTo(data, 0);
        str1._data.CopyTo(data, str0.Length);
        str2._data.CopyTo(data, str0.Length + str1.Length);
        str3._data.CopyTo(data, str0.Length + str1.Length + str2.Length);

        return new DynamicString(data, true);
      }

      if (!str0._isUtf8 && !str1._isUtf8 && !str2._isUtf8 && !str3._isUtf8)
      {
        var data = new byte[str0._data.Length + str1._data.Length + str2._data.Length + str3._data.Length];

        str0._data.CopyTo(data, 0);
        str1._data.CopyTo(data, str0._data.Length);
        str2._data.CopyTo(data, str0._data.Length + str1._data.Length);
        str3._data.CopyTo(data, str0._data.Length + str1._data.Length + str2._data.Length);

        return new DynamicString(data, false);
      }

      return Create(str0.Length + str1.Length + str2.Length + str3.Length, (str0, str1, str2, str3), (span, state) =>
      {
        state.str0.CopyToSpan(span);
        state.str1.CopyToSpan(span[state.str0.Length..]);
        state.str2.CopyToSpan(span[(state.str0.Length + state.str1.Length)..]);
        state.str3.CopyToSpan(span[(state.str0.Length + state.str1.Length + state.str2.Length)..]);
      });
    }

    public static DynamicString Concat(params DynamicString[] items)
    {
      var count = 0;

      for (int i = 0; i < items.Length; i++)
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

    #endregion

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
        for (int i = 0; i < span.Length && i < Length; i++)
        {
          span[i] = GetElementUTF8(i);
        }
        //Span<char> chars = stackalloc char[_data.Length];
        //Encoding.UTF8.GetChars(_data, chars);

        //chars.CopyTo(span);
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
      if (_isUtf8)
      {
        return _data.GetDjb2HashCode();
      }

      return _data.AsSpan().Cast<byte, char>().GetDjb2HashCode();
    }

    public IEnumerator<char> GetEnumerator()
    {
      for (int i = 0; i < Length; i++)
      {
        yield return GetElement(i);
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

      return Unsafe.ReadUnaligned<char>(ref Unsafe.AddByteOffset(ref MemoryMarshal.GetArrayDataReference(_data), (nuint)(index * sizeof(char))));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private char GetElementUTF8(int index)
    {
      return (char)Unsafe.AddByteOffset(ref MemoryMarshal.GetArrayDataReference(_data), (nuint)index);
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