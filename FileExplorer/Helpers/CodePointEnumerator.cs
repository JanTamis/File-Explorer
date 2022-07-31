using System;
using System.Collections;
using System.Collections.Generic;

namespace FileExplorer.Helpers;

/// <summary>
/// string からコードポイントを列挙するための enumerator
/// </summary>
public ref struct CodePointEnumerator
{
  private readonly ReadOnlySpan<char> _str;
  private int _index;
  private byte _count;

  /// <summary>
  /// </summary>
  /// <param name="s">コードポイントを読み出す元。</param>
  public CodePointEnumerator(ReadOnlySpan<char> s)
  {
    _str = s;
    _index = 0;
    _count = 0;
    Current = 0;
  }

  internal int PositionInCodeUnits => _index;

  /// <summary><see cref="IEnumerator{T}.Current"/></summary>
  public uint Current { get; private set; }

  /// <summary><see cref="IEnumerator.MoveNext"/></summary>
  public bool MoveNext()
  {
    _index += _count;

    if (_index >= _str.Length) return false;

    var c = _str[_index];

    if (Char.IsHighSurrogate(c))
    {
      if (_index + 1 >= _str.Length) return false;

      var x = (c & 0b00000011_11111111U) + 0b100_0000;
      x <<= 10;
      c = _str[_index + 1];
      x |= (c & 0b00000011_11111111U);

      Current = x;
      _count = 2;
    }
    else
    {
      Current = c;
      _count = 1;
    }
    return true;
  }

  /// <summary><see cref="IEnumerator.Reset"/></summary>
  public void Reset() { _index = 0; _count = 0; }
}