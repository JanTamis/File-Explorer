using System;
using System.Collections.Generic;
using Avalonia.Controls;
using FileExplorerCore.DisplayViews;
using FileExplorerCore.Helpers;
using FileExplorerCore.Interfaces;
using FileExplorerCore.Models;
using System.Diagnostics;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;
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

		private readonly Stack<string> undoStack = new();
		private readonly Stack<string> redoStack = new();
		readonly ObservableRangeCollection<FolderModel> _folders = new();

		public CancellationTokenSource TokenSource;
		private Control _displayControl;

		FileSystemWatcher watcher;

		private IPopup _popupContent;

		private readonly EnumerationOptions options = new()
		{
			IgnoreInaccessible = true,
			AttributesToSkip = FileAttributes.System | FileAttributes.Hidden,
		};

		private DateTime startSearchTime;

		private TimeSpan predictedTime;
		private TimeSpan previousLoadTime;

		private SortEnum _sort = SortEnum.None;

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
				else if (!String.IsNullOrWhiteSpace(Path))
				{
					UpdateFiles(false, "*");
				}
			}
		}

		public IEnumerable<FolderModel> Folders
		{
			get { return _folders; }
		}

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
					OnPropertyChanged(nameof(LoadTime));

					//TaskbarUtility.SetProgressState(TaskbarProgressBarStatus.NoProgress);
				}
				else
				{
					FileCount = Int32.MaxValue;

					//TaskbarUtility.SetProgressState(TaskbarProgressBarStatus.Indeterminate);
				}

				// GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
				GC.Collect(2, GCCollectionMode.Optimized, false);

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

					OnPropertyChanged(ref _path, value);
					OnPropertyChanged(nameof(FolderName));

					ThreadPool.QueueUserWorkItem(async x =>
					{
						await _folders.ClearTrim();
						await _folders.AddRange(GetFolders(), token: TokenSource.Token);

						IEnumerable<FolderModel> GetFolders()
						{
							var separator = OperatingSystem.IsWindows()
								? '\\'
								: '/';

							var path = Path;
							var names = path.Split(separator, StringSplitOptions.RemoveEmptyEntries);

							if (OperatingSystem.IsMacOS())
							{
								yield return new FolderModel(MainWindowViewModel.Tree[0]);
							}

							for (var i = 0; i < names.Length; i++)
							{
								var folderPath = String.Join(separator, new ArraySegment<string>(names, 0, i + 1));
								var name = names[i];

								if (!String.IsNullOrEmpty(folderPath))
								{
									if (i == 0 && OperatingSystem.IsWindows())
									{
										folderPath += separator;
										name = $"{new DriveInfo(name).VolumeLabel} ({name}{separator})";
									}

									yield return new FolderModel(GetTreeItem(folderPath));
								}
							}
						}
					});

					IsSearching = false;
				}
			}
		}

		public string FolderName => System.IO.Path.GetFileName(Path);

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

				if (IsGrid is false)
				{
					FileModel.ImageSize = 32;

					// foreach (var item in Files)
					// {
					// 	item.NeedsNewImage = true;
					// }

					var list = new FileDataGrid
					{
						Files = Files
					};

					list.PathChanged += async (path) => await SetPath(path);

					DisplayControl = list;
				}
				else
				{
					FileModel.ImageSize = 100;

					// foreach (var item in Files)
					// {
					// 	item.NeedsNewImage = true;
					// }

					var grid = new FileGrid
					{
						Files = Files
					};

					grid.PathChanged += async (path) => await SetPath(path);

					DisplayControl = grid;
				}
			}
		}

		public IPopup PopupContent
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
			Files.CountChanged += (count) => { Count = count; };

			Files.OnPropertyChanged += (property) =>
			{
				if (property is "IsSelected")
				{
					SelectionCount = Files.Count(x => x.IsSelected);

					OnPropertyChanged(nameof(SelectionText));
				}
			};

			Path = String.Empty;

			FileModel.SelectionChanged += (fileModel) =>
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
			if (TokenSource is { IsCancellationRequested: false })
			{
				IsLoading = false;
				TokenSource.Cancel();
			}
		}

		public async ValueTask UpdateFiles(bool recursive, string search)
		{
			FileModel.FileImageQueue.Clear();

			if (TokenSource is { })
			{
				TokenSource.Cancel();
			}

			options.RecurseSubdirectories = recursive;

			TokenSource = new CancellationTokenSource();
			previousLoadTime = TimeSpan.Zero;

			foreach (var file in Files)
			{
				if (file.HasImage)
				{
					file.Dispose();
				}
			}

			Files.Clear();
			Files.Trim();

			SelectionCount = 0;
			await OnPropertyChanged(nameof(SelectionText));

			IsLoading = true;

			//var timer = new System.Timers.Timer(1000);
			//timer.Elapsed += async delegate { await OnPropertyChanged(nameof(SearchText)); };

			//startSearchTime = DateTime.Now;

			//timer.Start();

			if (Directory.Exists(Path))
			{
				await Task.Run(async () =>
				{
					var query = Sort is SortEnum.None && !recursive
						? GetDirectories(Path).Concat(GetFiles(Path))
						: GetFileSystemEntries(Path, search, recursive);

					if (Sort is not SortEnum.None)
					{
						ThreadPool.QueueUserWorkItem(async x =>
						{
							options.RecurseSubdirectories = recursive;

							var count = await GetFileSystemEntriesCount(Path, search, options, TokenSource.Token);

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

		public async ValueTask SetPath(string path)
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
					catch (Exception)
					{
					}
				});
			}
			else if (Directory.Exists(path))
			{
				if (Path != path)
				{
					Path = path;
					await UpdateFiles(IsSearching, "*");
				}
			}
		}

		private IEnumerable<FileModel> GetFileSystemEntries(string path, string search, bool recursive)
		{
			options.RecurseSubdirectories = recursive;

			path = System.IO.Path.GetFullPath(path);

			var item = GetTreeItem(path);

			return item.EnumerateChildren()
				.Cast<FileSystemTreeItem>()
				.Where(w => w is not null && FileSystemName.MatchesSimpleExpression(search, w.Value))
				.Select(s => new FileModel(s));

			//if (search is "*" or "*.*" or "" && Sort is SortEnum.None && !recursive)
			//{
			//	return new FileSystemEnumerable<FileModel>(path, GetFileModel, options);
			//}

			//var query = FileSearcher.PrepareQuery(search);

			//return new FileSystemEnumerable<FileModel>(path, GetFileModel, options)
			//{
			//	ShouldIncludePredicate = (ref FileSystemEntry x) => FileSystemName.MatchesSimpleExpression(search, x.FileName) || FileSearcher.IsValid(x, query)
			//};
		}

		private async Task<int> GetFileSystemEntriesCount(string path, string search, EnumerationOptions options, CancellationToken token)
		{
			//FileSystemEnumerable<bool> enumerable;

			//if (search is "*" or "*.*" or "" && Sort is SortEnum.None && !options.RecurseSubdirectories)
			//{
			//	enumerable = new(path, null, options);
			//}
			//else
			//{
			//	var query = FileSearcher.PrepareQuery(search);
			//	var regex = new Wildcard(search, RegexOptions.IgnoreCase | RegexOptions.Compiled);

			//	enumerable = new(path, delegate { return false; }, options)
			//	{
			//		ShouldIncludePredicate = (ref FileSystemEntry x) => FileSystemName.MatchesSimpleExpression(search, x.FileName) || FileSearcher.IsValid(x, query)
			//	};
			//}

			//var count = 0;

			//foreach (var item in enumerable)
			//{
			//	if (token.IsCancellationRequested)
			//		break;

			//	count++;
			//}

			//return count;
			
			var item = GetTreeItem(path);

			return await item.GetChildrenCount();
		}

		public IEnumerable<FileModel> GetDirectories(string path)
		{
			//return new FileSystemEnumerable<FileModel>(path, GetFileModel, options)
			//{
			//	ShouldIncludePredicate = (ref FileSystemEntry x) => x.IsDirectory,
			//};

			FileSystemTreeItem item  = GetTreeItem(path);

			return item.EnumerateChildren(0)
				.Cast<FileSystemTreeItem>()
				.Where(w => w.IsFolder)
				.Select(s => new FileModel(s));
		}

		public IEnumerable<FileModel> GetFiles(string path)
		{
			//return new FileSystemEnumerable<FileModel>(path, GetFileModel, options)
			//{
			//	ShouldIncludePredicate = (ref FileSystemEntry x) => !x.IsDirectory,
			//};

			var item = GetTreeItem(path);

			return item.EnumerateChildren(0)
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
						return GetItem(child as FileSystemTreeItem, path, index + 1);
					}
				}

				return item;
			}
		}
	}
}