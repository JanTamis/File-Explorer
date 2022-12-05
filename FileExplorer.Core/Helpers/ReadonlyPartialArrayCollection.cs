using System.Collections;

namespace FileExplorer.Core.Helpers;

public class ReadonlyPartialArrayCollection<T> : IList, IEnumerable<T>
{
	private readonly T[] _items;
	private readonly int _index;
	private readonly int _endIndex;

	public ReadonlyPartialArrayCollection(T[] items, int index, int count)
	{
		_items = items;
		_index = index;
		_endIndex = Math.Min(index + count, items.Length - 1);
	}

	object? IList.this[int index] { get => _items[_index + index]; set => throw new NotImplementedException(); }

	bool IList.IsFixedSize => true;

	bool IList.IsReadOnly => true;

	int ICollection.Count => _endIndex - _index;

	bool ICollection.IsSynchronized => false;

	object ICollection.SyncRoot => null;

	public IEnumerator<T> GetEnumerator()
	{
		for (var i = _index; i <= _endIndex && i < _items.Length; i++)
		{
			yield return _items[i];
		}
	}

	int IList.Add(object? value)
	{
		return -1;
	}

	void IList.Clear()
	{

	}

	bool IList.Contains(object? value)
	{
		return value is T search && this.Contains(search);
	}

	void ICollection.CopyTo(Array array, int index)
	{
		Array.Copy(_items, array, _endIndex - _index);
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}

	int IList.IndexOf(object? value)
	{
		throw new NotImplementedException();
	}

	void IList.Insert(int index, object? value)
	{

	}

	void IList.Remove(object? value)
	{

	}

	void IList.RemoveAt(int index)
	{

	}
}