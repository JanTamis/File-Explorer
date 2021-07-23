using Avalonia.Controls;
using FileExplorerCore.Converters;
using FileExplorerCore.DisplayViews;
using FileExplorerCore.Helpers;
using FileExplorerCore.Interfaces;
using FileExplorerCore.Models;
using NetFabric.Hyperlinq;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Runtime;
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
		private int foundItems;
		private int _selectionCount;

		private bool isUserEntered = true;
		private bool _isLoading;
		private bool _isGrid;

		private readonly Stack<string> undoStack = new();
		private readonly Stack<string> redoStack = new();

		private CancellationTokenSource tokenSource;
		private Control _displayControl;

		private IPopup _popupContent;

		private readonly EnumerationOptions options = new()
		{
			IgnoreInaccessible = true,
			AttributesToSkip = FileAttributes.System,
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
				this.RaiseAndSetIfChanged(ref _sort, value);

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
			get
			{
				var path = Path.Replace(System.IO.Path.AltDirectorySeparatorChar, System.IO.Path.DirectorySeparatorChar);

				var names = path.Split(System.IO.Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

				for (int i = 0; i < names.Length; i++)
				{
					var folderPath = String.Join(System.IO.Path.DirectorySeparatorChar, new ArraySegment<string>(names, 0, i + 1));
					var name = names[i];

					if (!String.IsNullOrEmpty(folderPath))
					{
						if (i is 0)
						{
							folderPath += System.IO.Path.DirectorySeparatorChar;
							name += System.IO.Path.DirectorySeparatorChar;
						}

						yield return new FolderModel(folderPath, name, from directory in Directory.EnumerateDirectories(folderPath, "*", new EnumerationOptions())
																													 select new FolderModel(directory, System.IO.Path.GetFileName(directory)));
					}
				}
			}
		}
		public int Count
		{
			get => _count;
			set
			{
				this.RaiseAndSetIfChanged(ref _count, value);
				this.RaisePropertyChanged(nameof(FileCountText));
			}
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

		public string FileCountText => Count switch
		{
			1 => "1 item",
			_ => $"{Count:N0} items"
		};

		public string SelectionText
		{
			get
			{
				var result = String.Empty;
				var selectedFiles = Files.AsValueEnumerable()
																 .Where(x => x.IsSelected);

				if (SelectionCount > 0)
				{
					result = $"{SelectionCount:N0} items selected";

					if (!selectedFiles.Any(x => x.IsFolder))
					{
						var fileSize = selectedFiles.Where(x => !x.IsFolder)
																				.Sum(s => s.Size);

						result += $", {SizeConverter.ByteSize(fileSize)}";
					}
				}

				return result;
			}
		}

		public int SelectionCount
		{
			get => _selectionCount;
			private set => this.RaiseAndSetIfChanged(ref _selectionCount, value);
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
				if (IsIndeterminate || Double.IsNaN(SearchProgression) || SearchProgression == 0)
				{
					return "Almost There...";
				}
				else
				{
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

					this.RaiseAndSetIfChanged(ref _path, value);
					this.RaisePropertyChanged(nameof(FolderName));
					this.RaisePropertyChanged(nameof(Folders));
				}
			}
		}

		public string FolderName => System.IO.Path.GetFileName(Path);

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

				GC.Collect(2, GCCollectionMode.Forced, false, true);
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

		public TabItemViewModel()
		{
			Files.CountChanged += (count) =>
			{
				Count = count;
			};

			Files.OnPropertyChanged += (property) =>
			{
				if (property is "IsSelected")
				{
					SelectionCount = Files.AsValueEnumerable()
																.Count(x => x.IsSelected);

					this.RaisePropertyChanged(nameof(SelectionText));
				}
			};

			Path = String.Empty;

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

			options.RecurseSubdirectories = recursive;

			tokenSource = new CancellationTokenSource();
			previousLoadTime = TimeSpan.Zero;

			Files.ClearTrim();

			GC.Collect(2, GCCollectionMode.Forced, false, true);

			SelectionCount = 0;
			this.RaisePropertyChanged(nameof(SelectionText));

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

					if (Sort is not SortEnum.None)
					{
						ThreadPool.QueueUserWorkItem(x =>
						{
							options.RecurseSubdirectories = recursive;

							var count = GetFileSystemEntriesCount(Path, search, options, tokenSource.Token);

							if (!tokenSource.IsCancellationRequested)
							{
								FileCount = count;
							}
						});
					}

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

			timer.Stop();

			IsLoading = false;

			GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;

			GC.Collect(2, GCCollectionMode.Optimized, false, true);
		}

		public async Task SetPath(string path)
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
				if (Path != path)
				{
					Path = path;
					await UpdateFiles(false, "*");
				}
			}
		}

		private IEnumerable<FileModel> GetFileSystemEntries(string path, string search, bool recursive)
		{
			options.RecurseSubdirectories = recursive;

			var size = IsGrid ? 100 : 32;

			if (search is "*" or "*.*" or "" && Sort is SortEnum.None && !recursive)
			{
				return new FileSystemEnumerable<FileModel>(path, (ref FileSystemEntry x) => new FileModel(x.ToFullPath(), x.IsDirectory, size), options);
			}
			else
			{
				var query = FileSearcher.PrepareQuery(search);
				var regex = new Wildcard(search, RegexOptions.Singleline | RegexOptions.Compiled);

				return new FileSystemEnumerable<FileModel>(path, (ref FileSystemEntry x) => new FileModel(x.ToFullPath(), x.IsDirectory, size), options)
				{
					ShouldIncludePredicate = (ref FileSystemEntry x) => regex.IsMatch(new String(x.FileName)) || FileSearcher.IsValid(x, query)
				};
			}

			//if (readers.TryGetValue(System.IO.Path.GetPathRoot(path), out var reader))
			//{
			//	return reader.GetNodes(path).Select(x => new FileModel(x.FullName, Directory.Exists(x.FullName), size));
			//}

			//return Enumerable.Empty<FileModel>();
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