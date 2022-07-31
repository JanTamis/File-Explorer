//using System;
//using System.Buffers;
//using System.Buffers.Text;
//using System.Collections;
//using System.Collections.Generic;
//using System.Globalization;
//using System.IO;
//using System.Linq;
//using System.Numerics;
//using System.Runtime.CompilerServices;
//using System.Runtime.InteropServices;
//using System.Text;
//using System.Text.RegularExpressions;
//using System.Text.Unicode;
//using Microsoft.Toolkit.HighPerformance.Buffers;

//namespace FileExplorer.Helpers;

///// <summary>
///// A string that uses the UTF-8 encoding
///// </summary>
//public struct Utf8String : IEnumerable<char>,
//  IComparer<Utf8String>,
//  IComparable<Utf8String>,
//  IEqualityOperators<Utf8String, Utf8String>,
//  IAdditionOperators<Utf8String, Utf8String, Utf8String>
//{
//  private static readonly Dictionary<int, Utf8String> InternPool = new();

//  private readonly byte[] _data;

//  private int _length = -1;

//  /// <summary>
//  /// The character length of this string
//  /// </summary>
//  public int Length
//  {
//    get
//    {
//      if (_length < 0)
//      {
//        _length = Encoding.UTF8.GetCharCount(_data);
//      }

//      return _length;
//    }
//  }

//  private int ByteLength => _data.Length;

//  /// <summary>
//  /// A empty Utf8String
//  /// </summary>
//  public static readonly Utf8String Empty = new(Array.Empty<byte>(), 0);

//  /// <summary>
//  /// Creates	a new Utf8String from a set of characters
//  /// </summary>
//  /// <param name="chars">the characters to create the string from</param>
//  public Utf8String(ReadOnlySpan<char> chars)
//  {
//    if (chars.IsEmpty)
//    {
//      _data = Array.Empty<byte>();
//      _length = 0;

//      return;
//    }

//    if (InternPool.Any())
//    {
//      var hash = GetHashCode(ref MemoryMarshal.GetReference(MemoryMarshal.AsBytes(chars)), chars.Length * sizeof(char));

//      if (InternPool.TryGetValue(hash, out var poolItem))
//      {
//        this = poolItem;
//        return;
//      }
//    }

//    _data = new byte[Encoding.UTF8.GetByteCount(chars)];

//    Utf8.FromUtf16(chars, _data, out _length, out _);
//  }

//  /// <summary>
//  /// Creates a Utf8String from a UTF-8 file
//  /// </summary>
//  /// <param name="filepath">the path to the file</param>
//  /// <returns>the Utf8String from the file</returns>
//  public static Utf8String FromFile(string filepath)
//  {
//    using var stream = File.OpenRead(filepath);

//    return FromStream(stream);
//  }

//  /// <summary>
//  /// Returns a Utf8String from a UTF-8 encoded stream
//  /// </summary>
//  /// <param name="data">the data to create the string from</param>
//  /// <returns>the string from the stream</returns>
//  /// <exception cref="ArgumentException">if no data can be read from the stream</exception>
//  public static Utf8String FromStream(Stream data)
//  {
//    if (data.CanRead)
//    {
//      if (data.CanSeek)
//      {
//        var buffer = new byte[data.Length];
//        data.Read(buffer);

//        return new Utf8String(buffer);
//      }
//      else
//      {
//        using var buffer = new ArrayPoolList<byte>(1024);

//        Span<byte> span = stackalloc byte[1024];

//        while (data.Read(span) is var read && read is not 0)
//        {
//          buffer.AppendSpan(span[..read]);
//        }

//        return new Utf8String(buffer.ToArray());
//      }
//    }

//    throw new ArgumentException("can't read data from the stream", nameof(data));
//  }

//  private Utf8String(byte[] data, int length = -1)
//  {
//    _data = data;
//    _length = length;
//  }

//  private Utf8String(ReadOnlySpan<byte> data, int length = -1) : this(data.ToArray(), length)
//  {
//  }

//  /// <summary>
//  /// returns the character from a specific index
//  /// </summary>
//  /// <param name="index">the index of the character</param>
//  /// <exception cref="IndexOutOfRangeException">if the index was out of the bounds</exception>
//  [IndexerName("Chars")]
//  public char this[Index index]
//  {
//    get
//    {
//      var offset = index.GetOffset(Length);

//      if (offset >= Length)
//      {
//        throw new IndexOutOfRangeException(nameof(index));
//      }

//      Span<char> buffer = stackalloc char[2];

//      if (index.IsFromEnd)
//      {
//        var byteOffset = ByteLength;
//        var charOffset = Length - 1;

//        while (Rune.DecodeLastFromUtf8(_data.AsSpan(0, byteOffset), out var rune, out var bytesConsumed) is OperationStatus.Done)
//        {
//          if (charOffset <= offset)
//          {
//            rune.EncodeToUtf16(buffer);
//            return buffer[charOffset - offset];
//          }

//          byteOffset -= bytesConsumed;
//          charOffset -= rune.Utf16SequenceLength;
//        }
//      }
//      else
//      {
//        var byteOffset = 0;
//        var charOffset = 0;

//        while (Rune.DecodeFromUtf8(_data.AsSpan(byteOffset), out var rune, out var bytesConsumed) is OperationStatus.Done)
//        {
//          if (charOffset >= offset)
//          {
//            rune.EncodeToUtf16(buffer);
//            return buffer[charOffset - offset];
//          }

//          byteOffset += bytesConsumed;
//          charOffset += rune.Utf16SequenceLength;
//        }
//      }

//      return '\0';
//    }
//  }

//  /// <summary>
//  /// Returns a subset of the string specified by the range
//  /// </summary>
//  /// <param name="range">the range of the subset</param>
//  [IndexerName("Chars")]
//  public Utf8String this[Range range]
//  {
//    get
//    {
//      var (offset, length) = range.GetOffsetAndLength(Length);

//      return Substring(offset, length);
//    }
//  }

//  /// <summary>
//  /// Creates a new string with a specific length (in bytes) and initializes it after creation by using the specified callback  
//  /// </summary>
//  /// <param name="length">the length of the specified Utf8String (in bytes)</param>
//  /// <param name="state">the state to propagate to the action</param>
//  /// <param name="action">the action that will be invoked while making the string</param>
//  /// <typeparam name="TState">The type of the element to pass to <param name="action"></param></typeparam>
//  /// <returns>a immutable Utf8String</returns>
//  public static Utf8String Create<TState>(int length, TState state, SpanAction<byte, TState> action)
//  {
//    var buffer = new byte[length];

//    action(buffer, state);

//    return new Utf8String(buffer);
//  }

//  /// <summary>
//  /// Creates a new string with a specific length and initializes it after creation by using the specified callback  
//  /// </summary>
//  /// <param name="length">the length of the specified Utf8String</param>
//  /// <param name="state">the state to propagate to the action</param>
//  /// <param name="action">the action that will be invoked while making the string</param>
//  /// <typeparam name="TState">The type of the element to pass to <param name="action"></param></typeparam>
//  /// <returns>a immutable Utf8String</returns>
//  public static Utf8String Create<TState>(int length, TState state, SpanAction<char, TState> action)
//  {
//    using var buffer = SpanOwner<char>.Allocate(length);

//    action(buffer.Span[..length], state);

//    return new Utf8String(buffer.Span[..length]);
//  }

//  /// <summary>
//  /// Creates a string from String Interpolation
//  /// </summary>
//  /// <remarks>See https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/tokens/interpolated for more information</remarks>
//  /// <param name="handler">the interpolated string handler</param>
//  public static Utf8String Create(ref Utf8InterpolatedStringHandler handler)
//  {
//    return new Utf8String(handler.ToArrayAndClear());
//  }

//  /// <summary>
//  /// Creates a string from String Interpolation
//  /// </summary>
//  /// <remarks>See https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/tokens/interpolated for more information</remarks>
//  /// <param name="buffer">the temporary buffer the use while creating the string</param>
//  /// <param name="handler">the interpolated string handler</param>
//  public static Utf8String Create(Span<byte> buffer, [InterpolatedStringHandlerArgument("buffer")] ref Utf8InterpolatedStringHandler handler)
//  {
//    return new Utf8String(handler.ToArrayAndClear());
//  }

//  public static Utf8String EncodeFromBase64(ReadOnlySpan<byte> bytes)
//  {
//    var buffer = new byte[Base64.GetMaxEncodedToUtf8Length(bytes.Length)];
//    Base64.EncodeToUtf8(bytes, buffer, out _, out _);

//    return new Utf8String(buffer);
//  }

//  #region Concat

//  /// <summary>
//  /// Combines two strings
//  /// </summary>
//  /// <param name="str0">the first string</param>
//  /// <param name="str1">the second string</param>
//  /// <returns>the combined string</returns>
//  public static Utf8String Concat(Utf8String str0, Utf8String str1)
//  {
//    var length = str0.ByteLength + str1.ByteLength;
//    var buffer = new byte[length];

//    str0.CopyTo(buffer);
//    str1.CopyTo(buffer.AsSpan(str0.ByteLength));

//    return new Utf8String(buffer, str0.Length + str1.Length);
//  }

//  /// <summary>
//  /// Combines three strings
//  /// </summary>
//  /// <param name="str0">the first string</param>
//  /// <param name="str1">the second string</param>
//  /// <param name="str2">the third string</param>
//  /// <returns>the combined string</returns>
//  public static Utf8String Concat(Utf8String str0, Utf8String str1, Utf8String str2)
//  {
//    var length = str0.ByteLength + str1.ByteLength + str2.ByteLength;
//    var buffer = new byte[length];

//    str0.CopyTo(buffer.AsSpan());
//    str1.CopyTo(buffer.AsSpan(str0.ByteLength));
//    str2.CopyTo(buffer.AsSpan(str0.ByteLength + str1.ByteLength));

//    return new Utf8String(buffer, str0.Length + str1.Length + str2.Length);
//  }

//  /// <summary>
//  /// Combines four strings
//  /// </summary>
//  /// <param name="str0">the first String</param>
//  /// <param name="str1">the second String</param>
//  /// <param name="str2">the third String</param>
//  /// <param name="str3">the fourth String</param>
//  /// <returns></returns>
//  public static Utf8String Concat(Utf8String str0, Utf8String str1, Utf8String str2, Utf8String str3)
//  {
//    var length = str0.ByteLength + str1.ByteLength + str2.ByteLength + str3.ByteLength;
//    var buffer = new byte[length];

//    str0.CopyTo(buffer.AsSpan());
//    str1.CopyTo(buffer.AsSpan(str0.ByteLength));
//    str2.CopyTo(buffer.AsSpan(str0.ByteLength + str1.ByteLength));
//    str3.CopyTo(buffer.AsSpan(str0.ByteLength + str1.ByteLength + str2.ByteLength));

//    return new Utf8String(buffer, str0.Length + str1.Length + str2.Length + str3.Length);
//  }

//  /// <summary>
//  /// Combines a array of strings
//  /// </summary>
//  /// <param name="strings">the array of strings to combine</param>
//  /// <returns>the combined string</returns>
//  public static Utf8String Concat(params Utf8String[] strings)
//  {
//    var length = 0;
//    var byteLength = 0;

//    foreach (var item in strings)
//    {
//      length += item.Length;
//      byteLength += item.ByteLength;
//    }

//    var buffer = new byte[byteLength];
//    var offset = 0;

//    foreach (var str in strings)
//    {
//      str.CopyTo(buffer.AsSpan(offset));
//      offset += str.ByteLength;
//    }

//    return new Utf8String(buffer, length);
//  }

//  /// <summary>
//  /// Combines a IEnumerable of strings
//  /// </summary>
//  /// <param name="strings"></param>
//  /// <returns></returns>
//  public static Utf8String Concat(IEnumerable<Utf8String> strings)
//  {
//    using var builder = new ValueBuilder<byte>(stackalloc byte[1024]);

//    foreach (var str in strings)
//    {
//      builder.AppendSpan(str.AsBytes());
//    }

//    return new Utf8String(builder.AsSpan());
//  }

//  #endregion

//  #region Trimming

//  /// <summary>
//  /// Removes the whitespace from the beginning and the end of the string
//  /// </summary>
//  /// <returns>the trimmed string</returns>
//  public Utf8String Trim()
//  {
//    var startOffset = 0;
//    var byteLength = ByteLength;
//    var length = _length;

//    while (Rune.DecodeFromUtf8(_data.AsSpan(startOffset), out var rune, out var bytesConsumed) is OperationStatus.Done && Rune.IsWhiteSpace(rune))
//    {
//      startOffset += bytesConsumed;
//      length--;
//    }

//    while (Rune.DecodeLastFromUtf8(_data.AsSpan(0, byteLength), out var rune, out var bytesConsumed) is OperationStatus.Done && Rune.IsWhiteSpace(rune))
//    {
//      byteLength -= bytesConsumed;
//      length--;
//    }

//    return new Utf8String(_data[startOffset..byteLength], length);
//  }

//  /// <summary>
//  /// Removes the whitespace of the beginning of the string
//  /// </summary>
//  /// <returns>the trimmed string</returns>
//  public Utf8String TrimStart()
//  {
//    var startOffset = 0;
//    var byteLength = ByteLength;
//    var length = Length;

//    while (Rune.DecodeFromUtf8(_data.AsSpan(startOffset), out var rune, out var bytesConsumed) is OperationStatus.Done && Rune.IsWhiteSpace(rune))
//    {
//      startOffset += bytesConsumed;
//      length--;
//    }

//    return new Utf8String(_data[startOffset..byteLength], length);
//  }

//  /// <summary>
//  /// Removes the whitespace of the end of the string
//  /// </summary>
//  /// <returns>the trimmed string</returns>
//  public Utf8String TrimEnd()
//  {
//    var byteLength = ByteLength;
//    var length = Length;

//    while (Rune.DecodeLastFromUtf8(_data.AsSpan(0, byteLength), out var rune, out var bytesConsumed) is OperationStatus.Done && Rune.IsWhiteSpace(rune))
//    {
//      byteLength -= bytesConsumed;
//      length--;
//    }

//    return new Utf8String(_data[..byteLength], length);
//  }

//  #endregion

//  #region Remove

//  /// <summary>
//  /// Returns a new string in which a specified number of characters in the current instance beginning at a specified position have been deleted.
//  /// </summary>
//  /// <param name="startIndex">the zero-based position to begin deleting characters</param>
//  /// <returns>a new string that is equivalent to this instance except for the removed characters</returns>
//  public Utf8String Remove(int startIndex)
//  {
//    return Substring(0, startIndex);
//  }

//  /// <summary>
//  /// Returns a new string in which a specified number of characters in the current instance beginning at a specified position have been deleted
//  /// </summary>
//  /// <param name="startIndex">the zero-based position to begin deleting characters</param>
//  /// <param name="count">the number of characters to delete</param>
//  /// <returns>a new string that is equivalent to this instance except for the removed characters</returns>
//  public Utf8String Remove(int startIndex, int count)
//  {
//    var byteStartOffset = 0;

//    for (var i = 0; i < startIndex; i++)
//    {
//      var result = TryGetCount(_data, byteStartOffset);

//      if (result != -1)
//      {
//        byteStartOffset += result;
//      }
//    }

//    var byteEndOffset = byteStartOffset;

//    for (var i = 0; i < count; i++)
//    {
//      var result = TryGetCount(_data, byteEndOffset);

//      if (result != -1)
//      {
//        byteEndOffset += result;
//      }
//    }

//    var buffer = new byte[Length - (byteEndOffset - byteStartOffset)];

//    AsBytes().Slice(0, byteStartOffset).CopyTo(buffer);
//    AsBytes().Slice(byteEndOffset).CopyTo(buffer.AsSpan(byteStartOffset));

//    return new Utf8String(buffer);
//  }

//  #endregion

//  #region Substring

//  /// <summary>
//  /// Returns a part of the string from the specified index to the end of the string
//  /// </summary>
//  /// <param name="startIndex">the start of the substring</param>
//  /// <returns>the substring</returns>
//  public Utf8String Substring(int startIndex)
//  {
//    var byteStartOffset = 0;

//    for (var i = 0; i < startIndex; i++)
//    {
//      var result = TryGetCount(_data, byteStartOffset);

//      if (result != -1)
//      {
//        byteStartOffset += result;
//      }
//    }

//    return new Utf8String(_data[byteStartOffset..], Length - startIndex);
//  }

//  /// <summary>
//  /// Returns a part of the string from the specified index with the specified length
//  /// </summary>
//  /// <param name="startIndex">the start of the substring</param>
//  /// <param name="length">the length of the substring</param>
//  /// <returns>the substring</returns>
//  public Utf8String Substring(int startIndex, int length)
//  {
//    var byteStartOffset = 0;

//    for (var i = 0; i < startIndex; i++)
//    {
//      var result = TryGetCount(_data, byteStartOffset);

//      if (result != -1)
//      {
//        byteStartOffset += result;
//      }
//    }

//    var byteEndOffset = byteStartOffset;

//    for (var i = 0; i < length; i++)
//    {
//      var result = TryGetCount(_data, byteEndOffset);

//      if (result != -1)
//      {
//        byteEndOffset += result;
//      }
//    }

//    return new Utf8String(_data[byteStartOffset..(byteEndOffset - byteStartOffset)], length);
//  }

//  #endregion

//  #region Format

//  /// <summary>
//  /// Tries to format the specified value
//  /// </summary>
//  /// <param name="value">the value to try to format</param>
//  /// /// <param name="format">the format for the value</param>
//  /// <param name="result">the formatted string</param>
//  /// <typeparam name="T">the type of the <see cref="value"/></typeparam>
//  /// <returns>if the value was successfully formatted</returns>
//  public static bool TryFormat<T>(T value, ReadOnlySpan<char> format, out Utf8String result) where T : struct
//  {
//    Span<byte> buffer = stackalloc byte[128];

//    var written = 0;

//    if (StandardFormat.TryParse(format, out var standardFormat) && (typeof(T) == typeof(DateTime) && Utf8Formatter.TryFormat((DateTime)(object)value, buffer, out written, standardFormat) ||
//                                                                    typeof(T) == typeof(DateTimeOffset) && Utf8Formatter.TryFormat((DateTimeOffset)(object)value, buffer, out written, standardFormat) ||
//                                                                    typeof(T) == typeof(Guid) && Utf8Formatter.TryFormat((Guid)(object)value, buffer, out written, standardFormat) ||
//                                                                    typeof(T) == typeof(TimeSpan) && Utf8Formatter.TryFormat((TimeSpan)(object)value, buffer, out written, standardFormat) ||
//                                                                    typeof(T) == typeof(bool) && Utf8Formatter.TryFormat((bool)(object)value, buffer, out written, standardFormat) ||
//                                                                    typeof(T) == typeof(byte) && Utf8Formatter.TryFormat((byte)(object)value, buffer, out written, standardFormat) ||
//                                                                    typeof(T) == typeof(decimal) && Utf8Formatter.TryFormat((decimal)(object)value, buffer, out written, standardFormat) ||
//                                                                    typeof(T) == typeof(double) && Utf8Formatter.TryFormat((double)(object)value, buffer, out written, standardFormat) ||
//                                                                    typeof(T) == typeof(float) && Utf8Formatter.TryFormat((float)(object)value, buffer, out written, standardFormat) ||
//                                                                    typeof(T) == typeof(int) && Utf8Formatter.TryFormat((int)(object)value, buffer, out written, standardFormat) ||
//                                                                    typeof(T) == typeof(long) && Utf8Formatter.TryFormat((long)(object)value, buffer, out written, standardFormat) ||
//                                                                    typeof(T) == typeof(sbyte) && Utf8Formatter.TryFormat((sbyte)(object)value, buffer, out written, standardFormat) ||
//                                                                    typeof(T) == typeof(short) && Utf8Formatter.TryFormat((short)(object)value, buffer, out written, standardFormat) ||
//                                                                    typeof(T) == typeof(uint) && Utf8Formatter.TryFormat((uint)(object)value, buffer, out written, standardFormat) ||
//                                                                    typeof(T) == typeof(ulong) && Utf8Formatter.TryFormat((ulong)(object)value, buffer, out written, standardFormat) ||
//                                                                    typeof(T) == typeof(ushort) && Utf8Formatter.TryFormat((ushort)(object)value, buffer, out written, standardFormat)))
//    {
//      result = new Utf8String(buffer[..written]);
//      return true;
//    }

//    result = Empty;
//    return false;
//  }

//  /// <summary>
//  /// Tries to format the specified value
//  /// </summary>
//  /// <param name="value">the value to try to format</param>
//  /// <param name="format">the format for the value</param>
//  /// <typeparam name="T">the type of the <see cref="value"/></typeparam>
//  /// <returns>the formatted string</returns>
//  /// <exception cref="InvalidOperationException">if the value could not be formatted</exception>
//  public static Utf8String Format<T>(T value, ReadOnlySpan<char> format = default) where T : struct
//  {
//    if (TryFormat(value, format, out var result))
//    {
//      return result;
//    }

//    throw new InvalidOperationException($"Unable to format value of type {typeof(T).Name}");
//  }

//  public bool TryParse<T>(out T value, char format = default) where T : struct
//  {
//    if (typeof(T) == typeof(byte) && Utf8Parser.TryParse(_data, out byte byteValue, out _, format))
//    {
//      value = (T)(object)byteValue;
//      return true;
//    }
//    if (typeof(T) == typeof(bool) && Utf8Parser.TryParse(_data, out bool boolValue, out _, format))
//    {
//      value = (T)(object)boolValue;
//      return true;
//    }
//    if (typeof(T) == typeof(DateTime) && Utf8Parser.TryParse(_data, out DateTime datetimeValue, out _, format))
//    {
//      value = (T)(object)datetimeValue;
//      return true;
//    }
//    if (typeof(T) == typeof(DateTimeOffset) && Utf8Parser.TryParse(_data, out DateTimeOffset datetimeoffsetValue, out _, format))
//    {
//      value = (T)(object)datetimeoffsetValue;
//      return true;
//    }
//    if (typeof(T) == typeof(decimal) && Utf8Parser.TryParse(_data, out decimal decimalValue, out _, format))
//    {
//      value = (T)(object)decimalValue;
//      return true;
//    }
//    if (typeof(T) == typeof(double) && Utf8Parser.TryParse(_data, out double doubleValue, out _, format))
//    {
//      value = (T)(object)doubleValue;
//      return true;
//    }
//    if (typeof(T) == typeof(float) && Utf8Parser.TryParse(_data, out float floatValue, out _, format))
//    {
//      value = (T)(object)floatValue;
//      return true;
//    }
//    if (typeof(T) == typeof(Guid) && Utf8Parser.TryParse(_data, out Guid guidValue, out _, format))
//    {
//      value = (T)(object)guidValue;
//      return true;
//    }
//    if (typeof(T) == typeof(int) && Utf8Parser.TryParse(_data, out int intValue, out _, format))
//    {
//      value = (T)(object)intValue;
//      return true;
//    }
//    if (typeof(T) == typeof(long) && Utf8Parser.TryParse(_data, out long longValue, out _, format))
//    {
//      value = (T)(object)longValue;
//      return true;
//    }
//    if (typeof(T) == typeof(sbyte) && Utf8Parser.TryParse(_data, out sbyte sbyteValue, out _, format))
//    {
//      value = (T)(object)sbyteValue;
//      return true;
//    }
//    if (typeof(T) == typeof(short) && Utf8Parser.TryParse(_data, out short shortValue, out _, format))
//    {
//      value = (T)(object)shortValue;
//      return true;
//    }
//    if (typeof(T) == typeof(TimeSpan) && Utf8Parser.TryParse(_data, out TimeSpan timespanValue, out _, format))
//    {
//      value = (T)(object)timespanValue;
//      return true;
//    }
//    if (typeof(T) == typeof(uint) && Utf8Parser.TryParse(_data, out uint uintValue, out _, format))
//    {
//      value = (T)(object)uintValue;
//      return true;
//    }
//    if (typeof(T) == typeof(ulong) && Utf8Parser.TryParse(_data, out ulong ulongValue, out _, format))
//    {
//      value = (T)(object)ulongValue;
//      return true;
//    }
//    if (typeof(T) == typeof(ushort) && Utf8Parser.TryParse(_data, out ushort ushortValue, out _, format))
//    {
//      value = (T)(object)ushortValue;
//      return true;
//    }

//    value = default;
//    return false;
//  }

//  public T Parse<T>(char format = default) where T : struct
//  {
//    if (TryParse(out T value, format))
//    {
//      return value;
//    }

//    throw new Exception($"Unable to parse {typeof(T).Name}");
//  }

//  public byte[] DecodeToBase64()
//  {
//    var buffer = new byte[Base64.GetMaxDecodedFromUtf8Length(ByteLength)];
//    Base64.DecodeFromUtf8(_data, buffer, out _, out _);

//    return buffer;
//  }

//  #endregion

//  /// <summary>
//  /// Returns a array of the characters of this string
//  /// </summary>
//  /// <returns>a array of the characters of this string</returns>
//  public char[] ToCharArray()
//  {
//    var array = new char[Length];

//    CopyTo(array);

//    return array;
//  }

//  /// <summary>
//  /// Combines a array of strings and insert a character between the strings
//  /// </summary>
//  /// <param name="separator">the character to insert between the strings</param>
//  /// <param name="strings">the strings the combine</param>
//  /// <returns>a string that contains the combination of the strings seperated bij the separator</returns>
//  public static Utf8String Join(char separator, params Utf8String[] strings)
//  {
//    var byteLength = 0;
//    var rune = new Rune(separator);
//    var SeparatorByteLength = rune.Utf8SequenceLength;

//    foreach (var item in strings)
//    {
//      byteLength += item.ByteLength;
//    }

//    byteLength += SeparatorByteLength * (strings.Length - 1);

//    var buffer = new byte[byteLength];
//    var index = 0;

//    for (var i = 0; i < strings.Length; i++)
//    {
//      var str = strings[i];

//      str.CopyTo(buffer.AsSpan(index));
//      index += str.ByteLength;

//      if (i < strings.Length - 1)
//      {
//        rune.EncodeToUtf8(buffer.AsSpan(index));
//        index += SeparatorByteLength;
//      }
//    }

//    return new Utf8String(buffer);
//  }

//  /// <summary>
//  /// Returns if the string is empty
//  /// </summary>
//  /// <returns>if the string is empty</returns>
//  public bool IsEmpty()
//  {
//    return ByteLength is 0;
//  }

//  /// <summary>
//  /// Returns if the string only contains whitespace characters
//  /// </summary>
//  /// <returns>if the string only contains whitespace characters</returns>
//  public bool IsWhitespace()
//  {
//    var byteCount = 0;

//    while (Rune.DecodeFromUtf8(_data.AsSpan(byteCount), out var rune, out var bytesConsumed) is OperationStatus.Done)
//    {
//      if (!Rune.IsWhiteSpace(rune))
//      {
//        return false;
//      }

//      byteCount += bytesConsumed;
//    }

//    return true;
//  }

//  /// <summary>
//  /// Returns if the string if empty or only contains whitespace characters
//  /// </summary>
//  /// <returns>if the string if empty or only contains whitespace characters</returns>
//  public bool IsEmptyOrWhiteSpace()
//  {
//    return IsEmpty() || IsWhitespace();
//  }

//  /// <summary>
//  /// Returns if this string contains the specified character
//  /// </summary>
//  /// <param name="value">the specified character</param>
//  /// <returns>if the string contains the specified character</returns>
//  public bool Contains(char value)
//  {
//    using var buffer = SpanOwner<char>.Allocate(Length);
//    CopyTo(buffer.Span);

//    return buffer.Span.Contains(value);
//  }

//  public int IndexOf(char value)
//  {
//    using var buffer = SpanOwner<char>.Allocate(Length);
//    CopyTo(buffer.Span);

//    return buffer.Span.IndexOf(value);
//  }

//  public int IndexOf(char value, int startIndex)
//  {
//    using var buffer = SpanOwner<char>.Allocate(Length);
//    CopyTo(buffer.Span);

//    return buffer.Span[startIndex..].IndexOf(value);
//  }

//  public int IndexOf(char value, int startIndex, int count)
//  {
//    using var buffer = SpanOwner<char>.Allocate(Length);
//    CopyTo(buffer.Span);

//    return buffer.Span.Slice(startIndex, count).IndexOf(value);
//  }

//  public int LastIndexOf(char value)
//  {
//    using var buffer = SpanOwner<char>.Allocate(Length);
//    CopyTo(buffer.Span);

//    return buffer.Span.IndexOf(value);
//  }

//  public int LastIndexOf(char value, int startIndex)
//  {
//    using var buffer = SpanOwner<char>.Allocate(Length);
//    CopyTo(buffer.Span);

//    return buffer.Span.Slice(startIndex).LastIndexOf(value);
//  }

//  public int LastIndexOf(char value, int startIndex, int count)
//  {
//    using var buffer = SpanOwner<char>.Allocate(Length);
//    CopyTo(buffer.Span);

//    return buffer.Span.Slice(startIndex, count).LastIndexOf(value);
//  }

//  public int IndexOfAny(ReadOnlySpan<char> anyOf)
//  {
//    using var buffer = SpanOwner<char>.Allocate(Length);
//    CopyTo(buffer.Span);

//    return buffer.Span.IndexOfAny(anyOf);
//  }

//  public Utf8String Replace(char oldChar, char newChar)
//  {
//    if (oldChar == newChar)
//    {
//      return this;
//    }

//    using var buffer = SpanOwner<char>.Allocate(Length);
//    CopyTo(buffer.Span);

//    var span = buffer.Span;

//    while (span.IndexOf(oldChar) is var index && index != -1)
//    {
//      span[index] = newChar;
//      span = span[index..];
//    }

//    return new Utf8String(buffer.Span);

//  }

//  public bool EndsWith(char value)
//  {
//    Span<byte> buffer = stackalloc byte[4];
//    var rune = new Rune(value);

//    var bytesWritten = rune.EncodeToUtf8(buffer);

//    return AsBytes().EndsWith(buffer[..bytesWritten]);
//  }

//  public bool EndsWith(Utf8String value)
//  {
//    return AsBytes().EndsWith(value.AsBytes());
//  }

//  public ref readonly byte GetPinnableReference()
//  {
//    return ref MemoryMarshal.GetArrayDataReference(_data);
//  }

//  public IEnumerable<Utf8String> Split(char separator, StringSplitOptions options = StringSplitOptions.None)
//  {
//    var byteOffset = 0;
//    var previousOffset = 0;

//    var buffer = new char[2];

//    while (Rune.DecodeFromUtf8(_data.AsSpan(byteOffset), out var rune, out var bytesConsumed) is OperationStatus.Done)
//    {
//      var charCount = rune.EncodeToUtf16(buffer);

//      for (var i = 0; i < charCount; i++)
//      {
//        var charByteCount = Math.Min(bytesConsumed, 2);

//        if (buffer[i] == separator)
//        {
//          var length = byteOffset - previousOffset;

//          if (!options.HasFlag(StringSplitOptions.RemoveEmptyEntries) || length > 0)
//          {
//            yield return options.HasFlag(StringSplitOptions.TrimEntries)
//              ? TrimInternal(_data.AsSpan(previousOffset, length))
//              : new Utf8String(_data.AsSpan(previousOffset, length));
//          }

//          previousOffset = byteOffset + charByteCount;
//        }

//        byteOffset += charByteCount;
//        bytesConsumed -= charByteCount;
//      }
//    }

//    if (!options.HasFlag(StringSplitOptions.RemoveEmptyEntries) || previousOffset < _data.Length)
//    {
//      yield return options.HasFlag(StringSplitOptions.TrimEntries)
//        ? TrimInternal(_data.AsSpan(previousOffset))
//        : new Utf8String(_data.AsSpan(previousOffset));
//    }
//  }

//  public IEnumerable<Utf8String> Split(char[] separators, StringSplitOptions options = StringSplitOptions.None)
//  {
//    var byteOffset = 0;
//    var previousOffset = 0;

//    var buffer = new char[2];

//    while (Rune.DecodeFromUtf8(_data.AsSpan(byteOffset), out var rune, out var bytesConsumed) is OperationStatus.Done)
//    {
//      var charCount = rune.EncodeToUtf16(buffer);

//      for (var i = 0; i < charCount; i++)
//      {
//        var charByteCount = Math.Min(bytesConsumed, 2);

//        if (Array.IndexOf(separators, buffer[i]) > -1)
//        {
//          var length = byteOffset - previousOffset;

//          if (!options.HasFlag(StringSplitOptions.RemoveEmptyEntries) || length > 0)
//          {
//            yield return options.HasFlag(StringSplitOptions.TrimEntries)
//              ? TrimInternal(_data.AsSpan(previousOffset, length))
//              : new Utf8String(_data.AsSpan(previousOffset, length));
//          }

//          previousOffset = byteOffset + charByteCount;
//        }

//        byteOffset += charByteCount;
//        bytesConsumed -= charByteCount;
//      }
//    }

//    if (!options.HasFlag(StringSplitOptions.RemoveEmptyEntries) || previousOffset < _data.Length)
//    {
//      yield return options.HasFlag(StringSplitOptions.TrimEntries)
//        ? TrimInternal(_data.AsSpan(previousOffset))
//        : new Utf8String(_data.AsSpan(previousOffset));
//    }
//  }

//  public Utf8String ToUpper()
//  {
//    var result = new byte[ByteLength];
//    var offset = 0;

//    foreach (var rune in EnumerateRunes())
//    {
//      offset += Rune
//        .ToUpper(rune, CultureInfo.CurrentCulture)
//        .EncodeToUtf8(result.AsSpan(offset..));
//    }

//    return new Utf8String(result, _length);
//  }

//  public Utf8String ToUpperInvariant()
//  {
//    var result = new byte[ByteLength];
//    var offset = 0;

//    foreach (var rune in EnumerateRunes())
//    {
//      offset += Rune
//        .ToUpperInvariant(rune)
//        .EncodeToUtf8(result.AsSpan(offset..));
//    }

//    return new Utf8String(result, _length);
//  }

//  public Utf8String ToLower()
//  {
//    var result = new byte[ByteLength];
//    var offset = 0;

//    foreach (var rune in EnumerateRunes())
//    {
//      offset += Rune
//        .ToLower(rune, CultureInfo.CurrentCulture)
//        .EncodeToUtf8(result.AsSpan(offset..));
//    }

//    return new Utf8String(result, _length);
//  }

//  public Utf8String ToLowerInvariant()
//  {
//    var result = new byte[ByteLength];
//    var offset = 0;

//    foreach (var rune in EnumerateRunes())
//    {
//      offset += Rune
//        .ToLowerInvariant(rune)
//        .EncodeToUtf8(result.AsSpan(offset..));
//    }

//    return new Utf8String(result, _length);
//  }

//  public bool IsInterned()
//  {
//    if (!InternPool.Any())
//    {
//      return false;
//    }

//    using var buffer = SpanOwner<char>.Allocate(Length);
//    CopyTo(buffer.Span);

//    var hash = GetHashCode(ref MemoryMarshal.GetReference(MemoryMarshal.AsBytes(buffer.Span)), buffer.Length * sizeof(char));

//    return InternPool.ContainsKey(hash);
//  }

//  public Utf8String Intern()
//  {
//    using var buffer = SpanOwner<char>.Allocate(Length);
//    CopyTo(buffer.Span);

//    var hash = GetHashCode(ref MemoryMarshal.GetReference(MemoryMarshal.AsBytes(buffer.Span)), buffer.Length * sizeof(char));

//    if (!InternPool.ContainsKey(hash))
//    {
//      InternPool.Add(hash, this);
//    }

//    return this;
//  }

//  public Utf8String PadLeft(int totalWidth, char paddingChar = ' ')
//  {
//    if (totalWidth <= Length)
//    {
//      return this;
//    }

//    var rune = new Rune(paddingChar);

//    Span<byte> buffer = stackalloc byte[4];
//    var byteCount = rune.EncodeToUtf8(buffer);

//    var resultBuffer = new byte[ByteLength + byteCount * (totalWidth - Length)];

//    CopyTo(resultBuffer.AsSpan(byteCount * (totalWidth - Length)));

//    var temp = resultBuffer.AsSpan(0, byteCount * (totalWidth - Length));

//    if (byteCount == 1)
//    {
//      temp.Fill(buffer[0]);
//    }
//    else
//    {
//      while (buffer.TryCopyTo(temp))
//      {
//        temp = temp[byteCount..];
//      }
//    }

//    return new Utf8String(resultBuffer);
//  }

//  public Utf8String PadRight(int totalWidth, char paddingChar = ' ')
//  {
//    if (totalWidth <= Length)
//    {
//      return this;
//    }

//    var rune = new Rune(paddingChar);

//    Span<byte> buffer = stackalloc byte[4];
//    var byteCount = rune.EncodeToUtf8(buffer);

//    var resultBuffer = new byte[ByteLength + byteCount * (totalWidth - Length)];

//    CopyTo(resultBuffer.AsSpan(0, ByteLength));

//    var temp = resultBuffer.AsSpan(ByteLength);

//    if (byteCount == 1)
//    {
//      temp.Fill(buffer[0]);
//    }
//    else
//    {
//      while (buffer[..byteCount].TryCopyTo(temp))
//      {
//        temp = temp[byteCount..];
//      }
//    }

//    return new Utf8String(resultBuffer);
//  }

//  [MethodImpl(MethodImplOptions.AggressiveInlining)]
//  public void CopyTo(Span<char> span)
//  {
//    Utf8.ToUtf16(_data, span, out _, out _, false, false);
//  }

//  public bool TryCopyTo(Span<char> span)
//  {
//    return Utf8.ToUtf16(_data, span, out _, out _, false, false) is OperationStatus.Done;
//  }

//  public void CopyTo(Span<byte> span)
//  {
//    _data.CopyTo(span);
//  }

//  public bool TryCopyTo(Span<byte> span)
//  {
//    return AsBytes().TryCopyTo(span);
//  }

//  [MethodImpl(MethodImplOptions.AggressiveInlining)]
//  public ReadOnlySpan<byte> AsBytes()
//  {
//    return _data.AsSpan();
//  }

//  public override string ToString()
//  {
//    return String.Create(Length, this, (span, utf8String) => { utf8String.CopyTo(span); });
//  }

//  public override int GetHashCode()
//  {
//    return GetHashCode(ref MemoryMarshal.GetArrayDataReference(_data), ByteLength);
//  }

//  public IEnumerable<Rune> EnumerateRunes()
//  {
//    var byteCount = 0;

//    while (Rune.DecodeFromUtf8(_data.AsSpan(byteCount), out var rune, out var bytesConsumed) is OperationStatus.Done)
//    {
//      yield return rune;

//      byteCount += bytesConsumed;
//    }
//  }

//  public IEnumerable<Rune> EnumerateRunesReversed()
//  {
//    var byteCount = ByteLength;

//    while (Rune.DecodeLastFromUtf8(_data.AsSpan(0, byteCount), out var rune, out var bytesConsumed) is OperationStatus.Done)
//    {
//      yield return rune;

//      byteCount -= bytesConsumed;
//    }
//  }

//  public IEnumerator<char> GetEnumerator()
//  {
//    var buffer = GC.AllocateUninitializedArray<char>(2);

//    foreach (var rune in EnumerateRunes())
//    {
//      var count = rune.EncodeToUtf16(buffer);

//      if (count > 0)
//      {
//        yield return MemoryMarshal.GetArrayDataReference(buffer);

//        if (count > 1)
//        {
//          yield return Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(buffer), 1);
//        }
//      }
//    }
//  }

//  IEnumerator IEnumerable.GetEnumerator()
//  {
//    return GetEnumerator();
//  }

//  public static bool operator ==(Utf8String x, Utf8String y)
//  {
//    return x.Equals(y);
//  }

//  public static bool operator !=(Utf8String x, Utf8String y)
//  {
//    return !(x == y);
//  }

//  public static Utf8String operator +(Utf8String x, Utf8String y)
//  {
//    return Concat(x, y);
//  }

//  public override bool Equals(object? obj)
//  {
//    return obj is Utf8String dynamicString && Equals(dynamicString);
//  }

//  public bool Equals(Utf8String other)
//  {
//    return ByteLength == other.ByteLength && AsBytes().SequenceEqual(other.AsBytes());
//  }

//  public int Compare(Utf8String x, Utf8String y)
//  {
//    using var xRunes = x.EnumerateRunes().GetEnumerator();
//    using var yRunes = y.EnumerateRunes().GetEnumerator();

//    while (xRunes.MoveNext() && yRunes.MoveNext())
//    {
//      var result = xRunes.Current.CompareTo(yRunes.Current);

//      if (result != 0)
//        return result;
//    }

//    return x.ByteLength.CompareTo(y.ByteLength);
//  }

//  public int CompareTo(Utf8String other)
//  {
//    return Compare(this, other);
//  }

//  private static int TryGetCount(ReadOnlySpan<byte> buffer, int index)
//  {
//    if (index >= buffer.Length)
//      return -1;

//    var x = buffer[index];

//    var byteCount =
//      x < 192 ? 1 :
//      x < 224 ? 2 :
//      x < 240 ? 3 :
//      4;

//    if (index + byteCount > buffer.Length)
//      return -1;

//    return byteCount;
//  }

//  private static Utf8String TrimInternal(Span<byte> data)
//  {
//    var startOffset = 0;
//    var byteLength = data.Length;

//    while (Rune.DecodeFromUtf8(data[startOffset..], out var rune, out var bytesConsumed) is OperationStatus.Done && Rune.IsWhiteSpace(rune))
//    {
//      startOffset += bytesConsumed;
//    }

//    while (Rune.DecodeLastFromUtf8(data[..byteLength], out var rune, out var bytesConsumed) is OperationStatus.Done && Rune.IsWhiteSpace(rune))
//    {
//      byteLength -= bytesConsumed;
//    }

//    return new Utf8String(data[startOffset..byteLength]);
//  }

//  private static unsafe int GetHashCode(ref byte r0, int length)
//  {
//    // From: https://github.com/CommunityToolkit/WindowsCommunityToolkit/blob/0b99272c983d023147dbf48e6d4eec825d71e5b9/Microsoft.Toolkit.HighPerformance/Helpers/Internals/SpanHelper.Hash.cs#L87
//    var hash = 5381;
//    nint offset = 0;

//    // Check whether SIMD instructions are supported, and also check
//    // whether we have enough data to perform at least one unrolled
//    // iteration of the vectorized path. This heuristics is to balance
//    // the overhead of loading the constant values in the two registers,
//    // and the final loop to combine the partial hash values.
//    // Note that even when we use the vectorized path we don't need to do
//    // any preprocessing to try to get memory aligned, as that would cause
//    // the hash codes to potentially be different for the same data.
//    if (Vector.IsHardwareAccelerated && length >= Vector<byte>.Count << 3)
//    {
//      var vh = new Vector<int>(5381);
//      var v33 = new Vector<int>(33);

//      // First vectorized loop, with 8 unrolled iterations.
//      // Assuming 256-bit registers (AVX2), a total of 256 bytes are processed
//      // per iteration, with the partial hashes being accumulated for later use.
//      while (length >= Vector<byte>.Count << 3)
//      {
//        ref var ri0 = ref Unsafe.Add(ref r0, offset + Vector<byte>.Count * 0);
//        var vi0 = Unsafe.ReadUnaligned<Vector<int>>(ref ri0);
//        var vp0 = Vector.Multiply(vh, v33);
//        vh = Vector.Xor(vp0, vi0);

//        ref var ri1 = ref Unsafe.Add(ref r0, offset + Vector<byte>.Count * 1);
//        var vi1 = Unsafe.ReadUnaligned<Vector<int>>(ref ri1);
//        var vp1 = Vector.Multiply(vh, v33);
//        vh = Vector.Xor(vp1, vi1);

//        ref var ri2 = ref Unsafe.Add(ref r0, offset + Vector<byte>.Count * 2);
//        var vi2 = Unsafe.ReadUnaligned<Vector<int>>(ref ri2);
//        var vp2 = Vector.Multiply(vh, v33);
//        vh = Vector.Xor(vp2, vi2);

//        ref var ri3 = ref Unsafe.Add(ref r0, offset + Vector<byte>.Count * 3);
//        var vi3 = Unsafe.ReadUnaligned<Vector<int>>(ref ri3);
//        var vp3 = Vector.Multiply(vh, v33);
//        vh = Vector.Xor(vp3, vi3);

//        ref var ri4 = ref Unsafe.Add(ref r0, offset + Vector<byte>.Count * 4);
//        var vi4 = Unsafe.ReadUnaligned<Vector<int>>(ref ri4);
//        var vp4 = Vector.Multiply(vh, v33);
//        vh = Vector.Xor(vp4, vi4);

//        ref var ri5 = ref Unsafe.Add(ref r0, offset + Vector<byte>.Count * 5);
//        var vi5 = Unsafe.ReadUnaligned<Vector<int>>(ref ri5);
//        var vp5 = Vector.Multiply(vh, v33);
//        vh = Vector.Xor(vp5, vi5);

//        ref var ri6 = ref Unsafe.Add(ref r0, offset + Vector<byte>.Count * 6);
//        var vi6 = Unsafe.ReadUnaligned<Vector<int>>(ref ri6);
//        var vp6 = Vector.Multiply(vh, v33);
//        vh = Vector.Xor(vp6, vi6);

//        ref var ri7 = ref Unsafe.Add(ref r0, offset + Vector<byte>.Count * 7);
//        var vi7 = Unsafe.ReadUnaligned<Vector<int>>(ref ri7);
//        var vp7 = Vector.Multiply(vh, v33);
//        vh = Vector.Xor(vp7, vi7);

//        length -= Vector<byte>.Count << 3;
//        offset += Vector<byte>.Count << 3;
//      }

//      // When this loop is reached, there are up to 255 bytes left (on AVX2).
//      // Each iteration processed an additional 32 bytes and accumulates the results.
//      while (length >= Vector<byte>.Count)
//      {
//        ref var ri = ref Unsafe.Add(ref r0, offset);
//        var vi = Unsafe.ReadUnaligned<Vector<int>>(ref ri);
//        var vp = Vector.Multiply(vh, v33);
//        vh = Vector.Xor(vp, vi);

//        length -= Vector<byte>.Count;
//        offset += Vector<byte>.Count;
//      }

//      // Combine the partial hash values in each position.
//      // The loop below should automatically be unrolled by the JIT.
//      for (var j = 0; j < Vector<int>.Count; j++)
//      {
//        hash = unchecked(((hash << 5) + hash) ^ vh[j]);
//      }
//    }
//    else
//    {
//      // Only use the loop working with 64-bit values if we are on a
//      // 64-bit processor, otherwise the result would be much slower.
//      // Each unrolled iteration processes 64 bytes.
//      if (sizeof(nint) == sizeof(ulong))
//      {
//        while (length >= sizeof(ulong) << 3)
//        {
//          ref var ri0 = ref Unsafe.Add(ref r0, offset + sizeof(ulong) * 0);
//          var value0 = Unsafe.ReadUnaligned<ulong>(ref ri0);
//          hash = unchecked(((hash << 5) + hash) ^ (int)value0 ^ (int)(value0 >> 32));

//          ref var ri1 = ref Unsafe.Add(ref r0, offset + sizeof(ulong) * 1);
//          var value1 = Unsafe.ReadUnaligned<ulong>(ref ri1);
//          hash = unchecked(((hash << 5) + hash) ^ (int)value1 ^ (int)(value1 >> 32));

//          ref var ri2 = ref Unsafe.Add(ref r0, offset + sizeof(ulong) * 2);
//          var value2 = Unsafe.ReadUnaligned<ulong>(ref ri2);
//          hash = unchecked(((hash << 5) + hash) ^ (int)value2 ^ (int)(value2 >> 32));

//          ref var ri3 = ref Unsafe.Add(ref r0, offset + sizeof(ulong) * 3);
//          var value3 = Unsafe.ReadUnaligned<ulong>(ref ri3);
//          hash = unchecked(((hash << 5) + hash) ^ (int)value3 ^ (int)(value3 >> 32));

//          ref var ri4 = ref Unsafe.Add(ref r0, offset + sizeof(ulong) * 4);
//          var value4 = Unsafe.ReadUnaligned<ulong>(ref ri4);
//          hash = unchecked(((hash << 5) + hash) ^ (int)value4 ^ (int)(value4 >> 32));

//          ref var ri5 = ref Unsafe.Add(ref r0, offset + sizeof(ulong) * 5);
//          var value5 = Unsafe.ReadUnaligned<ulong>(ref ri5);
//          hash = unchecked(((hash << 5) + hash) ^ (int)value5 ^ (int)(value5 >> 32));

//          ref var ri6 = ref Unsafe.Add(ref r0, offset + sizeof(ulong) * 6);
//          var value6 = Unsafe.ReadUnaligned<ulong>(ref ri6);
//          hash = unchecked(((hash << 5) + hash) ^ (int)value6 ^ (int)(value6 >> 32));

//          ref var ri7 = ref Unsafe.Add(ref r0, offset + sizeof(ulong) * 7);
//          var value7 = Unsafe.ReadUnaligned<ulong>(ref ri7);
//          hash = unchecked(((hash << 5) + hash) ^ (int)value7 ^ (int)(value7 >> 32));

//          length -= sizeof(ulong) << 3;
//          offset += sizeof(ulong) << 3;
//        }
//      }

//      // Each unrolled iteration processes 32 bytes
//      while (length >= sizeof(uint) << 3)
//      {
//        ref var ri0 = ref Unsafe.Add(ref r0, offset + sizeof(uint) * 0);
//        var value0 = Unsafe.ReadUnaligned<uint>(ref ri0);
//        hash = unchecked(((hash << 5) + hash) ^ (int)value0);

//        ref var ri1 = ref Unsafe.Add(ref r0, offset + sizeof(uint) * 1);
//        var value1 = Unsafe.ReadUnaligned<uint>(ref ri1);
//        hash = unchecked(((hash << 5) + hash) ^ (int)value1);

//        ref var ri2 = ref Unsafe.Add(ref r0, offset + sizeof(uint) * 2);
//        var value2 = Unsafe.ReadUnaligned<uint>(ref ri2);
//        hash = unchecked(((hash << 5) + hash) ^ (int)value2);

//        ref var ri3 = ref Unsafe.Add(ref r0, offset + sizeof(uint) * 3);
//        var value3 = Unsafe.ReadUnaligned<uint>(ref ri3);
//        hash = unchecked(((hash << 5) + hash) ^ (int)value3);

//        ref var ri4 = ref Unsafe.Add(ref r0, offset + sizeof(uint) * 4);
//        var value4 = Unsafe.ReadUnaligned<uint>(ref ri4);
//        hash = unchecked(((hash << 5) + hash) ^ (int)value4);

//        ref var ri5 = ref Unsafe.Add(ref r0, offset + sizeof(uint) * 5);
//        var value5 = Unsafe.ReadUnaligned<uint>(ref ri5);
//        hash = unchecked(((hash << 5) + hash) ^ (int)value5);

//        ref var ri6 = ref Unsafe.Add(ref r0, offset + sizeof(uint) * 6);
//        var value6 = Unsafe.ReadUnaligned<uint>(ref ri6);
//        hash = unchecked(((hash << 5) + hash) ^ (int)value6);

//        ref var ri7 = ref Unsafe.Add(ref r0, offset + sizeof(uint) * 7);
//        var value7 = Unsafe.ReadUnaligned<uint>(ref ri7);
//        hash = unchecked(((hash << 5) + hash) ^ (int)value7);

//        length -= sizeof(uint) << 3;
//        offset += sizeof(uint) << 3;
//      }
//    }

//    // At this point (assuming AVX2), there will be up to 31 bytes
//    // left, both for the vectorized and non vectorized paths.
//    // That number would go up to 63 on AVX512 systems, in which case it is
//    // still useful to perform this last loop unrolling.
//    if (length >= sizeof(ushort) << 3)
//    {
//      ref var ri0 = ref Unsafe.Add(ref r0, offset + sizeof(ushort) * 0);
//      var value0 = Unsafe.ReadUnaligned<ushort>(ref ri0);
//      hash = unchecked(((hash << 5) + hash) ^ value0);

//      ref var ri1 = ref Unsafe.Add(ref r0, offset + sizeof(ushort) * 1);
//      var value1 = Unsafe.ReadUnaligned<ushort>(ref ri1);
//      hash = unchecked(((hash << 5) + hash) ^ value1);

//      ref var ri2 = ref Unsafe.Add(ref r0, offset + sizeof(ushort) * 2);
//      var value2 = Unsafe.ReadUnaligned<ushort>(ref ri2);
//      hash = unchecked(((hash << 5) + hash) ^ value2);

//      ref var ri3 = ref Unsafe.Add(ref r0, offset + sizeof(ushort) * 3);
//      var value3 = Unsafe.ReadUnaligned<ushort>(ref ri3);
//      hash = unchecked(((hash << 5) + hash) ^ value3);

//      ref var ri4 = ref Unsafe.Add(ref r0, offset + sizeof(ushort) * 4);
//      var value4 = Unsafe.ReadUnaligned<ushort>(ref ri4);
//      hash = unchecked(((hash << 5) + hash) ^ value4);

//      ref var ri5 = ref Unsafe.Add(ref r0, offset + sizeof(ushort) * 5);
//      var value5 = Unsafe.ReadUnaligned<ushort>(ref ri5);
//      hash = unchecked(((hash << 5) + hash) ^ value5);

//      ref var ri6 = ref Unsafe.Add(ref r0, offset + sizeof(ushort) * 6);
//      var value6 = Unsafe.ReadUnaligned<ushort>(ref ri6);
//      hash = unchecked(((hash << 5) + hash) ^ value6);

//      ref var ri7 = ref Unsafe.Add(ref r0, offset + sizeof(ushort) * 7);
//      var value7 = Unsafe.ReadUnaligned<ushort>(ref ri7);
//      hash = unchecked(((hash << 5) + hash) ^ value7);

//      length -= sizeof(ushort) << 3;
//      offset += sizeof(ushort) << 3;
//    }

//    // Handle the leftover items
//    while (length > 0)
//    {
//      hash = unchecked(((hash << 5) + hash) ^ Unsafe.Add(ref r0, offset));

//      length -= 1;
//      offset += 1;
//    }

//    return hash;
//  }
//}