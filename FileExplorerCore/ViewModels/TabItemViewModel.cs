using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Runtime;
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
		private string _path;
		private string _search = String.Empty;

		private int _count;
		private int _fileCount;
		private int foundItems;
		private int _selectionCount;

		private bool isUserEntered = true;
		private bool _isLoading;
		private bool _isGrid;

		private readonly Stack<FileSystemTreeItem> undoStack = new();
		private readonly Stack<FileSystemTreeItem> redoStack = new();
		readonly ObservableRangeCollection<FolderModel> _folders = new();

		public CancellationTokenSource TokenSource;
		private Control _displayControl = new Quickstart();

		FileSystemWatcher watcher;

		private IPopup _popupContent;

		private DateTime startSearchTime;

		private TimeSpan predictedTime;
		private TimeSpan previousLoadTime;

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

		public IEnumerable<FolderModel> Folders => _folders;

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

				if (Sort is SortEnum.None)
				{
					OnPropertyChanged(nameof(IsIndeterminate));
					OnPropertyChanged(nameof(SearchProgression));
				}
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
				var selectedFiles = Files.Where(x => x.IsSelected);

				if (SelectionCount > 0)
				{
					result = $"{SelectionCount:N0} items selected";

					if (!selectedFiles.Any(x => x.IsFolder))
					{
						var fileSize = selectedFiles
							.Where(x => !x.IsFolder)
							.Sum(s => s.Size);

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

		public TimeSpan LoadTime => DateTime.Now - startSearchTime;

		public bool IsLoading
		{
			get => _isLoading;
			set
			{
				OnPropertyChanged(ref _isLoading, value);

				if (!IsLoading)
				{
					//OnPropertyChanged(nameof(LoadTime));

					//TaskbarUtility.SetProgressState(TaskbarProgressBarStatus.NoProgress);
				}
				else
				{
					FileCount = Int32.MaxValue;

					//TaskbarUtility.SetProgressState(TaskbarProgressBarStatus.Indeterminate);
				}

				GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, false, true);

				OnPropertyChanged(nameof(SearchFailed));
			}
		}

		public bool IsIndeterminate => FileCount == Int32.MaxValue;

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
				predictedTime = predictedTime.Subtract(TimeSpan.FromSeconds(1));

				if ((loadTime - previousLoadTime).TotalSeconds >= 3)
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

				if (isUserEntered)
				{
					undoStack.Push(TreeItem);
					redoStack.Clear();
				}

				OnPropertyChanged(nameof(FolderName));

				ThreadPool.QueueUserWorkItem(async x =>
				{
					await _folders.ClearTrim();

					if (TreeItem is not null)
					{
						await _folders.AddRange(GetFolders(), token: TokenSource.Token);
					}

					IEnumerable<FolderModel> GetFolders()
					{
						return TreeItem.EnumerateToRoot()
							.Cast<FileSystemTreeItem>()
							.Select(s => new FolderModel(s))
							.Reverse();
					}
				});

				IsSearching = false;
			}
		}

		public string FolderName => TreeItem.Value;

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
					PopupContent.OnClose += delegate { PopupContent = null; };
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

			UpdateFiles(false, "*");
		}

		public FileSystemTreeItem? Undo()
		{
			if (undoStack.TryPop(out var path))
			{
				isUserEntered = false;

				redoStack.Push(TreeItem);
			}

			return path;
		}

		public FileSystemTreeItem? Redo()
		{
			if (redoStack.TryPop(out var path))
			{
				isUserEntered = false;

				undoStack.Push(TreeItem);
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
			
			if (TokenSource is not null)
			{
				TokenSource.Cancel();
			}

			TokenSource = new CancellationTokenSource();
			previousLoadTime = TimeSpan.Zero;

			Files.Clear();
			Files.Trim();

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
					var query = Sort is SortEnum.None && !recursive
						? GetDirectories(TreeItem).Concat(GetFiles(TreeItem))
						: GetFileSystemEntries(TreeItem, search, recursive);

					if (Sort is not SortEnum.None)
					{
						ThreadPool.QueueUserWorkItem(async _ =>
						{
							var count = await GetFileSystemEntriesCount(TreeItem, search, TokenSource.Token);

							if (!TokenSource.IsCancellationRequested)
							{
								FileCount = count;
							}
						});
					}

					if (Sort is SortEnum.None)
					{
						await Files.AddRange(query, token: TokenSource.Token);
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
				if (TreeItem != path)
				{
					TreeItem = path;
					await UpdateFiles(IsSearching, "*");
				}
			}
		}

		private IEnumerable<FileModel> GetFileSystemEntries(FileSystemTreeItem path, string search, bool recursive)
		{
			return path
				.EnumerateChildren()
				.Cast<FileSystemTreeItem>()
				.Where(w => w is not null && FileSystemName.MatchesSimpleExpression(search, w.Value))
				.Select(s => new FileModel(s));
		}

		private async Task<int> GetFileSystemEntriesCount(FileSystemTreeItem path, string search, CancellationToken token)
		{
			return await path.GetChildrenCount();
		}

		public IEnumerable<FileModel> GetDirectories(FileSystemTreeItem path)
		{
			return path
				.EnumerateChildren(0)
				.Cast<FileSystemTreeItem>()
				.Where(w => w.IsFolder)
				.Select(s => new FileModel(s));
		}

		public IEnumerable<FileModel> GetFiles(FileSystemTreeItem path)
		{
			return path
				.EnumerateChildren(0)
				.Cast<FileSystemTreeItem>()
				.Where(w => !w.IsFolder)
				.Select(s => new FileModel(s));
		}

		public static FileSystemTreeItem GetTreeItem(string path)
		{
			string[] temp = null;
			FileSystemTreeItem item = null;

			if (OperatingSystem.IsMacOS())
			{
				item = MainWindowViewModel.Tree.Children[0];
				temp = path.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
			}
			else if (OperatingSystem.IsWindows())
			{
				temp = path.Split('\\', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

				foreach (var child in MainWindowViewModel.Tree.EnumerateChildren(0))
				{
					if (child.Value.StartsWith(path[0]))
					{
						item = child;
						break;
					}
				}
			}

			foreach (var split in temp)
			{
				foreach (FileSystemTreeItem child in item.EnumerateChildren(0))
				{
					if (child is not null && child.Value == split)
					{
						item = child;
						break;
					}
				}
			}

			if (temp.Length > 0)
			{
				item = GetItem(item, temp, 1);
			}

			return item;

			static FileSystemTreeItem GetItem(FileSystemTreeItem item, string[] path, int index)
			{
				if (index == path.Length)
				{
					return item;
				}

				foreach (var child in item.Children)
				{
					if (child.Value == path[index])
					{
						return GetItem(child, path, index + 1);
					}
				}

				return item;
			}
		}
	}
}