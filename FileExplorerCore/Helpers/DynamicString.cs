using System;
using System.Buffers;
using System.Buffers.Text;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Unicode;
using Microsoft.Toolkit.HighPerformance;

namespace FileExplorerCore.Helpers;

/// <summary>
/// A string that uses the best encoding
/// </summary>
public readonly struct DynamicString : IEnumerable<char>, IEqualityComparer<DynamicString>
{
  private readonly byte[] _data;

  private readonly int _length;

  public int Length => _length;

  private int ByteLength => _data.Length;

  public static DynamicString Empty = new(ReadOnlySpan<byte>.Empty, 0);

  public DynamicString(ReadOnlySpan<char> data)
  {
    if (data.IsEmpty)
    {
      _data = Array.Empty<byte>();
      _length = 0;

      return;
    }

    using var buffer = new Buffer<byte>(data.Length * 2);

    Utf8.FromUtf16(data, buffer, out _length, out var bytes, false, false);

    _data = buffer[..bytes].ToArray();
  }

  private DynamicString(ReadOnlySpan<byte> data, int length)
  {
    _data = data.ToArray();
    _length = length;
  }

  public char this[Index index]
  {
    get
    {
      var offset = index.GetOffset(Length);

      if (offset < 0 || offset >= Length)
      {
        throw new ArgumentOutOfRangeException(nameof(index));
      }

      using var buffer = new Buffer<char>(offset + 1);

      var span = GetChars(_data, buffer);

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

    using var buffer = new Buffer<byte>(length);

    str0.AsBytes().CopyTo(buffer);
    str1.AsBytes().CopyTo(buffer[str1.ByteLength..]);

    return new DynamicString(buffer, str0.Length + str1.Length);
  }

  public static DynamicString Concat(DynamicString str0, DynamicString str1, DynamicString str2)
  {
    var length = str0.ByteLength + str1.ByteLength + str2.ByteLength;

    using var buffer = new Buffer<byte>(length);

    str0.AsBytes().CopyTo(buffer);
    str1.AsBytes().CopyTo(buffer[str0.ByteLength..]);
    str2.AsBytes().CopyTo(buffer[(str0.ByteLength + str1.ByteLength)..]);

    return new DynamicString(buffer, str0.Length + str1.Length + str2.Length);
  }

  public static DynamicString Concat(DynamicString str0, DynamicString str1, DynamicString str2, DynamicString str3)
  {
    var length = str0.ByteLength + str1.ByteLength + str2.ByteLength + str3.ByteLength;

    using var buffer = new Buffer<byte>(length);

    str0.AsBytes().CopyTo(buffer);
    str1.AsBytes().CopyTo(buffer[str0.ByteLength..]);
    str2.AsBytes().CopyTo(buffer[(str0.ByteLength + str1.ByteLength)..]);
    str3.AsBytes().CopyTo(buffer[(str0.ByteLength + str1.ByteLength + str2.ByteLength)..]);

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

    using var builder = new ValueListBuilder<byte>(stackalloc byte[byteLength]);

    for (var i = 0; i < items.Length; i++)
    {
      var str = items[i];

      str.AsBytes().CopyTo(builder.AppendSpan(str.ByteLength));
    }

    return new DynamicString(builder.AsSpan(), length);
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
    using var buffer = new Buffer<char>(Length);

    return new DynamicString(GetChars(_data, buffer)
      .Slice(0, startIndex + 1));
  }

  public DynamicString Remove(int startIndex, int count)
  {
    using var builder = new ValueListBuilder<char>(Length - count);
    using var buffer = new Buffer<char>(Length);

    var chars = GetChars(_data, buffer);

    chars.Slice(0, startIndex).CopyTo(builder.AppendSpan(startIndex));
    chars.Slice(startIndex + count).CopyTo(builder.AppendSpan(Length - startIndex - count));

    return new DynamicString(builder.AsSpan());
  }

  #endregion

  #region Substring

  public DynamicString Substring(int startIndex)
  {
    using var buffer = new Buffer<char>(Length);

    var span = GetChars(_data, buffer);

    return new DynamicString(span[startIndex..]);
  }

  public DynamicString Substring(int startIndex, int length)
  {
    using var buffer = new Buffer<char>(Length);

    var span = GetChars(_data, buffer);

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
    var length = strings.Length - 1;
    var byteLength = 0;

    foreach (var item in strings)
    {
      length += item.Length;
      byteLength += item.ByteLength;
    }

    using var builder = new ValueListBuilder<byte>(byteLength);

    for (var i = 0; i < strings.Length; i++)
    {
      var str = strings[i];

      str._data.CopyTo(builder.AppendSpan(str.ByteLength));

      if (i < strings.Length - 1)
      {
        builder.Append((byte)separator);
      }
    }

    return new DynamicString(builder.AsSpan(), length);
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

    var chars = GetChars(_data, stackalloc char[Length]);

    for (var i = 0; i < chars.Length; i++)
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
    var span = GetBytes(stackalloc char[] { value }, stackalloc byte[3]);

    return _data.AsSpan().IndexOf(span) != -1;
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

  public void CopyToSpan(Span<char> span)
  {
    if (!span.IsEmpty && ByteLength > 0)
    {
      GetChars(_data, span);
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    return x.ByteLength == y.Length && x._data.AsSpan().SequenceEqual(y._data);
  }

  int IEqualityComparer<DynamicString>.GetHashCode(DynamicString obj)
  {
    return obj.GetHashCode();
  }

  public static bool operator ==(DynamicString x, DynamicString y)
  {
    return x.ByteLength == y.Length && x._data.AsSpan().SequenceEqual(y._data);
  }

  public static bool operator !=(DynamicString x, DynamicString y)
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

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private static ReadOnlySpan<byte> GetBytes(ReadOnlySpan<char> data, Span<byte> buffer)
  {
    Utf8.FromUtf16(data, buffer, out _, out var bytesWritten, false, false);

    if (bytesWritten < buffer.Length)
    {
      return buffer[..bytesWritten];
    }

    return ReadOnlySpan<byte>.Empty;
  }

  public override bool Equals(object? obj)
  {
    return obj is DynamicString dynamicString && Equals(dynamicString, this);
  }
}