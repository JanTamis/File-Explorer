using Microsoft.Toolkit.HighPerformance;
using System;
using System.Buffers;
using System.Buffers.Text;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Unicode;

namespace FileExplorerCore.Helpers;

/// <summary>
/// A string that uses the best encoding
/// </summary>
public class DynamicString : IEnumerable<Rune>, IEqualityComparer<DynamicString>
{
  private readonly byte[] _data;

  private readonly int _length;

  public int Length => _length;

  private int ByteLength => _data.Length;

  public static DynamicString Empty = new(Array.Empty<byte>(), 0);

  public DynamicString(ReadOnlySpan<char> data)
  {
    if (data.IsEmpty)
    {
      _data = Array.Empty<byte>();
      _length = 0;

      return;
    }

    // var codePoints = new CodePointEnumerator(data);
    Span<byte> span = stackalloc byte[data.Length * 2];

    Utf8.FromUtf16(data, span, out _, out var bytes);

    _length = data.Length;
    _data = span[0..bytes].ToArray();
  }

  private DynamicString(byte[] data, int length)
  {
    _data = data;
    _length = length;
  }

  public Rune this[Index index]
  {
    get
    {
      var offset = index.GetOffset(Length);

      if (offset < 0 || offset >= Length)
      {
        throw new ArgumentOutOfRangeException(nameof(index));
      }

      var startOffset = 0;

      for (int i = 0; i < offset; i++)
      {
        var result = TryGetCount(_data, startOffset);

        if (result != 255)
        {
          startOffset += result;
        }
      }

      Decode(_data, startOffset, out var codePoint);

      return new Rune(codePoint);
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

  public static DynamicString Create<TState>(int length, TState state, SpanAction<char, TState> action)
  {
    using var buffer = new Buffer<char>(length);

    action(buffer, state);

    return new DynamicString(buffer);
  }

  #region Concat

  public static DynamicString Concat(DynamicString str0, DynamicString str1)
  {
    var length = str0.ByteLength + str1.ByteLength;

    var buffer = new byte[length];

    str0.CopyTo(buffer);
    str1.CopyTo(buffer.AsSpan(str1.ByteLength));

    return new DynamicString(buffer, str0.Length + str1.Length);
  }

  public static DynamicString Concat(DynamicString str0, DynamicString str1, DynamicString str2)
  {
    var length = str0.ByteLength + str1.ByteLength + str2.ByteLength;

    var buffer = new byte[length];

    str0.CopyTo(buffer);
    str1.CopyTo(buffer.AsSpan(str0.ByteLength));
    str2.CopyTo(buffer.AsSpan(str0.ByteLength + str1.ByteLength));

    return new DynamicString(buffer, str0.Length + str1.Length + str2.Length);
  }

  public static DynamicString Concat(DynamicString str0, DynamicString str1, DynamicString str2, DynamicString str3)
  {
    var length = str0.ByteLength + str1.ByteLength + str2.ByteLength + str3.ByteLength;

    var buffer = new byte[length];

    str0.CopyTo(buffer);
    str1.CopyTo(buffer.AsSpan(str0.ByteLength));
    str2.CopyTo(buffer.AsSpan(str0.ByteLength + str1.ByteLength));
    str3.CopyTo(buffer.AsSpan(str0.ByteLength + str1.ByteLength + str2.ByteLength));

    return new DynamicString(buffer, str0.Length + str1.Length + str2.Length + str3.Length);
  }

  public static DynamicString Concat(params DynamicString[] items)
  {
    var length = 0;
    var byteLength = 0;

    foreach (var item in items)
    {
      length += item.Length;
      byteLength += item.ByteLength;
    }

    using var builder = new ArrayPoolList<byte>(byteLength);

    foreach (var str in items)
    {
      str.CopyTo(builder.AppendSpan(str.ByteLength));
    }

    return new DynamicString(builder.ToArray(), length);
  }

  #endregion

  #region Trimming

  public DynamicString Trim()
  {
    using var buffer = new Buffer<char>(Length);

    return new DynamicString(GetChars(_data, buffer).Trim());
  }

  public DynamicString TrimStart()
  {
    using var buffer = new Buffer<char>(Length);

    return new DynamicString(GetChars(_data, buffer).TrimStart());
  }

  public DynamicString TrimEnd()
  {
    using var buffer = new Buffer<char>(Length);

    return new DynamicString(GetChars(_data, buffer).TrimEnd());
  }

  #endregion

  #region Remove

  public DynamicString Remove(int startIndex)
  {
    var endIndex = 0;

    for (int i = 0; i < startIndex; i++)
    {
      var count = TryGetCount(_data, endIndex);

      if (count != 255)
      {
        endIndex += count;
      }
    }

    return new DynamicString(_data[0..endIndex], startIndex);
  }

  public DynamicString Remove(int startIndex, int count)
  {
    using var builder = new ArrayPoolList<char>(Length - count);
    using var buffer = new Buffer<char>(Length);

    var chars = GetChars(_data, buffer);

    chars[..startIndex].CopyTo(builder.AppendSpan(startIndex));
    chars[(startIndex + count)..].CopyTo(builder.AppendSpan(Length - startIndex - count));

    return new DynamicString(builder.AsSpan());
  }

  #endregion

  #region Substring

  public DynamicString Substring(int startIndex)
  {
    return Substring(startIndex, Length - startIndex);
  }

  public DynamicString Substring(int startIndex, int length)
  {
    var byteStartOffset = 0;

    for (int i = 0; i < startIndex; i++)
    {
      var result = TryGetCount(_data, byteStartOffset);

      if (result != 255)
      {
        byteStartOffset += result;
      }
    }

    var byteEndOffset = byteStartOffset;

    for (int i = 0; i < length; i++)
    {
      var result = TryGetCount(_data, byteEndOffset);

      if (result != 255)
      {
        byteEndOffset += result;
      }
    }

    return new DynamicString(_data[byteStartOffset..(byteEndOffset - byteStartOffset)], length);
  }

  #endregion

  #region Format

  public static bool TryFormat(bool value, out DynamicString result)
  {
    Span<byte> buffer = stackalloc byte[5];

    if (Utf8Formatter.TryFormat(value, buffer, out var written))
    {
      result = new DynamicString(buffer[..written].ToArray(), written);
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
      result = new DynamicString(buffer[..written].ToArray(), written);
      return true;
    }

    result = Empty;
    return false;
  }

  #endregion

  public char[] ToCharArray()
  {
    var array = new char[Length];

    CopyTo(array);

    return array;
  }

  public static DynamicString Join(char separator, params DynamicString[] strings)
  {
    var length = strings.Length - 1;
    var byteLength = 0;

    var rune = new Rune(separator);

    foreach (var item in strings)
    {
      length += item.Length;
      byteLength += item.ByteLength;
    }

    using var builder = new ArrayPoolList<byte>(byteLength);

    for (var i = 0; i < strings.Length; i++)
    {
      var str = strings[i];

      str.CopyTo(builder.AppendSpan(str.ByteLength));

      if (i < strings.Length - 1)
      {
        rune.EncodeToUtf8(builder.AppendSpan(rune.Utf8SequenceLength));
      }
    }

    return new DynamicString(builder.ToArray(), length);
  }

  public static DynamicString Join(char separator, params string[] strings)
  {
    var length = strings.Length - 1;

    foreach (var item in strings)
    {
      length += item.Length;
    }

    using var builder = new ValueStringBuilder(length);

    for (var i = 0; i < strings.Length; i++)
    {
      var str = strings[i];

      str.CopyTo(builder.AppendSpan(str.Length));

      if (i < strings.Length - 1)
      {
        builder.Append(separator);
      }
    }

    return new DynamicString(builder.AsSpan());
  }

  public bool IsEmpty()
  {
    return ByteLength is 0;
  }

  public bool IsEmptyOrWhiteSpace()
  {
    if (IsEmpty())
    {
      return true;
    }

    foreach (var rune in this)
    {
      if (Rune.IsWhiteSpace(rune))
      {
        return true;
      }
    }

    return false;
  }

  public bool Contains(char value)
  {
    var rune = new Rune(value);

    foreach (var item in this)
    {
      if (rune == item)
      {
        return true;
      }
    }

    return false;
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

  public static DynamicString[] Split(char separator, DynamicString items)
  {
    return items.Split(separator);
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

  public void CopyTo(Span<char> span)
  {
    if (!span.IsEmpty && ByteLength > 0)
    {
      GetChars(_data, span);
    }
  }

  public void CopyTo(Span<byte> span)
  {
    if (!span.IsEmpty && ByteLength > 0)
    {
      _data.CopyTo(span);
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public ReadOnlySpan<byte> AsBytes()
  {
    return _data.AsSpan();
  }

  public override string ToString()
  {
    return Encoding.UTF8.GetString(_data);
  }

  public override int GetHashCode()
  {
    return HashCode.Combine(_data.GetDjb2HashCode(), Length);
  }

  public IEnumerator<Rune> GetEnumerator()
  {
    for (int i = 0; i < ByteLength;)
    {
      i += Decode(_data, i, out var codePoint);

      yield return new Rune(codePoint);
    }
  }

  IEnumerator IEnumerable.GetEnumerator()
  {
    return GetEnumerator();
  }

  public bool Equals(DynamicString? x, DynamicString? y)
  {
    return x == y;
  }

  int IEqualityComparer<DynamicString>.GetHashCode(DynamicString obj)
  {
    return obj.GetHashCode();
  }

  public static bool operator ==(DynamicString? x, DynamicString? y)
  {
    if (x is null && y is null)
    {
      return true;
    }
    if (x is null || y is null)
    {
      return false;
    }

    return x.Length == y.Length && x._data.AsSpan().SequenceEqual(y._data);
  }

  public static bool operator !=(DynamicString? x, DynamicString? y)
  {
    return !(x == y);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private static ReadOnlySpan<char> GetChars(ReadOnlySpan<byte> data, Span<char> buffer)
  {
    Utf8.ToUtf16(data, buffer, out var characters, out _, false, false);

    if (characters < buffer.Length)
    {
      return buffer[..characters];
    }

    return ReadOnlySpan<char>.Empty;
  }

  public override bool Equals(object? obj)
  {
    return obj is DynamicString dynamicString && Equals(dynamicString, this);
  }

  private static byte TryGetCount(ReadOnlySpan<byte> buffer, int index)
  {
    if (index >= buffer.Length)
      return 255;

    uint x = buffer[index];

    var byteCount =
        (x < 192U) ? (byte)1 :
        (x < 224U) ? (byte)2 :
        (x < 240U) ? (byte)3 :
        (byte)4;

    if (index + byteCount > buffer.Length)
      return 255;

    return byteCount;
  }

  private static unsafe int Encode(uint codePoint, Span<byte> to)
  {
    if (codePoint < 128)
    {
      to[0] = (byte)codePoint;
      return 1;
    }
    else if (codePoint < 2048)
    {
      to[0] = (byte)(0b1100_0000 | ((codePoint >> 6) & 0b01_1111));
      to[1] = (byte)(0b1000_0000 | (codePoint & 0b11_1111));
      return 2;
    }
    else if (codePoint < 65536)
    {
      to[0] = (byte)(0b1110_0000 | ((codePoint >> 12) & 0b1111));
      to[1] = (byte)(0b1000_0000 | ((codePoint >> 6) & 0b11_1111));
      to[2] = (byte)(0b1000_0000 | (codePoint & 0b11_1111));
      return 3;
    }
    else
    {
      to[0] = (byte)(0b1111_0000 | ((codePoint >> 18) & 0b0111));
      to[1] = (byte)(0b1000_0000 | ((codePoint >> 12) & 0b11_1111));
      to[2] = (byte)(0b1000_0000 | ((codePoint >> 6) & 0b11_1111));
      to[3] = (byte)(0b1000_0000 | (codePoint & 0b11_1111));
      return 4;
    }
  }

  public static byte Decode(ReadOnlySpan<byte> buffer, int index, out uint codePoint)
  {
    const byte InvalidCount = 0xff;
    const uint EoS = 0xffff_ffff;

    if (index >= buffer.Length) { codePoint = EoS; return InvalidCount; }

    uint code = buffer[index];

    if (code < 0b1100_0000)
    {
      // ASCII 文字
      codePoint = code;
      return 1;
    }
    if (code < 0b1110_0000)
    {
      // 2バイト文字
      if (index + 1 >= buffer.Length) { codePoint = EoS; return InvalidCount; }
      code &= 0b1_1111;
      code = (code << 6) | (uint)(buffer[++index] & 0b0011_1111);
      codePoint = code;
      return 2;
    }
    if (code < 0b1111_0000)
    {
      // 3バイト文字
      if (index + 2 >= buffer.Length) { codePoint = EoS; return InvalidCount; }
      code &= 0b1111;
      code = (code << 6) | (uint)(buffer[++index] & 0b0011_1111);
      code = (code << 6) | (uint)(buffer[++index] & 0b0011_1111);
      codePoint = code;
      return 3;
    }

    // 4バイト文字
    if (index + 3 >= buffer.Length) { codePoint = EoS; return InvalidCount; }
    code &= 0b0111;
    code = (code << 6) | (uint)(buffer[++index] & 0b0011_1111);
    code = (code << 6) | (uint)(buffer[++index] & 0b0011_1111);
    code = (code << 6) | (uint)(buffer[++index] & 0b0011_1111);
    codePoint = code;
    return 4;
  }
}