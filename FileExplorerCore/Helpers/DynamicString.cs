using Microsoft.Toolkit.HighPerformance;
using System;
using System.Buffers;
using System.Buffers.Text;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Unicode;

namespace FileExplorerCore.Helpers
{
  /// <summary>
  /// A string that uses the best encoding
  /// </summary>
  public readonly struct DynamicString : IEnumerable<char>, IEqualityComparer<DynamicString>
  {
    private readonly byte[] _data;
    private readonly int _length;

    public int Length => _length;

    public static DynamicString Empty = new(Array.Empty<byte>(), 0);

    public DynamicString(ReadOnlySpan<char> data)
    {
      Span<byte> temp = stackalloc byte[data.Length * sizeof(char)];

      Utf8.FromUtf16(data, temp, out _length, out var bytes, false, false);

      _data = temp[..bytes].ToArray();
    }

    private DynamicString(ReadOnlySpan<byte> data, int length)
    {
      _data = data.ToArray();
      _length = length;
    }

    private char this[int index]
    {
      get
      {
        if (index < 0 || index >= Length)
        {
          throw new ArgumentOutOfRangeException(nameof(index));
        }
        
        var span = GetChars(_data, stackalloc char[index + 1]);

        return span[index];
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
      return Create(str0.Length + str1.Length, (str0, str1), (span, state) =>
      {
        state.str0.CopyToSpan(span);
        state.str1.CopyToSpan(span[state.str0.Length..]);
      });
    }

    public static DynamicString Concat(DynamicString str0, DynamicString str1, DynamicString str2)
    {
      return Create(str0.Length + str1.Length + str2.Length, (str0, str1, str2), (span, state) =>
      {
        state.str0.CopyToSpan(span);
        state.str1.CopyToSpan(span[state.str0.Length..]);
        state.str2.CopyToSpan(span[(state.str0.Length + state.str1.Length)..]);
      });
    }

    public static DynamicString Concat(DynamicString str0, DynamicString str1, DynamicString str2, DynamicString str3)
    {
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
      return new DynamicString(GetChars(_data, stackalloc char[Length]).Trim());
    }

    public DynamicString TrimStart()
    {
      return new DynamicString(GetChars(_data, stackalloc char[Length]).TrimStart());
    }

    public DynamicString TrimEnd()
    {
      return new DynamicString(GetChars(_data, stackalloc char[Length]).TrimEnd());
    }

    #endregion

    #region Remove

    public DynamicString Remove(int startIndex)
    {
      return new DynamicString(GetChars(_data, stackalloc char[Length])
        .Slice(startIndex)
        .TrimStart());
    }

    public DynamicString Remove(int startIndex, int count)
    {
      throw new NotImplementedException();
    }

    #endregion

    #region Substring

    public DynamicString Substring(int startIndex)
    {
      var span = GetChars(_data, stackalloc char[Length]);

      return new DynamicString(span[startIndex..]);
    }

    public DynamicString Substring(int startIndex, int length)
    {
      var span = GetChars(_data, stackalloc char[Length]);

      return new DynamicString(span[startIndex..(startIndex + length)]);
    }

    #endregion

    #region Format

    public static bool TryFormat(bool value, out DynamicString result)
    {
      Span<byte> buffer = stackalloc byte[5];

      if (Utf8Formatter.TryFormat(value, buffer, out var written))
      {
        result = new DynamicString(buffer[..written], written);
        return true;
      }

      result = Empty;
      return false;
    }

    public static bool TryFormat(DateTime value, out DynamicString result)
    {
      Span<byte> buffer = stackalloc byte[50];

      if (Utf8Formatter.TryFormat(value, buffer, out var written))
      {
        result = new DynamicString(buffer[..written], written);
        return true;
      }

      result = Empty;
      return false;
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

      var chars = GetChars(_data, stackalloc char[Length]);

      for (int i = 0; i < chars.Length; i++)
      {
        if (Char.IsWhiteSpace(chars[i]))
        {
          return true;
        }
      }

      return false;
    }

    public bool Contains(char value)
    {
      return GetChars(_data, stackalloc char[Length])
        .Contains(value);
    }

    public int IndexOf(char value)
    {
      return GetChars(_data, stackalloc char[Length])
        .IndexOf(value);
    }

    public int IndexOf(char value, int startIndex)
    {
      return GetChars(_data, stackalloc char[Length])
        .Slice(startIndex)
        .IndexOf(value);
    }

    public int IndexOf(char value, int startIndex, int count)
    {
      return GetChars(_data, stackalloc char[Length])
        .Slice(startIndex, startIndex + count)
        .IndexOf(value);
    }

    public int LastIndexOf(char value)
    {
      return GetChars(_data, stackalloc char[Length])
        .LastIndexOf(value);
    }

    public int LastIndexOf(char value, int startIndex)
    {
      return GetChars(_data, stackalloc char[Length])
        .Slice(startIndex)
        .LastIndexOf(value);
    }

    public int LastIndexOf(char value, int startIndex, int count)
    {
      return GetChars(_data, stackalloc char[Length])
        .Slice(startIndex, startIndex + count)
        .LastIndexOf(value);
    }

    public int IndexOfAny(ReadOnlySpan<char> anyOf)
    {
      return GetChars(_data, stackalloc char[Length])
        .IndexOfAny(anyOf);
    }

    public DynamicString[] Split(char separator)
    {
      var characters = GetChars(_data, stackalloc char[Length]);

      var data = new DynamicString[characters.Count(separator)];
      var index = 0;

      foreach (var element in characters.Tokenize(separator))
      {
        data[index++] = new DynamicString(element);
      }

      return data;
    }

    public int Count(char character)
    {
      return GetChars(_data, stackalloc char[Length])
        .Count(character);
    }

    public void CopyToSpan(Span<char> span)
    {
      Utf8.ToUtf16(_data, span, out _, out _, false, false);
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
      return HashCode.Combine(_data.GetDjb2HashCode(), Length);
    }

    public IEnumerator<char> GetEnumerator()
    {
      var array = ArrayPool<char>.Shared.Rent(Length);
      
      GetChars(_data, array);

      for (var i = 0; i < Length; i++)
      {
        yield return array[i];
      }

      ArrayPool<char>.Shared.Return(array);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }

    public bool Equals(DynamicString x, DynamicString y)
    {
      return x._data.Length == y.Length && x._data.AsSpan().SequenceEqual(y._data);
    }

    public int GetHashCode(DynamicString obj)
    {
      return obj.GetHashCode();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ReadOnlySpan<char> GetChars(ReadOnlySpan<byte> data, Span<char> buffer)
    {
      Utf8.ToUtf16(data, buffer, out var characters, out _, false, false);

      return buffer[..characters];
    }
  }
}