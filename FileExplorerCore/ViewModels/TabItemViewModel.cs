using Avalonia.Controls;
using Avalonia.Threading;
using FileExplorerCore.DisplayViews;
using FileExplorerCore.Helpers;
using FileExplorerCore.Interfaces;
using FileExplorerCore.Models;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Reactive;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace FileExplorerCore.ViewModels
{
	public class TabItemViewModel : ViewModelBase
	{
		private string _path;
		private string? _search;

		private int _count;
		private int _fileCount;

		private bool _isLoading;
		private bool isUserEntered = true;
		private bool _isGrid;

		private readonly Stack<string> undoStack = new();
		private readonly Stack<string> redoStack = new();

		private CancellationTokenSource tokenSource;
		private Control _displayControl;

		private IPopup _popupContent;

		private readonly EnumerationOptions options = new()
		{
			IgnoreInaccessible = true,
			BufferSize = 16384,
			AttributesToSkip = FileAttributes.Hidden,
		};

		private DateTime startSearchTime;

		private TimeSpan predictedTime;
		private TimeSpan previousLoadTime;

		private int foundItems;

		public ReactiveCommand<Unit, Unit> RemoveTabCommand;

		private SortEnum _sort = SortEnum.None;

		public SortEnum Sort
		{
			get => _sort;
			set
			{
				this.RaiseAndSetIfChanged(ref _sort, value);

				if (!IsLoading && Files.Count > 1)
				{
					Files.Sort(new FileModelComparer(Sort));
				}
				else
				{
					if (!String.IsNullOrWhiteSpace(Path))
					{
						UpdateFiles(false, "*");
					}
				}
			}
		}

		public int Count
		{
			get => _count;
			set => this.RaiseAndSetIfChanged(ref _count, value);
		}

		public int FileCount
		{
			get => _fileCount;
			set
			{
				this.RaiseAndSetIfChanged(ref _fileCount, value);
				this.RaisePropertyChanged(nameof(IsIndeterminate));
				this.RaisePropertyChanged(nameof(SearchProgression));
				this.RaisePropertyChanged(nameof(SearchText));
			}
		}

		public TimeSpan LoadTime => DateTime.Now - startSearchTime;

		public bool IsLoading
		{
			get => _isLoading;
			set
			{
				this.RaiseAndSetIfChanged(ref _isLoading, value);

				if (!IsLoading)
				{
					this.RaisePropertyChanged(nameof(LoadTime));

					TaskbarUtility.SetProgressState(TaskbarProgressBarStatus.NoProgress);
				}
				else
				{
					FileCount = Int32.MaxValue;

					TaskbarUtility.SetProgressState(TaskbarProgressBarStatus.Indeterminate);
				}

				this.RaisePropertyChanged(nameof(SearchFailed));
			}
		}

		public bool IsIndeterminate => FileCount == Int32.MaxValue;

		public double SearchProgression => Count / (double)FileCount;

		public string SearchText
		{
			get
			{
				if (IsIndeterminate || Double.IsNaN(SearchProgression) || SearchProgression is 0)
				{
					return "Almost There...";
				}
				else
				{
					var loadTime = LoadTime;
					predictedTime = predictedTime.Subtract(TimeSpan.FromSeconds(1));

					if ((loadTime - previousLoadTime).TotalSeconds >= 5)
					{
						var deltaItems = Count - foundItems;
						var remainingItems = FileCount - Count;
						var elapsed = loadTime - previousLoadTime;

						previousLoadTime = loadTime;

						if (deltaItems > 0)
						{
							predictedTime = TimeSpan.FromSeconds(remainingItems / (double)deltaItems * elapsed.TotalSeconds);
						}

						foundItems = Count;
					}

					TaskbarUtility.SetProgressState(TaskbarProgressBarStatus.Normal);
					TaskbarUtility.SetProgressValue((int)(SearchProgression * Int32.MaxValue), Int32.MaxValue);

					return $"Remaining Time: {predictedTime:hh\\:mm\\:ss}";
				}
			}
		}

		public bool SearchFailed => !IsLoading && Files.Count == 0 && DisplayControl is not Quickstart;

		public ObservableRangeCollection<FileModel> Files { get; set; } = new();

		public string Path
		{
			get => _path;
			set
			{
				if (value != Path && (Directory.Exists(value) || String.IsNullOrEmpty(value)))
				{
					if (value is "" && DisplayControl is not Quickstart)
					{
						DisplayControl = new Quickstart();
					}
					else
					{
						IsGrid = IsGrid;
					}

					if (isUserEntered)
					{
						undoStack.Push(Path);
						redoStack.Clear();
					}
				}
				this.RaiseAndSetIfChanged(ref _path, value);
			}
		}

		public string? Search
		{
			get => _search;
			set => this.RaiseAndSetIfChanged(ref _search, value);
		}

		public Control DisplayControl
		{
			get => _displayControl;
			set => this.RaiseAndSetIfChanged(ref _displayControl, value);
		}

		public bool IsGrid
		{
			get => _isGrid;
			set
			{
				this.RaiseAndSetIfChanged(ref _isGrid, value);

				if (IsGrid is false)
				{
					foreach (var item in Files)
					{
						item.ImageSize = 32;
					}

					var list = new FileDataGrid
					{
						Files = Files
					};

					list.PathChanged += (path) => SetPath(path);

					DisplayControl = list;
				}
				else
				{
					foreach (var item in Files)
					{
						item.ImageSize = 100;
					}

					var grid = new FileGrid
					{
						Files = Files
					};

					grid.PathChanged += (path) => SetPath(path);

					DisplayControl = grid;
				}
			}
		}

		public IPopup PopupContent
		{
			get => _popupContent;
			set
			{
				this.RaiseAndSetIfChanged(ref _popupContent, value);

				if (PopupContent is not null)
				{
					PopupContent.OnClose += delegate
					{
						PopupContent = null;
					};
				}

				this.RaisePropertyChanged(nameof(PopupVisible));
			}
		}

		public bool PopupVisible => PopupContent is not null;


		public TabItemViewModel(ReactiveCommand<Unit, Unit> removeCommand)
		{
			Files.CountChanged += (count) =>
			{
				Count = count;
			};

			Path = String.Empty;

			UpdateFiles(false, "*");

			RemoveTabCommand = removeCommand;
		}

		public string? Undo()
		{
			if (undoStack.TryPop(out var path))
			{
				isUserEntered = false;

				redoStack.Push(Path);
			}

			return path;
		}

		public string? Redo()
		{
			if (redoStack.TryPop(out var path))
			{
				isUserEntered = false;

				undoStack.Push(Path);
			}

			return path;
		}

		public void CancelUpdateFiles()
		{
			if (tokenSource is { IsCancellationRequested: false })
			{
				tokenSource.Cancel();
			}
		}

		public async Task UpdateFiles(bool recursive, string search)
		{
			FileModel.FileImageQueue.Clear();

			if (tokenSource is { })
			{
				tokenSource.Cancel();
			}

			tokenSource = new CancellationTokenSource();
			previousLoadTime = TimeSpan.Zero;

			await Dispatcher.UIThread.InvokeAsync(Files.Clear);

			IsLoading = true;

			var timer = new System.Timers.Timer(1000);
			timer.Elapsed += delegate
			{
				this.RaisePropertyChanged(nameof(SearchText));
			};

			startSearchTime = DateTime.Now;

			timer.Start();

			if (Directory.Exists(Path))
			{
				await Task.Run(async () =>
				{
					var query = Sort is SortEnum.None && !recursive ? GetDirectories(Path).Concat(GetFiles(Path)) : GetFileSystemEntries(Path, search, recursive);

					ThreadPool.QueueUserWorkItem(x =>
					{
						options.RecurseSubdirectories = recursive;

						var watch = Stopwatch.StartNew();
						var count = GetFileSystemEntriesCount(Path, search, options, tokenSource.Token);

						watch.Stop();

						if (!tokenSource.IsCancellationRequested)
						{
							FileCount = count;
						}
					});

					if (Sort is SortEnum.None)
					{
						await Files.ReplaceRange(query, tokenSource.Token);
					}
					else
					{
						var comparer = new FileModelComparer(Sort);

						await Files.ReplaceRange(query, tokenSource.Token, comparer);
					}
				});
			}
			//else
			//{
			//	var drives = DriveInfo.GetDrives();
			//	var files = drives.Where(x => x.IsReady)
			//										.Select(s => new FileModel(s.RootDirectory.FullName, false));

			//	await Files.ReplaceRange(files, tokenSource.Token);
			//}

			timer.Stop();

			IsLoading = false;
		}

		private async Task SetPath(string path)
		{
			if (File.Exists(path))
			{
				await Task.Run(() =>
				{
					try
					{
						var info = new ProcessStartInfo
						{
							FileName = path,
							UseShellExecute = true,
						};

						Process.Start(info);
					}
					catch (Exception) { }
				});
			}
			else if (Directory.Exists(path))
			{
				Path = path;
				await UpdateFiles(false, "*");
			}
		}

		private IEnumerable<FileModel> GetFileSystemEntries(string path, string search, bool recursive)
		{
			options.RecurseSubdirectories = recursive;

			var size = IsGrid ? 100 : 32;

			if (search is "*" or "*.*" or "" && Sort is SortEnum.None && !options.RecurseSubdirectories)
			{
				return new FileSystemEnumerable<FileModel>(path, (ref FileSystemEntry x) => new FileModel(x.ToSpecifiedFullPath(), x.IsDirectory, size), options);
			}
			else
			{
				var query = FileSearcher.PrepareQuery(search);
				var regex = new Wildcard(search, RegexOptions.Singleline | RegexOptions.Compiled);

				return new FileSystemEnumerable<FileModel>(path, (ref FileSystemEntry x) => new FileModel(x.ToSpecifiedFullPath(), x.IsDirectory, size), options)
				{
					ShouldIncludePredicate = (ref FileSystemEntry x) => regex.IsMatch(new String(x.FileName)) || FileSearcher.IsValid(x, query)
				};
			}
		}

		private int GetFileSystemEntriesCount(string path, string search, EnumerationOptions options, CancellationToken token)
		{
			var count = 0;
			FileSystemEnumerable<byte> enumerable;

			if (search is "*" or "*.*" or "" && Sort is SortEnum.None && !options.RecurseSubdirectories)
			{
				enumerable = new FileSystemEnumerable<byte>(path, (ref FileSystemEntry x) => 0, options);
			}
			else
			{
				var query = FileSearcher.PrepareQuery(search);
				var regex = new Wildcard(search, RegexOptions.IgnoreCase | RegexOptions.Compiled);

				enumerable = new FileSystemEnumerable<byte>(path, (ref FileSystemEntry x) => 0, options)
				{
					ShouldIncludePredicate = (ref FileSystemEntry x) => regex.IsMatch(new String(x.FileName)) || FileSearcher.IsValid(x, query)
				};
			}

			foreach (var item in enumerable)
			{
				if (token.IsCancellationRequested)
					break;

				count++;
			}

			return count;
		}

		private IEnumerable<FileModel> GetDirectories(string path)
		{
			var size = IsGrid ? 100 : 32;

			return new FileSystemEnumerable<FileModel>(path, (ref FileSystemEntry x) => new FileModel(x.ToFullPath(), true, size), options)
			{
				ShouldIncludePredicate = (ref FileSystemEntry x) => x.IsDirectory,
			};
		}

		private IEnumerable<FileModel> GetFiles(string path)
		{
			var size = IsGrid ? 100 : 32;

			return new FileSystemEnumerable<FileModel>(path, (ref FileSystemEntry x) => new FileModel(x.ToFullPath(), false, size), options)
			{
				ShouldIncludePredicate = (ref FileSystemEntry x) => !x.IsDirectory,
			};
		}
	}
}