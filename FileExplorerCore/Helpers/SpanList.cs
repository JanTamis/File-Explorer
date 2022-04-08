using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace FileExplorerCore.Helpers
{
  public ref partial struct SpanList<T>
  {
    private Span<T> _span;
    private T[]? _arrayFromPool;
    private int _pos;

    public SpanList(Span<T> initialSpan)
    {
      _span = initialSpan;
      _arrayFromPool = null;
      _pos = 0;
    }

    public SpanList(int capacity)
    {
      _arrayFromPool = ArrayPool<T>.Shared.Rent(capacity);
      _span = _arrayFromPool;
      _pos = 0;
    }

    public int Length
    {
      get => _pos;
      set
      {
        _pos = value;
      }
    }

    public ref T this[int index]
    {
      get
      {
        return ref _span[index];
      }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(T item)
    {
      int pos = _pos;
      if ((uint)pos < (uint)_span.Length)
      {
        _span[pos] = item;
        _pos = pos + 1;
      }
      else
      {
        AddWithResize(item);
      }
    }

    // Hide uncommon path
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddWithResize(T item)
    {
      int pos = _pos;
      Grow();
      _span[pos] = item;
      _pos = pos + 1;
    }

    public ReadOnlySpan<T> AsSpan()
    {
      return _span[.._pos];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
      var toReturn = _arrayFromPool;

      if (toReturn is not null)
      {
        _arrayFromPool = null;
        ArrayPool<T>.Shared.Return(toReturn);
      }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Pop()
    {
      _pos--;
      return _span[_pos];
    }

    private void Grow()
    {
      var array = ArrayPool<T>.Shared.Rent(_span.Length * 2);
      var toReturn = _arrayFromPool;

      _span.TryCopyTo(array);
      _span = _arrayFromPool = array;

      if (toReturn is not null)
      {
        ArrayPool<T>.Shared.Return(toReturn);
      }
    }
  }
}
