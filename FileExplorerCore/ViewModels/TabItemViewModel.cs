using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Enumeration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using FileExplorerCore.DisplayViews;
using FileExplorerCore.Helpers;
using FileExplorerCore.Interfaces;
using FileExplorerCore.Models;
using Humanizer;

namespace FileExplorerCore.ViewModels
{
	public class TabItemViewModel : ViewModelBase
	{
		private string _search = String.Empty;

		private int _count;
		private int _fileCount;
		private int _foundItems;
		private int _selectionCount;

		private bool _isUserEntered = true;
		private bool _isLoading;
		private bool _isGrid;

		private readonly Stack<FileSystemTreeItem?> _undoStack = new();
		private readonly Stack<FileSystemTreeItem?> _redoStack = new();

		public CancellationTokenSource? TokenSource;
		private Control _displayControl = new Quickstart();

		private IPopup? _popupContent;

		private DateTime _startSearchTime;

		private TimeSpan _predictedTime;
		private TimeSpan _previousLoadTime;

		private SortEnum _sort = SortEnum.None;
		private FileSystemTreeItem? _treeItem;

		public event Action PathChanged;

		public SortEnum Sort
		{
			get => _sort;
			set
			{
				OnPropertyChanged(ref _sort, value);

				if (!IsLoading && Files.Count > 1)
				{
					Files.Sort(new FileModelComparer(Sort));
				}
				else if (TreeItem is not null)
				{
					UpdateFiles(false, "*");
				}
			}
		}

		public IEnumerable<FolderModel> Folders => TreeItem?
			.EnumerateToRoot()
			.Reverse()
			.Select(s => new FolderModel(s)) ?? Enumerable.Empty<FolderModel>();

		public int Count
		{
			get => _count;
			set
			{
				OnPropertyChanged(ref _count, value);
				OnPropertyChanged(nameof(FileCountText));
			}
		}

		public int FileCount
		{
			get => _fileCount;
			set
			{
				OnPropertyChanged(ref _fileCount, value);

				OnPropertyChanged(nameof(IsIndeterminate));
				OnPropertyChanged(nameof(SearchProgression));
				//OnPropertyChanged(nameof(SearchText));
			}
		}

		public string FileCountText => Count switch
		{
			1 => "1 item",
			_ => $"{Count:N0} items",
		};

		public string SelectionText
		{
			get
			{
				var result = String.Empty;

				if (SelectionCount > 0)
				{
					result = $"{SelectionCount:N0} items selected";
					var selectedFiles = Files.Where(w => !w.IsFolder && w.IsSelected);

					var fileSize = -1L;

					foreach (var file in selectedFiles)
					{
						if (!file.IsFolder)
						{
							fileSize = file.Size;
						}
					}
					
					if (fileSize is not -1)
					{
						result += $", {fileSize.Bytes()}";
					}
				}

				return result;
			}
		}

		public int SelectionCount
		{
			get => _selectionCount;
			private set => OnPropertyChanged(ref _selectionCount, value);
		}

		public TimeSpan LoadTime => DateTime.Now - _startSearchTime;

		public bool IsLoading
		{
			get => _isLoading;
			set
			{
				OnPropertyChanged(ref _isLoading, value);

				if (IsLoading)
				{
					FileCount = -1;
				}
				else
				{
					// GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
					// GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, false, true);
				}

				OnPropertyChanged(nameof(SearchFailed));
			}
		}

		public bool IsIndeterminate => FileCount == -1;

		public double SearchProgression => Count / (double)FileCount;

		public string SearchText
		{
			get
			{
				if (IsIndeterminate || SearchProgression is 0 or Double.NaN)
				{
					return String.Empty;
				}

				var loadTime = LoadTime;
				_predictedTime = _predictedTime.Subtract(TimeSpan.FromSeconds(1));

				if ((loadTime - _previousLoadTime).TotalSeconds >= 3)
				{
					var deltaItems = Count - _foundItems;
					var remainingItems = FileCount - Count;
					var elapsed = loadTime - _previousLoadTime;

					_previousLoadTime = loadTime;

					if (deltaItems > 0)
					{
						_predictedTime = TimeSpan.FromSeconds(remainingItems / (double)deltaItems * elapsed.TotalSeconds);
					}

					_foundItems = Count;
				}

				return $"Remaining Time: {_predictedTime.Humanize()}";
			}
		}

		public bool SearchFailed => !IsLoading && Files.Count == 0 && DisplayControl is not Quickstart;

		public ObservableRangeCollection<FileModel> Files { get; } = new();

		public FileSystemTreeItem? TreeItem
		{
			get => _treeItem;
			set
			{
				OnPropertyChanged(ref _treeItem, value);

				PathChanged?.Invoke();

				OnPropertyChanged(nameof(FolderName));

				if (value is null && DisplayControl is not Quickstart)
				{
					DisplayControl = new Quickstart();
				}
				else
				{
					IsGrid = IsGrid;
				}

				if (_isUserEntered)
				{
					_undoStack.Push(TreeItem);
					_redoStack.Clear();
				}

				OnPropertyChanged(nameof(FolderName));

				if (TreeItem is not null)
				{
					OnPropertyChanged(nameof(Folders));
				}

				IsSearching = false;
			}
		}

		public string FolderName => TreeItem?.Value ?? String.Empty;

		public string Search
		{
			get => _search;
			set => OnPropertyChanged(ref _search, value);
		}

		public Control DisplayControl
		{
			get => _displayControl;
			set => OnPropertyChanged(ref _displayControl, value);
		}

		public bool IsSearching { get; set; }

		public bool IsGrid
		{
			get => _isGrid;
			set
			{
				OnPropertyChanged(ref _isGrid, value);

				if (IsGrid == false)
				{
					var list = new FileDataGrid
					{
						Files = Files,
					};

					list.PathChanged += path => SetPath(path);

					DisplayControl = list;
				}
				else
				{
					var grid = new FileGrid
					{
						Files = Files,
					};

					grid.PathChanged += path => SetPath(path);

					DisplayControl = grid;
				}
			}
		}

		public IPopup? PopupContent
		{
			get => _popupContent;
			set
			{
				OnPropertyChanged(ref _popupContent, value);

				if (PopupContent is not null)
				{
					PopupContent.OnClose += () => PopupContent = null;
				}

				OnPropertyChanged(nameof(PopupVisible));
			}
		}

		public bool PopupVisible => PopupContent is not null;

		public TabItemViewModel()
		{
			Files.CountChanged += count => { Count = count; };

			Files.OnPropertyChanged += property =>
			{
				if (property is "IsSelected")
				{
					SelectionCount = Files.Count(x => x.IsSelected);

					OnPropertyChanged(nameof(SelectionText));
				}
			};

			FileModel.SelectionChanged += fileModel =>
			{
				if (fileModel.IsSelected)
				{
					SelectionCount++;
				}
				else
				{
					SelectionCount--;
				}

				OnPropertyChanged(nameof(SelectionText));
			};
		}

		public FileSystemTreeItem? Undo()
		{
			if (_undoStack.TryPop(out var path))
			{
				_isUserEntered = false;

				_redoStack.Push(TreeItem);
			}

			return path;
		}

		public FileSystemTreeItem? Redo()
		{
			if (_redoStack.TryPop(out var path))
			{
				_isUserEntered = false;

				_undoStack.Push(TreeItem);
			}

			return path;
		}

		public void CancelUpdateFiles()
		{
			if (TokenSource is { IsCancellationRequested: false })
			{
				IsLoading = false;
				TokenSource.Cancel();
			}
		}

		public async ValueTask UpdateFiles(bool recursive, string search)
		{
			if (TreeItem is null)
			{
				return;
			}

			foreach (var file in Files)
			{
				file.IsVisible = false;
			}

			TokenSource?.Cancel();

			TokenSource = new CancellationTokenSource();
			_previousLoadTime = TimeSpan.Zero;

			await Files.ClearTrim();

			SelectionCount = 0;
			await OnPropertyChanged(nameof(SelectionText));

			IsLoading = true;

			//var timer = new System.Timers.Timer(1000);
			//timer.Elapsed += async delegate { await OnPropertyChanged(nameof(SearchText)); };

			//startSearchTime = DateTime.Now;

			//timer.Start();

			if (TreeItem.IsFolder)
			{
				await Task.Run(async () =>
				{
					var query = recursive
						? GetFileSystemEntriesRecursive(TreeItem, search)
						: GetFileSystemEntries(TreeItem);

					if (Sort is not SortEnum.None)
					{
						ThreadPool.QueueUserWorkItem( _ =>
						{
							var count = GetFileSystemEntriesCount(TreeItem, search, TokenSource.Token);

							if (!TokenSource.IsCancellationRequested)
							{
								FileCount = count;
							}
						});
					}

					if (recursive)
					{
						if (Sort is SortEnum.None)
						{
							await Files.AddRange<Comparer<FileModel>>(query, token: TokenSource.Token);
						}
						else
						{
							var comparer = new FileModelComparer(Sort);

							await Files.AddRange(query, comparer, token: TokenSource.Token);
						}
					}
					else
					{
						var comparer = new FileModelComparer(Sort);

						await Files.AddRange(query, comparer, token: TokenSource.Token);
					}
				});
			}

			//timer.Stop();

			IsLoading = false;
		}

		public async ValueTask SetPath(FileSystemTreeItem? path)
		{
			if (path is null)
			{
				TreeItem = path;
				DisplayControl = new Quickstart();
				
				return;
			}

			if (!path.IsFolder)
			{
				await Task.Run(() =>
				{
					try
					{
						var info = new ProcessStartInfo
						{
							FileName = path.GetPath(x => x.ToString()),
							UseShellExecute = true,
						};

						Process.Start(info);
					}
					catch (Exception)
					{
					}
				});
			}
			else
			{
				FileSystemTreeItem item = null;

				foreach (var parent in path.EnumerateToRoot().Reverse())
				{
					item = new FileSystemTreeItem(parent.Value, true, item);
				}

				TreeItem = item;
				await UpdateFiles(false, "*");
			}
		}

		private IEnumerable<FileModel> GetFileSystemEntriesRecursive(FileSystemTreeItem path, string search)
		{
			return path
				// .EnumerateChildren((ref FileSystemEntry file) => !file.IsHidden && FileSystemName.MatchesSimpleExpression(search, file.FileName)) //|| (!file.IsDirectory && File.ReadLines(file.ToSpecifiedFullPath()).Any(a => a.Contains(search))))
				.EnumerateChildren()
				.Where(w => FileSystemName.MatchesSimpleExpression(search, w.Value))
				.Select(s => new FileModel(s));
		}

		private IEnumerable<FileModel> GetFileSystemEntries(FileSystemTreeItem path)
		{
			return path
				.EnumerateChildren((ref FileSystemEntry file) => !file.IsHidden, 0)
				.Select(s => new FileModel(s));
		}

		private int GetFileSystemEntriesCount(FileSystemTreeItem path, string search, CancellationToken token)
		{
			return path.GetChildrenCount();
		}
	}
}