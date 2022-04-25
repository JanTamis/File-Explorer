using System;
using Microsoft.Toolkit.HighPerformance;
using System.Buffers;
using System.Buffers.Text;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Unicode;

namespace FileExplorerCore.Helpers;

/// <summary>
/// A string that uses the UTF-8 encoding
/// </summary>
public class Utf8String : IEnumerable<char>
{
  private readonly byte[] _data;

  private int _length = -1;

  public int Length
  {
    get
    {
      if (_length is -1)
      {
        for (var i = 0; i < _data.Length; i++)
        {
          var x = _data[i];
          var result = (x & 192) != 128;

          _length += Unsafe.As<bool, byte>(ref result);
        }
      }

      return _length;
    }
  }

  private int ByteLength => _data.Length;

  public static readonly Utf8String Empty = new(Array.Empty<byte>(), 0);

  public Utf8String(ReadOnlySpan<char> data)
  {
    if (data.IsEmpty)
    {
      _data = Array.Empty<byte>();
      _length = 0;

      return;
    }

    _data = new byte[Encoding.UTF8.GetByteCount(data)];

    Utf8.FromUtf16(data, _data, out _length, out _);
  }

  public static Utf8String FromFile(string filepath)
  {
    using var stream = File.OpenRead(filepath);

    return FromStream(stream);
  }

  public static Utf8String FromStream(Stream data)
  {
    if (data.CanRead)
    {
      var buffer = new byte[data.Length];
      data.Read(buffer);

      return new Utf8String(buffer);
    }

    throw new ArgumentException("can't read data from the stream", nameof(data));
  }

  private Utf8String(byte[] data, int length = -1)
  {
    _data = data;
    _length = length;
  }

  public char this[Index index]
  {
    get
    {
      var offset = index.GetOffset(Length);

      if (offset >= Length)
      {
        throw new ArgumentOutOfRangeException(nameof(index));
      }

      Span<char> buffer = stackalloc char[2];

      if (index.IsFromEnd)
      {
        var byteOffset = ByteLength;
        var charOffset = Length - 1;

        while (Rune.DecodeLastFromUtf8(_data.AsSpan(0, byteOffset), out var rune, out var bytesConsumed) is OperationStatus.Done)
        {
          if (charOffset <= offset)
          {
            rune.EncodeToUtf16(buffer);
            return buffer[charOffset - offset];
          }

          byteOffset -= bytesConsumed;
          charOffset -= rune.Utf16SequenceLength;
        }
      }
      else
      {
        var byteOffset = 0;
        var charOffset = 0;

        while (Rune.DecodeFromUtf8(_data.AsSpan(byteOffset), out var rune, out var bytesConsumed) is OperationStatus.Done)
        {
          if (charOffset >= offset)
          {
            rune.EncodeToUtf16(buffer);
            return buffer[charOffset - offset];
          }

          byteOffset += bytesConsumed;
          charOffset += rune.Utf16SequenceLength;
        }
      }

      return '\0';
    }
  }

  public Utf8String this[Range range]
  {
    get
    {
      var (offset, length) = range.GetOffsetAndLength(Length);

      return Substring(offset, length);
    }
  }

  public static Utf8String Create<TState>(int length, TState state, SpanAction<byte, TState> action)
  {
    var buffer = new byte[length];

    action(buffer, state);

    return new Utf8String(buffer);
  }

  #region Concat

  public static Utf8String Concat(Utf8String str0, Utf8String str1)
  {
    var length = str0.ByteLength + str1.ByteLength;
    var buffer = new byte[length];

    str0.CopyTo(buffer);
    str1.CopyTo(buffer.AsSpan(str0.ByteLength));

    return new Utf8String(buffer, str0.Length + str1.Length);
  }

  public static Utf8String Concat(Utf8String str0, Utf8String str1, Utf8String str2)
  {
    var length = str0.ByteLength + str1.ByteLength + str2.ByteLength;
    var buffer = new byte[length];

    str0.CopyTo(buffer.AsSpan());
    str1.CopyTo(buffer.AsSpan(str0.ByteLength));
    str2.CopyTo(buffer.AsSpan(str0.ByteLength + str1.ByteLength));

    return new Utf8String(buffer, str0.Length + str1.Length + str2.Length);
  }

  public static Utf8String Concat(Utf8String str0, Utf8String str1, Utf8String str2, Utf8String str3)
  {
    var length = str0.ByteLength + str1.ByteLength + str2.ByteLength + str3.ByteLength;
    var buffer = new byte[length];

    str0.CopyTo(buffer.AsSpan());
    str1.CopyTo(buffer.AsSpan(str0.ByteLength));
    str2.CopyTo(buffer.AsSpan(str0.ByteLength + str1.ByteLength));
    str3.CopyTo(buffer.AsSpan(str0.ByteLength + str1.ByteLength + str2.ByteLength));

    return new Utf8String(buffer, str0.Length + str1.Length + str2.Length + str3.Length);
  }

  public static Utf8String Concat(params Utf8String[] items)
  {
    var length = 0;
    var byteLength = 0;

    foreach (var item in items)
    {
      length += item.Length;
      byteLength += item.ByteLength;
    }

    var buffer = new byte[byteLength];
    var offset = 0;

    foreach (var str in items)
    {
      str.CopyTo(buffer.AsSpan(offset));
      offset += str.ByteLength;
    }

    return new Utf8String(buffer, length);
  }

  #endregion

  #region Trimming

  public Utf8String Trim()
  {
    var startOffset = 0;
    var byteLength = Length;
    var length = Length;

    while (Rune.DecodeFromUtf8(_data.AsSpan(startOffset), out var rune, out var bytesConsumed) is OperationStatus.Done && Rune.IsWhiteSpace(rune))
    {
      startOffset += bytesConsumed;
      length--;
    }

    while (Rune.DecodeLastFromUtf8(_data.AsSpan(0, byteLength), out var rune, out var bytesConsumed) is OperationStatus.Done && Rune.IsWhiteSpace(rune))
    {
      byteLength -= bytesConsumed;
      length--;
    }

    return new Utf8String(_data[startOffset..byteLength], length);
  }

  public Utf8String TrimStart()
  {
    var startOffset = 0;
    var byteLength = Length;
    var length = Length;

    while (Rune.DecodeFromUtf8(_data.AsSpan(startOffset), out var rune, out var bytesConsumed) is OperationStatus.Done && Rune.IsWhiteSpace(rune))
    {
      startOffset += bytesConsumed;
      length--;
    }

    return new Utf8String(_data[startOffset..byteLength], length);
  }

  public Utf8String TrimEnd()
  {
    var byteLength = Length;
    var length = Length;

    while (Rune.DecodeLastFromUtf8(_data.AsSpan(0, byteLength), out var rune, out var bytesConsumed) is OperationStatus.Done && Rune.IsWhiteSpace(rune))
    {
      byteLength -= bytesConsumed;
      length--;
    }

    return new Utf8String(_data[..byteLength], length);
  }

  #endregion

  #region Remove

  public Utf8String Remove(int startIndex)
  {
    var endIndex = 0;

    for (var i = 0; i < startIndex; i++)
    {
      var count = TryGetCount(_data, endIndex);

      if (count != -1)
      {
        endIndex += count;
      }
    }

    return new Utf8String(_data[..endIndex], startIndex);
  }

  public Utf8String Remove(int startIndex, int count)
  {
    using var builder = new ArrayPoolList<char>(Length - count);
    using var buffer = new Buffer<char>(Length);

    var chars = CopyTo(buffer);

    buffer[..startIndex].CopyTo(builder.AppendSpan(startIndex));
    buffer[(startIndex + count)..].CopyTo(builder.AppendSpan(Length - startIndex - count));

    return new Utf8String(builder.AsSpan());
  }

  #endregion

  #region Substring

  public Utf8String Substring(int startIndex)
  {
    var byteStartOffset = 0;

    for (var i = 0; i < startIndex; i++)
    {
      var result = TryGetCount(_data, byteStartOffset);

      if (result != -1)
      {
        byteStartOffset += result;
      }
    }

    return new Utf8String(_data[byteStartOffset..], Length - startIndex);
  }

  public Utf8String Substring(int startIndex, int length)
  {
    var byteStartOffset = 0;

    for (var i = 0; i < startIndex; i++)
    {
      var result = TryGetCount(_data, byteStartOffset);

      if (result != -1)
      {
        byteStartOffset += result;
      }
    }

    var byteEndOffset = byteStartOffset;

    for (var i = 0; i < length; i++)
    {
      var result = TryGetCount(_data, byteEndOffset);

      if (result != -1)
      {
        byteEndOffset += result;
      }
    }

    return new Utf8String(_data[byteStartOffset..(byteEndOffset - byteStartOffset)], length);
  }

  #endregion

  #region Format

  public static bool TryFormat(bool value, out Utf8String result)
  {
    Span<byte> buffer = stackalloc byte[5];

    if (Utf8Formatter.TryFormat(value, buffer, out var written))
    {
      result = new Utf8String(buffer[..written].ToArray(), written);
      return true;
    }

    result = Empty;
    return false;
  }

  public static bool TryFormat(DateTime value, out Utf8String result)
  {
    Span<byte> buffer = stackalloc byte[50];

    if (Utf8Formatter.TryFormat(value, buffer, out var written))
    {
      result = new Utf8String(buffer[..written].ToArray(), written);
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

  public static Utf8String Join(char separator, params Utf8String[] strings)
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

    return new Utf8String(builder.ToArray(), length);
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

    foreach (var rune in EnumerateRunes())
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
    foreach (var item in this)
    {
      if (item == value)
        return true;
    }

    return false;
  }

  public int IndexOf(char value)
  {
    var index = 0;

    foreach (var item in this)
    {
      if (item == value)
        return index;

      index++;
    }

    return -1;
  }

  public int IndexOf(char value, int startIndex)
  {
    var index = 0;

    foreach (var item in this)
    {
      if (index >= startIndex && item == value)
        return index;

      index++;
    }

    return -1;
  }

  public int IndexOf(char value, int startIndex, int count)
  {
    var index = 0;

    foreach (var item in this)
    {
      if (index >= startIndex && index < startIndex + count && item == value)
        return index;

      index++;
    }

    return -1;
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
    var index = 0;

    foreach (var item in this)
    {
      if (anyOf.Contains(item))
        return index;

      index++;
    }

    return -1;
  }

  public static Utf8String[] Split(char separator, Utf8String items)
  {
    return items.Split(separator);
  }

  public Utf8String[] Split(char separator)
  {
    var characters = GetChars(_data, stackalloc char[Length]);

    var data = new Utf8String[characters.Count(separator)];
    var index = 0;

    foreach (var element in characters.Tokenize(separator))
    {
      data[index++] = new Utf8String(element);
    }

    return data;
  }

  public int CopyTo(Span<char> span)
  {
    Utf8.ToUtf16(_data, span, out _, out var characters, false, false);

    return characters;
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

  public IEnumerable<Rune> EnumerateRunes()
  {
    var byteCount = 0;

    while (Rune.DecodeFromUtf8(_data.AsSpan(byteCount), out var rune, out var bytesConsumed) is OperationStatus.Done)
    {
      yield return rune;

      byteCount += bytesConsumed;
    }
  }

  public IEnumerable<Rune> EnumerateRunesReversed()
  {
    var byteCount = ByteLength;

    while (Rune.DecodeLastFromUtf8(_data.AsSpan(0, byteCount), out var rune, out var bytesConsumed) is OperationStatus.Done)
    {
      yield return rune;

      byteCount -= bytesConsumed;
    }
  }

  public IEnumerator<char> GetEnumerator()
  {
    var buffer = new char[2];

    foreach (var rune in EnumerateRunes())
    {
      var length = rune.EncodeToUtf16(buffer);

      for (int i = 0; i < length; i++)
      {
        yield return buffer[i];
      }
    }
  }

  IEnumerator IEnumerable.GetEnumerator()
  {
    return GetEnumerator();
  }

  public bool Equals(Utf8String x, Utf8String y)
  {
    return x == y;
  }

  public static bool operator ==(Utf8String x, Utf8String y)
  {
    return x.Length == y.Length && x.AsBytes().SequenceEqual(y.AsBytes());
  }

  public static bool operator !=(Utf8String x, Utf8String y)
  {
    return !(x == y);
  }

  public override bool Equals(object? obj)
  {
    return obj is Utf8String dynamicString && Equals(dynamicString, this);
  }

  private static int TryGetCount(ReadOnlySpan<byte> buffer, int index)
  {
    if (index >= buffer.Length)
      return -1;

    uint x = buffer[index];

    var byteCount =
      x < 192U ? 1 :
      x < 224U ? 2 :
      x < 240U ? 3 :
      4;

    if (index + byteCount > buffer.Length)
      return -1;

    return byteCount;
  }

  private static int Decode(ReadOnlySpan<byte> buffer, int index, out uint codePoint)
  {
    const int InvalidCount = -1;
    const uint EoS = 0xffff_ffff;

    if (index >= buffer.Length)
    {
      codePoint = EoS;
      return InvalidCount;
    }

    uint code = buffer[index];

    switch (code)
    {
      case < 0b1100_0000:
        // ASCII 文字
        codePoint = code;
        return 1;
      // 2バイト文字
      case < 0b1110_0000 when index + 1 >= buffer.Length:
        codePoint = EoS;
        return InvalidCount;
      case < 0b1110_0000:
        code &= 0b1_1111;
        code = (code << 6) | (uint)(buffer[++index] & 0b0011_1111);
        codePoint = code;
        return 2;
      // 3バイト文字
      case < 0b1111_0000 when index + 2 >= buffer.Length:
        codePoint = EoS;
        return InvalidCount;
      case < 0b1111_0000:
        code &= 0b1111;
        code = (code << 6) | (uint)(buffer[++index] & 0b0011_1111);
        code = (code << 6) | (uint)(buffer[++index] & 0b0011_1111);
        codePoint = code;
        return 3;
    }

    // 4バイト文字
    if (index + 3 >= buffer.Length)
    {
      codePoint = EoS;
      return InvalidCount;
    }

    code &= 0b0111;
    code = (code << 6) | (uint)(buffer[++index] & 0b0011_1111);
    code = (code << 6) | (uint)(buffer[++index] & 0b0011_1111);
    code = (code << 6) | (uint)(buffer[++index] & 0b0011_1111);
    codePoint = code;
    return 4;
  }
}