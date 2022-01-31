using Avalonia.Threading;
using FileExplorerCore.Interfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Collections.Pooled;

namespace FileExplorerCore.Helpers
{
	/// <summary> 
	/// Represents a dynamic data collection that provides notifications when items get added, removed, or when the whole list is refreshed. 
	/// </summary> 
	/// <typeparam name="T"></typeparam> 
	public class ObservableRangeCollection<T> : INotifyCollectionChanged, IList<T>, IList
	{
		public event Action<int> CountChanged = delegate { };
		public event Action<string> OnPropertyChanged = delegate { };

		public event NotifyCollectionChangedEventHandler? CollectionChanged = delegate { };

		private readonly List<T> Data = new();

		const int updateTime = 1000;
		const int updateCountTime = 50;

		public int Count => Data.Count;

		public bool IsReadOnly => false;

		public bool IsFixedSize => false;

		public bool IsSynchronized => false;

		public object SyncRoot => new();

		object? IList.this[int index]
		{
			get => Data[index];
			set => Data[index] = value is null ? Data[index] : (T)value;
		}

		public T this[int index]
		{
			get => Data[index];
			set
			{
				Data[index] = value;

				if (Dispatcher.UIThread.CheckAccess())
				{
					OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, value));
				}
				else
				{
					Dispatcher.UIThread.InvokeAsync(() => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, value)));
				}
			}
		}

		public ObservableRangeCollection()
		{
			CollectionChanged += delegate
			{
				CountChanged(Count);
			};
		}

		public ObservableRangeCollection(IEnumerable<T> items) : this()
		{
			ThreadPool.QueueUserWorkItem(async x =>
			{
				await AddRange(items);
			});
		}

		/// <summary> 
		/// Removes the first occurence of each item in the specified collection from ObservableCollection(Of T). 
		/// </summary> 
		public void RemoveRange(IEnumerable<T> collection)
		{
			ArgumentNullException.ThrowIfNull(collection);

			foreach (var i in collection)
				Data.Remove(i);

			OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		}

		/// <summary> 
		/// Clears the current collection and replaces it with the specified collection. 
		/// </summary> 
		public async Task AddRange(IEnumerable<T> collection, IComparer<T>? comparer = null, Action<T>? action = null, CancellationToken token = default)
		{
			ArgumentNullException.ThrowIfNull(collection);

			var buffer = new Queue<T>();
			Task? task = null;

			var index = 0;

			var watch = Stopwatch.StartNew();
			var countWatch = Stopwatch.StartNew();

			foreach (var item in collection)
			{
				if (token.IsCancellationRequested)
					break;

				if (comparer != null && Data.Count > 0)
				{
					var i = Data.BinarySearch(item, comparer);

					if (i >= 0)
						Data.Insert(i, item);
					else
						Data.Insert(~i, item);
				}
				else
				{
					Data.Add(item);
				}

				if (action != null)
				{
					buffer.Enqueue(item);
				}

				if (watch.ElapsedMilliseconds >= updateTime)
				{
					if (action is not null || task is { IsCompleted: true } or null)
					{
						task = Task.Run(() =>
						{
							while (buffer.Count > 0 && !token.IsCancellationRequested)
							{
								if (token.IsCancellationRequested)
									break;

								while (buffer.TryDequeue(out var temp))
								{
									if (token.IsCancellationRequested)
										break;

									action!(temp);
								}
							}
						}, token);

					}

					if (comparer is null && Data.Count >= index && Data.Count > 0 && index > 0)
					{
						if (Dispatcher.UIThread.CheckAccess())
						{
							OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
						}
						else
						{
							await Dispatcher.UIThread.InvokeAsync(() => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)));
						}
					}
					else
					{
						if (Dispatcher.UIThread.CheckAccess())
						{
							OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
						}
						else
						{
							await Dispatcher.UIThread.InvokeAsync(() => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)));
						}
					}

					index = Data.Count;

					watch.Restart();
				}

				if (countWatch.ElapsedMilliseconds >= updateCountTime)
				{
					CountChanged(Data.Count);

					countWatch.Restart();
				}
			}

			if (action is not null || task is { IsCompleted: true } or null)
			{
				task = Task.Run(() =>
				{
					while (buffer.Count > 0 && !token.IsCancellationRequested)
					{
						while (buffer.TryDequeue(out var temp) && !token.IsCancellationRequested)
						{
							action!(temp);
						}
					}
				}, token);
			}

			if (comparer is null && Data.Count <= index && Data.Count > 0 && index > 0)
			{
				if (Dispatcher.UIThread.CheckAccess())
				{
					OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
				}
				else
				{
					await Dispatcher.UIThread.InvokeAsync(() => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)));
				}
			}
			else
			{
				if (Dispatcher.UIThread.CheckAccess())
				{
					OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
				}
				else
				{
					await Dispatcher.UIThread.InvokeAsync(() => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)));
				}
			}
		}

		/// <summary> 
		/// Clears the current collection and replaces it with the specified collection. 
		/// </summary> 
		public async Task AddRange(IEnumerable<T> collection, IAsyncComparer<T>? comparer, Action<T>? action = null, CancellationToken token = default)
		{
			ArgumentNullException.ThrowIfNull(collection);

			var buffer = new Queue<T>();
			Task? task = null;

			int index = 0;

			var watch = Stopwatch.StartNew();
			var countWatch = Stopwatch.StartNew();

			foreach (var item in collection)
			{
				if (token.IsCancellationRequested)
					break;

				if (comparer != null && Data.Count > 0)
				{
					var i = await BinarySearchAsync(item, 0, Data.Count, comparer);

					if (i >= 0)
						Data.Insert(i, item);
					else
						Data.Insert(~i, item);
				}
				else
				{
					Data.Add(item);
				}

				if (action != null)
				{
					buffer.Enqueue(item);
				}

				if (watch.ElapsedMilliseconds >= updateTime)
				{
					if (action is not null || task is { IsCompleted: true } or null)
					{
						task = Task.Run(() =>
						{
							while (buffer.Count > 0 && !token.IsCancellationRequested)
							{
								if (token.IsCancellationRequested)
									break;

								while (buffer.TryDequeue(out var temp))
								{
									if (token.IsCancellationRequested)
										break;

									action!(temp);
								}
							}
						}, token);

					}

					if (comparer == null && Data.Count <= index)
					{
						if (Dispatcher.UIThread.CheckAccess())
						{
							OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
						}
						else
						{
							await Dispatcher.UIThread.InvokeAsync(() => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)));
						}
					}
					else
					{
						if (Dispatcher.UIThread.CheckAccess())
						{
							OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
						}
						else
						{
							await Dispatcher.UIThread.InvokeAsync(() => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)));
						}
					}

					index = Data.Count - 1;

					watch.Restart();
				}

				if (countWatch.ElapsedMilliseconds >= updateCountTime)
				{
					CountChanged(Data.Count);

					countWatch.Restart();
				}
			}

			if (action is not null || task is { IsCompleted: true } or null)
			{
				task = Task.Run(() =>
				{
					while (buffer.Count > 0 && !token.IsCancellationRequested)
					{
						while (buffer.TryDequeue(out var temp) && !token.IsCancellationRequested)
						{
							action!(temp);
						}
					}
				}, token);
			}

			if (comparer == null && Data.Count <= index && Data.Count > 0 && index > 0)
			{
				if (Dispatcher.UIThread.CheckAccess())
				{
					OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
				}
				else
				{
					await Dispatcher.UIThread.InvokeAsync(() => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)));
				}
			}
			else
			{
				if (Dispatcher.UIThread.CheckAccess())
				{
					OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
				}
				else
				{
					await Dispatcher.UIThread.InvokeAsync(() => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)));
				}
			}
		}

		private void OnCollectionChanged(NotifyCollectionChangedEventArgs notifyCollectionChangedEventArgs)
		{
			CollectionChanged(this, notifyCollectionChangedEventArgs);
		}

		public async ValueTask ClearTrim()
		{
			Data.Clear();

			if (Dispatcher.UIThread.CheckAccess())
			{
				OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, Data));
			}
			else
			{
				await Dispatcher.UIThread.InvokeAsync(() => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, Data)));
			}

			Data.Capacity = 0;
		}

		public void Trim()
		{
			Data.Capacity = 0;
		}

		public int BinarySearch(T value, IComparer<T> comparer)
		{
			return Data.BinarySearch(value, comparer);
		}

		public async ValueTask<int> BinarySearchAsync(T value, int index, int length, IAsyncComparer<T> comparer)
		{
			var lo = index;
			var hi = index + length - 1;

			while (lo <= hi)
			{
				// i might overflow if lo and hi are both large positive numbers.
				var i = GetMedian(lo, hi);
				var c = await comparer.CompareAsync(Data[i], value);

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

			static int GetMedian(int low, int hi)
			{
				// Note both may be negative, if we are dealing with arrays w/ negative lower bounds.
				return low + ((hi - low) >> 1);
			}
		}

		public void Sort(IComparer<T>? comparer = null)
		{
			comparer ??= Comparer<T>.Default;

			ThreadPool.QueueUserWorkItem(x =>
			{
				ParallelQuickSort(Data, 0, Data.Count - 1, comparer);

				Dispatcher.UIThread.Post(() => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)));
			});
		}

		public async Task SortAsync(IAsyncComparer<T> comparer)
		{
			await ParallelQuickSortAsync(Data, 0, Data.Count - 1, comparer);

			Dispatcher.UIThread.Post(() => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)));
		}

		public IEnumerator<T> GetEnumerator()
		{
			var max = Data.Count;

			for (int i = 0; i < max; i++)
			{
				max = Data.Count;

				yield return Data[i];
			}
		}

		public void PropertyChanged(string property)
		{
			OnPropertyChanged(property);
		}

		static void ParallelQuickSort(IList<T> array, int left, int right, IComparer<T> comparer)
		{
			var Threshold = 250;
			var i = left;
			var j = right;
			var m = array[(left + right) / 2];

			while (i <= j)
			{
				while (comparer.Compare(array[i], m) is -1)
				{ i++; }
				while (comparer.Compare(array[j], m) is 1)
				{ j--; }

				if (i <= j)
				{
					(array[i], array[j]) = (array[j], array[i]);

					i++;
					j--;
				}
			}

			if (j - left > Threshold && right - i > Threshold)
			{
				Parallel.Invoke(
					() => ParallelQuickSort(array, left, j, comparer),
					() => ParallelQuickSort(array, i, right, comparer)
				);
			}
			else
			{
				if (j > left)
				{ ParallelQuickSort(array, left, j, comparer); }
				if (i < right)
				{ ParallelQuickSort(array, i, right, comparer); }
			}
		}

		static async Task ParallelQuickSortAsync(IList<T> array, int left, int right, IAsyncComparer<T> comparer)
		{
			var Threshold = 250;
			var i = left;
			var j = right;
			var m = array[(left + right) / 2];

			while (i <= j)
			{
				while (await comparer.CompareAsync(array[i], m) is -1)
				{
					i++;
				}

				while (await comparer.CompareAsync(array[j], m) is 1)
				{
					j--;
				}

				if (i <= j)
				{
					var temp = array[i];

					array[i] = array[j];
					array[j] = temp;

					i++;
					j--;
				}
			}

			if (j - left > Threshold && right - i > Threshold)
			{
				await Task.WhenAll(ParallelQuickSortAsync(array, left, j, comparer), ParallelQuickSortAsync(array, i, right, comparer));
			}
			else
			{
				if (j > left)
				{ 
					await ParallelQuickSortAsync(array, left, j, comparer); 
				}
				if (i < right)
				{ 
					await ParallelQuickSortAsync(array, i, right, comparer); 
				}
			}
		}

		public int IndexOf(T item)
		{
			return Data.IndexOf(item);
		}

		public void Insert(int index, T item)
		{
			Data.Insert(index, item);

			if (Dispatcher.UIThread.CheckAccess())
			{
				OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item));
			}
			else
			{
				Dispatcher.UIThread.InvokeAsync(() => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item)));
			}
		}

		public void RemoveAt(int index)
		{
			Data.RemoveAt(index);

			if (Dispatcher.UIThread.CheckAccess())
			{
				OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
			}
			else
			{
				Dispatcher.UIThread.InvokeAsync(() => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)));
			}
		}

		public void Add(T item)
		{
			Data.Add(item);

			if (Dispatcher.UIThread.CheckAccess())
			{
				OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item));
			}
			else
			{
				Dispatcher.UIThread.InvokeAsync(() => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item)));
			}
		}

		public void Clear()
		{
			Data.Clear();
		}

		public bool Contains(T item)
		{
			return Data.Contains(item);
		}

		public void CopyTo(T[] array, int arrayIndex)
		{
			Data.CopyTo(array, arrayIndex);
		}

		public bool Remove(T item)
		{
			var value = Data.Remove(item);

			if (value)
			{
				if (Dispatcher.UIThread.CheckAccess())
				{
					OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item));
				}
				else
				{
					Dispatcher.UIThread.InvokeAsync(() => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item)));
				}
			}

			return value;
		}

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
			return Data.IndexOf((T)value);
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
	}
}