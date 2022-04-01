using Microsoft.Toolkit.HighPerformance.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Microsoft.Toolkit.HighPerformance;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Diagnostics.CodeAnalysis;

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

    public static DynamicString Empty = new(Array.Empty<byte>());

    public unsafe DynamicString(ReadOnlySpan<char> data)
    {
      _isUtf8 = true;
      var index = 0;

      var max = Vector128<short>.Zero;

      if (Avx2.IsSupported && Vector256<short>.Count * 2 <= data.Length)
      {
        var vectors = MemoryMarshal.Cast<char, Vector256<short>>(data);
        var result = vectors[0];

        for (int i = 1; i < vectors.Length; i++)
        {
          result = Avx2.Max(result, vectors[i]);
        }

        max = Sse2.Max(result.GetLower(), result.GetUpper());
        index = vectors.Length * Vector256<short>.Count;
      }

      if (Sse2.IsSupported && (Vector128<short>.Count * 2 <= data.Length - index || index != 0))
      {
        var vectors = MemoryMarshal.Cast<char, Vector128<short>>(data[index..]);

        for (int i = 0; i < vectors.Length; i++)
        {
          max = Sse2.Max(max, vectors[i]);
        }

        var compare = Sse2.CompareGreaterThan(Vector128.Create((short)Byte.MaxValue), max);
        var isMatch = Unsafe.As<Vector128<short>, long>(ref compare);

        if (isMatch != -1)
        {
          _isUtf8 = false;
          index = data.Length;
        }
        else
        {
          index += vectors.Length * Vector128<short>.Count;
        }
      }

      for (var i = index; i < data.Length; i++)
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

        for (int i = 0; i < _data.Length; i++)
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

    public char this[int index]
    {
      get
      {
        if (_isUtf8)
        {
          return (char)_data[index];
        }
        else
        {
          return Unsafe.ReadUnaligned<char>(ref _data[index * 2]);
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

      for (int i = 0; i < Length; i++)
      {
        if (Char.IsWhiteSpace(this[i]))
        {
          return true;
        }
      }

      //if (_isUtf8)
      //{
      //	Span<char> chars = stackalloc char[Encoding.UTF8.GetCharCount(_data)];
      //	Encoding.UTF8.GetChars(_data, chars);

      //	for (var i = 0; i < chars.Length; i++)
      //	{
      //		if (Char.IsWhiteSpace(chars[i]))
      //		{
      //			return true;
      //		}
      //	}
      //}
      //else
      //{
      //	var chars = _data.AsSpan().Cast<byte, char>();

      //	for (var i = 0; i < chars.Length; i++)
      //	{
      //		if (Char.IsWhiteSpace(chars[i]))
      //		{
      //			return true;
      //		}
      //	}
      //}

      return false;
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
      for (int i = 0; i < Length; i++)
      {
        yield return this[i];
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

    public int GetHashCode([DisallowNull] DynamicString obj)
    {
      throw new NotImplementedException();
    }
  }
}
