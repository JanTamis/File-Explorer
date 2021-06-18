using Avalonia.Threading;
using JetBrains.Annotations;
using System;
using System.Collections.Concurrent;
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

		const int updateTime = 1000;
		const int updateCountTime = 100;

		public ObservableRangeCollection() : base()
		{
			base.CollectionChanged += delegate
			{
				CountChanged.Invoke(Count);
			};
		}

		/// <summary> 
		/// Adds the elements of the specified collection to the end of the ObservableCollection(Of T). 
		/// </summary> 
		public Task AddRange(IEnumerable<T> collection, CancellationToken token)
		{
			if (collection is null)
				throw new ArgumentNullException(nameof(collection));

			var timer = new System.Timers.Timer(updateTime);
			var isLoading = false;

			timer.Elapsed += async delegate
			{
				isLoading = true;

				await Dispatcher.UIThread.InvokeAsync(() => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)));

				isLoading = false;
			};

			timer.Start();

			foreach (var i in collection)
			{
				if (token.IsCancellationRequested)
					break;

				while (isLoading) { }

				Items.Add(i);
				CountChanged(Count);
			}

			timer.Stop();

			return Dispatcher.UIThread.InvokeAsync(() => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)));
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
			Task task = null;

			if (Items is List<T> list)
			{
				var watch = Stopwatch.StartNew();
				var countWatch = Stopwatch.StartNew();

				foreach (var item in collection)
				{
					if (token.IsCancellationRequested)
						break;

					if (comparer is not null)
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

					if (action is not null)
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
						await Dispatcher.UIThread.InvokeAsync(() => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)));

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

				await Dispatcher.UIThread.InvokeAsync(() => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)));
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

					Dispatcher.UIThread.InvokeAsync(() => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)));
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

		static void ParallelQuickSort(List<T> array, int left, int right, IComparer<T> comparer)
		{
			var Threshold = 50;
			var i = left;
			var j = right;
			var m = array[(left + right) / 2];

			while (i <= j)
			{
				while (comparer.Compare(array[i], m) == -1) { i++; }
				while (comparer.Compare(array[j], m) == 1) { j--; }

				if (i <= j)
				{
					var t = array[i]; 
					array[i] = array[j]; 
					array[j] = t;

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