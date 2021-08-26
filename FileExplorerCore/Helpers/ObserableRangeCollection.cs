using Avalonia.Threading;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;

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

		public ObservableRangeCollection() : base()
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
				await ReplaceRange(items, default);
			});
		}

		/// <summary> 
		/// Adds the elements of the specified collection to the end of the ObservableCollection(Of T). 
		/// </summary> 
		public void AddRange(IEnumerable<T> collection, CancellationToken token)
		{
			if (collection is null)
				throw new ArgumentNullException(nameof(collection));

			var timer = new System.Timers.Timer(updateTime);

			timer.Elapsed += delegate
			{
				if (Dispatcher.UIThread.CheckAccess())
				{
					OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
				}
				else
				{
					Dispatcher.UIThread.Post(() => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)));
				}
			};

			timer.Start();

			foreach (var i in collection)
			{
				if (token.IsCancellationRequested)
					break;

				Items.Add(i);
				CountChanged(Count);
			}

			timer.Stop();

			if (Dispatcher.UIThread.CheckAccess())
			{
				OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
			}
			else
			{
				Dispatcher.UIThread.Post(() => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)));
			}
		}

		/// <summary> 
		/// Removes the first occurence of each item in the specified collection from ObservableCollection(Of T). 
		/// </summary> 
		public void RemoveRange(IEnumerable<T> collection)
		{
			if (collection is null)
				throw new ArgumentNullException(nameof(collection));

			foreach (var i in collection)
				Items.Remove(i);

			OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		}

		/// <summary> 
		/// Clears the current collection and replaces it with the specified collection. 
		/// </summary> 
		public async Task ReplaceRange(IEnumerable<T> collection, CancellationToken token, IComparer<T>? comparer = null, Action<T>? action = null)
		{
			if (collection is null)
				throw new ArgumentNullException(nameof(collection));

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
						int i = list.BinarySearch(item, comparer);

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
							});

						}

						if (comparer == null)
						{
							if (Dispatcher.UIThread.CheckAccess())
							{
								OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, list, index));
								index = list.Count - 1;
							}
							else
							{
								await Dispatcher.UIThread.InvokeAsync(() => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, list, index)));
								index = list.Count - 1;
							}
						}
						else
						{
							if (Dispatcher.UIThread.CheckAccess())
							{
								OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
								index = list.Count - 1;
							}
							else
							{
								await Dispatcher.UIThread.InvokeAsync(() => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)));
								index = list.Count - 1;
							}
						}

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
					});
				}

				if (comparer == null)
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
						index = list.Count - 1;
					}
					else
					{
						await Dispatcher.UIThread.InvokeAsync(() => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)));
						index = list.Count - 1;
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