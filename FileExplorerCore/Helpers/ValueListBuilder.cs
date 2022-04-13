using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace FileExplorerCore.Helpers;

public ref struct ValueListBuilder<T>
{
	private Span<T> _span;
	private T[]? _arrayFromPool;
	private int _pos;

	public ValueListBuilder(Span<T> initialSpan)
	{
		_span = initialSpan;
		_arrayFromPool = null;
		_pos = 0;
	}

	public ValueListBuilder(int length)
	{
		_arrayFromPool = ArrayPool<T>.Shared.Rent(length);
		_span = _arrayFromPool;
		_pos = 0;
	}

	public int Length
	{
		get => _pos;
		set => _pos = value;
	}

	public ref T this[int index] => ref _span[index];

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Append(T item)
	{
		var pos = _pos;
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

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Span<T> AppendSpan(int length)
	{
		var origPos = _pos;

		if (origPos > _span.Length - length)
		{
			Grow(length);
		}

		_pos = origPos + length;
		return _span.Slice(origPos, length);
	}

	// Hide uncommon path
	[MethodImpl(MethodImplOptions.NoInlining)]
	private void AddWithResize(T item)
	{
		var pos = _pos;
		Grow();
		_span[pos] = item;
		_pos = pos + 1;
	}

	public ReadOnlySpan<T> AsSpan()
	{
		return _span.Slice(0, _pos);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Dispose()
	{
		var toReturn = _arrayFromPool;
		if (toReturn != null)
		{
			_arrayFromPool = null;
			ArrayPool<T>.Shared.Return(toReturn);
		}
	}

	private void Grow()
	{
		var array = ArrayPool<T>.Shared.Rent(_span.Length * 2);

		_span.TryCopyTo(array);

		var toReturn = _arrayFromPool;
		_span = _arrayFromPool = array;
		if (toReturn != null)
		{
			ArrayPool<T>.Shared.Return(toReturn);
		}
	}

	private void Grow(int length)
	{
		var array = ArrayPool<T>.Shared.Rent(_span.Length + length);

		_span.TryCopyTo(array);

		var toReturn = _arrayFromPool;
		_span = _arrayFromPool = array;
		if (toReturn != null)
		{
			ArrayPool<T>.Shared.Return(toReturn);
		}
	}
}