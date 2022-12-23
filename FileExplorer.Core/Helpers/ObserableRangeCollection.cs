using Avalonia.Threading;
using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using FileExplorer.Core.Extensions;
using FileExplorer.Core.Interfaces;
using PerformanceTests;

namespace FileExplorer.Core.Helpers;

/// <summary> 
/// Represents a dynamic data collection that provides notifications when items get added, removed, or when the whole list is refreshed. 
/// </summary> 
/// <typeparam name="T"></typeparam>
public sealed class ObservableRangeCollection<T> : INotifyCollectionChanged, IList<T>, IList
{
	public event Action<int> CountChanged = delegate { };
	public event Action<string> OnPropertyChanged = delegate { };

	public event NotifyCollectionChangedEventHandler? CollectionChanged = delegate { };

	private readonly DynamicList<T> _data = new();

	private const int UpdateTime = 500;
	private const int UpdateCountTime = 50;

	public int Count => _data.Count;

	public bool IsReadOnly => false;

	public bool IsFixedSize => false;

	public bool IsSynchronized => false;

	public object SyncRoot { get; } = new object();

	object? IList.this[int index]
	{
		get => _data[index];
		set => _data[index] = (T)value;
	}

	public T this[int index]
	{
		get => _data[index];
		set
		{
			_data[index] = value;

			OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, value, index));
		}
	}

	public ObservableRangeCollection()
	{

	}

	public ObservableRangeCollection(IEnumerable<T> items, bool needsReset = false) : this()
	{
		if (items is ICollection<T>)
		{
			_data.AddRange(items);
		}
		else
		{
			ThreadPool.QueueUserWorkItem(async x => await AddRange<Comparer<T>>(items));
		}
	}

	public ObservableRangeCollection(IAsyncEnumerable<T> items) : this()
	{
		AddRangeAsync<Comparer<T>>(items);
	}

	/// <summary> 
	/// Removes the first occurence of each item in the specified collection from ObservableCollection(Of T). 
	/// </summary> 
	public ValueTask RemoveRange(IEnumerable<T> collection)
	{
		ArgumentNullException.ThrowIfNull(collection);

		foreach (var i in collection)
			_data.Remove(i);

		return OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
	}

	/// <summary> 
	/// Clears the current collection and replaces it with the specified collection. 
	/// </summary> 
	public async Task AddRange<TComparer>(IEnumerable<T> collection, TComparer? comparer = default, CancellationToken token = default) where TComparer : IComparer<T>
	{
		ArgumentNullException.ThrowIfNull(collection);

		var watch = Stopwatch.GetTimestamp();
		var isDone = false;
		var buffer = new List<T>();

		Task.Run(async () =>
		{
			var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(UpdateCountTime));

			while (!isDone && await timer.WaitForNextTickAsync(token))
			{
				CountChanged(Count);
			}
		}, token);

		ValueTask task = default;

		foreach (var item in collection)
		{
			if (token.IsCancellationRequested)
			{
				break;
			}

			buffer.Add(item);

			if (task is { IsCompleted: true } && Stopwatch.GetElapsedTime(watch).TotalMilliseconds >= UpdateTime)
			{
				UpdateList(buffer, comparer);

				buffer.Clear();

				task = OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

				watch = Stopwatch.GetTimestamp();
			}
		}

		UpdateList(buffer, comparer);

		await OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		CountChanged(Count);

		isDone = true;

		void UpdateList(List<T> buffer, TComparer comparer)
		{
			foreach (var bufferItem in buffer)
			{
				var index = BinarySearch(bufferItem, comparer);

				if (index >= 0)
				{
					_data.Insert(index, bufferItem);
				}
				else
				{
					_data.Insert(~index, bufferItem);
				}
			}
		}
	}

	/// <summary>
	/// Clears the current collection and replaces it with the specified collection.
	/// </summary>
	public async Task AddRangeAsyncComparer<TComparer>(IEnumerable<T> collection, TComparer? comparer = default, CancellationToken token = default) where TComparer : IAsyncComparer<T>
	{
		ArgumentNullException.ThrowIfNull(collection);

		if (comparer is null)
		{
			await AddRange(collection, token);
			return;
		}

		var watch = Stopwatch.GetTimestamp();
		var isDone = false;
		var buffer = new List<T>();

		Task.Run(async () =>
		{
			var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(UpdateCountTime));

			while (!isDone && await timer.WaitForNextTickAsync(token))
			{
				CountChanged(Count);
			}
		}, token);

		ValueTask task = default;

		foreach (var item in collection)
		{
			if (token.IsCancellationRequested)
			{
				break;
			}

			buffer.Add(item);

			if (task is { IsCompleted: true } && Stopwatch.GetElapsedTime(watch).TotalMilliseconds >= UpdateTime)
			{
				await UpdateList(buffer, comparer);

				buffer.Clear();

				task = OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

				watch = Stopwatch.GetTimestamp();
			}
		}

		await UpdateList(buffer, comparer);

		await OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		CountChanged(Count);

		isDone = true;

		async ValueTask UpdateList(List<T> buffer, TComparer comparer)
		{
			foreach (var bufferItem in buffer)
			{
				var index = await BinarySearchAsync(bufferItem, comparer);

				if (index >= 0)
				{
					_data.Insert(index, bufferItem);
				}
				else
				{
					_data.Insert(~index, bufferItem);
				}
			}
		}
	}

	/// <summary> 
	/// Clears the current collection and replaces it with the specified collection. 
	/// </summary> 
	public async Task AddRange(IEnumerable<T> collection, CancellationToken token = default)
	{
		ArgumentNullException.ThrowIfNull(collection);

		var watch = Stopwatch.GetTimestamp();

		var isDone = false;
		var index = Math.Max(0, Count - 1);

		Task.Run(async () =>
		{
			using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(UpdateCountTime));

			while (!isDone && await timer.WaitForNextTickAsync(token))
			{
				CountChanged(Count);
			}
		}, token);

		foreach (var item in collection)
		{
			if (token.IsCancellationRequested)
			{
				break;
			}

			_data.Add(item);

			if (Stopwatch.GetElapsedTime(watch).TotalMilliseconds >= UpdateTime)
			{
				OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, new ReadonlyPartialCollection<T>(_data, index, Count - index), index));

				watch = Stopwatch.GetTimestamp();
				index = Count;
			}
		}
	}

	/// <summary> 
	/// Clears the current collection and replaces it with the specified collection. 
	/// </summary> 
	public async Task AddRangeAsync<TComparer>(IAsyncEnumerable<T> collection, TComparer comparer = default, CancellationToken token = default) where TComparer : IComparer<T>
	{
		ArgumentNullException.ThrowIfNull(collection);

		var watch = Stopwatch.GetTimestamp();
		var isDone = false;
		var buffer = new List<T>();

		Task.Run(async () =>
		{
			var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(UpdateCountTime));

			while (!isDone && await timer.WaitForNextTickAsync(token))
			{
				CountChanged(Count);
			}
		}, token);

		ValueTask task = default;

		await foreach (var item in collection.WithCancellation(token))
		{
			buffer.Add(item);

			if (task is { IsCompleted: true } && Stopwatch.GetElapsedTime(watch).TotalMilliseconds >= UpdateTime)
			{
				UpdateList(buffer, comparer);

				buffer.Clear();

				task = OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

				watch = Stopwatch.GetTimestamp();
			}
		}

		UpdateList(buffer, comparer);

		await OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		CountChanged(Count);

		isDone = true;

		void UpdateList(List<T> buffer, TComparer comparer)
		{
			foreach (var bufferItem in buffer)
			{
				var index = BinarySearch(bufferItem, comparer);

				if (index >= 0)
				{
					_data.Insert(index, bufferItem);
				}
				else
				{
					_data.Insert(~index, bufferItem);
				}
			}
		}
	}

	/// <summary> 
	/// Clears the current collection and replaces it with the specified collection. 
	/// </summary> 
	public async Task AddRangeAsync(IAsyncEnumerable<T> collection, CancellationToken token = default)
	{
		ArgumentNullException.ThrowIfNull(collection);

		var watch = Stopwatch.GetTimestamp();
		var index = Math.Max(0, Count - 1);

		using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(UpdateCountTime));
		var timerTask = timer.WaitForNextTickAsync(token);

		var task = ValueTask.CompletedTask;

		await foreach (var item in collection.WithCancellation(token))
		{
			_data.Add(item);

			if (timerTask is { IsCompleted: true, Result: true })
			{
				CountChanged(Count);

				timerTask = timer.WaitForNextTickAsync(token);
			}

			if (task is { IsCompleted: true } && Stopwatch.GetElapsedTime(watch).TotalMilliseconds >= UpdateTime)
			{
				task = OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, new ReadonlyPartialCollection<T>(_data, index, Count - index), index));
				watch = Stopwatch.GetTimestamp();

				index = Count;
			}
		}

		await OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, new ReadonlyPartialCollection<T>(_data, index, Count - index), index));
		CountChanged(Count);
	}

	/// <summary>
	/// Clears the current collection and replaces it with the specified collection.
	/// </summary>
	public async Task AddRangeAsync(IItemProvider provider, IFileItem folder, string pattern, CancellationToken token = default)
	{
		ArgumentNullException.ThrowIfNull(provider);

		var index = Math.Max(0, Count - 1);
		var bag = new DynamicBag<T>();

		var isLocked = false;

		var task = provider.EnumerateItemsAsync(folder, pattern, x => bag.Add((T)x), token).ConfigureAwait(false);
		var isDone = false;

		Runner.Run<Task>(async () =>
		{
			using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(UpdateCountTime));

			try
			{
				while (await timer.WaitForNextTickAsync(token) && !isDone)
				{
					var lockTaken = false;

					try
					{
						bag.FreezeBag(ref lockTaken);

						var count = bag.DangerousCount;

						if (count > 0)
						{
							_data.EnsureCapacity(_data.Count + count);
							_data._size += bag.CopyFromEachQueueToArray(_data._items, _data._size);

							OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, new ReadonlyPartialCollection<T>(_data, index, Count - index), index));

							index = Count;
						}
					}
					finally
					{
						bag.UnfreezeBag(lockTaken);
						bag.Clear();
					}
				}
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
			}
		}, token);

		await task;
		isDone = true;

		while (bag.TryTake(out var result))
		{
			_data.Add(result);
		}

		await OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, new ReadonlyPartialCollection<T>(_data, index, Count - index), index));
		CountChanged(Count);
	}

	public async ValueTask OnCollectionChanged(NotifyCollectionChangedEventArgs notifyCollectionChangedEventArgs)
	{
		try
		{
			if (CollectionChanged is not null)
			{
				if (Dispatcher.UIThread.CheckAccess())
				{
					CollectionChanged(this, notifyCollectionChangedEventArgs);
				}
				else
				{
					await Dispatcher.UIThread.InvokeAsync(() => CollectionChanged(this, notifyCollectionChangedEventArgs));
				}

				CountChanged(Count);
			}
		}
		catch (TaskCanceledException e)
		{
			Console.WriteLine(e);
		}
	}

	public async ValueTask ClearTrim()
	{
		_data.Clear();
		_data.Capacity = 0;

		await OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
	}

	public void Trim()
	{
		_data.Capacity = 0;
	}

	public int BinarySearch<TComparer>(T value, TComparer comparer) where TComparer : IComparer<T>
	{
		var lo = 0;
		var hi = _data.Count - 1;

		while (lo <= hi)
		{
			// i might overflow if lo and hi are both large positive numbers.
			var i = lo + ((hi - lo) >> 1);
			var c = comparer.Compare(_data[i], value);

			switch (c)
			{
				case 0:
					return i;
				case < 0:
					lo = i + 1;
					break;
				default:
					hi = i - 1;
					break;
			}
		}

		return ~lo;
	}

	public async ValueTask<int> BinarySearchAsync<TComparer>(T value, TComparer comparer) where TComparer : IAsyncComparer<T>
	{
		var lo = 0;
		var hi = _data.Count - 1;

		while (lo <= hi)
		{
			// i might overflow if lo and hi are both large positive numbers.
			var i = lo + ((hi - lo) >> 1);
			var c = await comparer.CompareAsync(_data[i], value);

			switch (c)
			{
				case 0:
					return i;
				case < 0:
					lo = i + 1;
					break;
				default:
					hi = i - 1;
					break;
			}
		}

		return ~lo;
	}

	public async Task Sort<TComparer>(TComparer comparer = default!) where TComparer : IComparer<T>
	{
		await ParallelQuickSort(_data, 0, _data.Count - 1, comparer);
		await OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
	}

	public IEnumerator<T> GetEnumerator()
	{
		for (var i = 0; i < _data.Count; i++)
		{
			yield return _data[i];
		}
	}

	public void PropertyChanged(string property)
	{
		OnPropertyChanged(property);
	}

	static async ValueTask ParallelQuickSort<TComparer>(IList<T> array, int left, int right, TComparer comparer) where TComparer : IComparer<T>
	{
		const int threshold = 250;
		var i = left;
		var j = right;
		var m = array[(left + right) / 2];

		while (i <= j)
		{
			while (comparer.Compare(array[i], m) is -1)
			{
				i++;
			}

			while (comparer.Compare(array[j], m) is 1)
			{
				j--;
			}

			if (i <= j)
			{
				(array[i], array[j]) = (array[j], array[i]);

				i++;
				j--;
			}
		}

		if (j - left > threshold && right - i > threshold)
		{
			await Task.WhenAll(
				Task.Run(() => ParallelQuickSort(array, left, j, comparer)),
				Task.Run(() => ParallelQuickSort(array, i, right, comparer)));
		}
		else
		{
			if (j > left)
			{
				await ParallelQuickSort(array, left, j, comparer);
			}

			if (i < right)
			{
				await ParallelQuickSort(array, i, right, comparer);
			}
		}
	}

// private static async Task ParallelQuickSortAsync(IList<T> array, int left, int right, IAsyncComparer<T> comparer)
// {
//   const int Threshold = 250;
//   var i = left;
//   var j = right;
//   var m = array[(left + right) / 2];
//
//   while (i <= j)
//   {
//     while (await comparer.CompareAsync(array[i], m) is -1)
//     {
//       i++;
//     }
//
//     while (await comparer.CompareAsync(array[j], m) is 1)
//     {
//       j--;
//     }
//
//     if (i <= j)
//     {
//       (array[i], array[j]) = (array[j], array[i]);
//
//       i++;
//       j--;
//     }
//   }
//
//   if (j - left > Threshold && right - i > Threshold)
//   {
//     await Task.WhenAll(ParallelQuickSortAsync(array, left, j, comparer), ParallelQuickSortAsync(array, i, right, comparer));
//   }
//   else
//   {
//     if (j > left)
//     {
//       await ParallelQuickSortAsync(array, left, j, comparer);
//     }
//
//     if (i < right)
//     {
//       await ParallelQuickSortAsync(array, i, right, comparer);
//     }
//   }
// }

	public int IndexOf(T item)
	{
		return _data.IndexOf(item);
	}

	public void Insert(int index, T item)
	{
		_data.Insert(index, item);
		OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
	}

	public void RemoveAt(int index)
	{
		_data.RemoveAt(index);
		OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

	}

	public void Add(T item)
	{
		_data.Add(item);
		OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
	}

	public void Clear()
	{
		_data.Clear();
		//_data.Capacity = 0;

		OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
	}

	public bool Contains(T item)
	{
		return _data.Contains(item);
	}

	public void CopyTo(T[] array, int arrayIndex)
	{
		_data.CopyTo(array, arrayIndex);
	}

	public bool Remove(T item)
	{
		var value = _data.Remove(item);

		if (value)
		{
			OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item));
		}

		return value;
	}

// public ref T GetPinnableReference()
// {
// 	return ref CollectionsMarshal.AsSpan(_data).GetPinnableReference();
// }

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}

	public int Add(object? value)
	{
		throw new NotImplementedException();
	}

	public bool Contains(object? value)
	{
		throw new NotImplementedException();
	}

	public int IndexOf(object? value)
	{
		return _data.IndexOf((T)value);
	}

	public void Insert(int index, object? value)
	{
		throw new NotImplementedException();
	}

	public void Remove(object? value)
	{
		throw new NotImplementedException();
	}

	public void CopyTo(Array array, int index)
	{
		throw new NotImplementedException();
	}

	private class TempList
	{
		private const int DefaultCapacity = 4;

		internal T[] _items; // Do not rename (binary serialization)
		internal int _size; // Do not rename (binary serialization)
		private int _version; // Do not rename (binary serialization)

#pragma warning disable CA1825 // avoid the extra generic instantiation for Array.Empty<T>()
		private static readonly T[] s_emptyArray = new T[0];
#pragma warning restore CA1825
	}
}