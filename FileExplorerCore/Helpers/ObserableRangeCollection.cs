using Avalonia.Threading;
using FileExplorerCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace FileExplorerCore.Helpers
{
	/// <summary> 
	/// Represents a dynamic data collection that provides notifications when items get added, removed, or when the whole list is refreshed. 
	/// </summary> 
	/// <typeparam name="T"></typeparam> 
	public class ObservableRangeCollection<T> : ObservableCollection<T>, IEnumerable<T>
	{
		public event Action<int> CountChanged = delegate { };
		public new event Action<string> OnPropertyChanged = delegate { };

		const int updateTime = 500;
		const int updateCountTime = 50;

		public ObservableRangeCollection()
		{
			base.CollectionChanged += delegate
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
				Items.Remove(i);

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

			if (Items is List<T> list)
			{
				var watch = Stopwatch.StartNew();
				var countWatch = Stopwatch.StartNew();

				foreach (var item in collection)
				{
					if (token.IsCancellationRequested)
						break;

					if (comparer != null && list.Count > 0)
					{
						var i = list.BinarySearch(item, comparer);

						if (i >= 0)
							list.Insert(i, item);
						else
							list.Insert(~i, item);
					}
					else
					{
						list.Add(item);
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

						if (comparer == null && list.Count >= index && list.Count > 0 && index > 0)
						{
							if (Dispatcher.UIThread.CheckAccess())
							{
								OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, list, Math.Min(index - 1, 0)));
							}
							else
							{
								await Dispatcher.UIThread.InvokeAsync(() => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, list, Math.Min(index - 1, 0))));
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

						index = list.Count;

						watch.Restart();
					}

					if (countWatch.ElapsedMilliseconds >= updateCountTime)
					{
						CountChanged(Items.Count);

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

				if (comparer == null && list.Count <= index && list.Count > 0 && index > 0)
				{
					if (Dispatcher.UIThread.CheckAccess())
					{
						OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, list, Math.Min(index - 1, 0)));
					}
					else
					{
						await Dispatcher.UIThread.InvokeAsync(() => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, list, Math.Min(index - 1, 0))));
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

			if (Items is List<T> list)
			{
				var watch = Stopwatch.StartNew();
				var countWatch = Stopwatch.StartNew();

				foreach (var item in collection)
				{
					if (token.IsCancellationRequested)
						break;

					if (comparer != null && list.Count > 0)
					{
						int i = await BinarySearchAsync(item, 0, list.Count, comparer);

						if (i >= 0)
							list.Insert(i, item);
						else
							list.Insert(~i, item);
					}
					else
					{
						list.Add(item);
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

						//if (comparer == null && list.Count <= index)
						//{
							if (Dispatcher.UIThread.CheckAccess())
							{
								OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, list, index));
							}
							else
							{
								await Dispatcher.UIThread.InvokeAsync(() => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, list, index)));
							}
						//}
						//else
						//{
						//	if (Dispatcher.UIThread.CheckAccess())
						//	{
						//		OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
						//	}
						//	else
						//	{
						//		await Dispatcher.UIThread.InvokeAsync(() => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)));
						//	}
						//}

						index = list.Count - 1;

						watch.Restart();
					}

					if (countWatch.ElapsedMilliseconds >= updateCountTime)
					{
						CountChanged(Items.Count);

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

				if (comparer == null && list.Count <= index && list.Count > 0 && index > 0)
				{
					if (Dispatcher.UIThread.CheckAccess())
					{
						OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, list, index));
					}
					else
					{
						await Dispatcher.UIThread.InvokeAsync(() => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, list, index)));
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
		}

		public void ClearTrim()
		{
			if (Items is List<T> list)
			{
				list.Clear();
				list.Capacity = 0;

				if (Dispatcher.UIThread.CheckAccess())
				{
					OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, list));
				}
				else
				{
					Dispatcher.UIThread.InvokeAsync(() => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, list)));
				}
			}
		}

		public int BinarySearch(T value, IComparer<T> comparer)
		{
			if (Items is List<T> list)
			{
				return list.BinarySearch(value, comparer);
			}

			return 0;
		}

		public async Task<int> BinarySearchAsync(T value, int index, int length, IAsyncComparer<T> comparer)
		{
			var lo = index;
			var hi = index + length - 1;

			while (lo <= hi)
			{
				// i might overflow if lo and hi are both large positive numbers.
				var i = GetMedian(lo, hi);

				int c;

				try
				{
					c = await comparer.CompareAsync(Items[i], value);
				}
				catch (Exception)
				{
					return default;
				}

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
				if (Items is List<T> list)
				{
					ParallelQuickSort(list, 0, Count - 1, comparer);

					Dispatcher.UIThread.Post(() => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)));
				}
			});
		}

		public new IEnumerator<T> GetEnumerator()
		{
			var max = Items.Count;

			for (int i = 0; i < max; i++)
			{
				max = Items.Count;

				yield return Items[i];
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
				while (comparer.Compare(array[i], m) is -1) { i++; }
				while (comparer.Compare(array[j], m) is 1) { j--; }

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
				Parallel.Invoke(
					() => ParallelQuickSort(array, left, j, comparer),
					() => ParallelQuickSort(array, i, right, comparer)
				);
			}
			else
			{
				if (j > left) { ParallelQuickSort(array, left, j, comparer); }
				if (i < right) { ParallelQuickSort(array, i, right, comparer); }
			}
		}
	}
}