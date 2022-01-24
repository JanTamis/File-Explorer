﻿using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Threading;
using FileExplorerCore.DisplayViews;
using FileExplorerCore.Helpers;
using FileExplorerCore.Interfaces;
using FileExplorerCore.Models;
using System.Diagnostics;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Runtime;
using System.Text.RegularExpressions;
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
				this.OnPropertyChanged(ref _sort, value);

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
				this.OnPropertyChanged(ref _count, value);
				this.OnPropertyChanged(nameof(FileCountText));
			}
		}

		public int FileCount
		{
			get => _fileCount;
			set
			{
				this.OnPropertyChanged(ref _fileCount, value);
				this.OnPropertyChanged(nameof(IsIndeterminate));
				this.OnPropertyChanged(nameof(SearchProgression));
				this.OnPropertyChanged(nameof(SearchText));
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
				var selectedFiles = Files.Where(x => x.IsSelected);

				if (SelectionCount > 0)
				{
					result = $"{SelectionCount:N0} items selected";

					if (!selectedFiles.Any(x => x.IsFolder))
					{
						var fileSize = selectedFiles.Where(x => !x.IsFolder)
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
			private set => this.OnPropertyChanged(ref _selectionCount, value);
		}

		public TimeSpan LoadTime => DateTime.Now - startSearchTime;

		public bool IsLoading
		{
			get => _isLoading;
			set
			{
				this.OnPropertyChanged(ref _isLoading, value);

				if (!IsLoading)
				{
					this.OnPropertyChanged(nameof(LoadTime));

					//TaskbarUtility.SetProgressState(TaskbarProgressBarStatus.NoProgress);
				}
				else
				{
					FileCount = Int32.MaxValue;

					//TaskbarUtility.SetProgressState(TaskbarProgressBarStatus.Indeterminate);
				}

				GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
				GC.Collect(2, GCCollectionMode.Forced, false, true);

				this.OnPropertyChanged(nameof(SearchFailed));
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
					return null;
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

					this.OnPropertyChanged(ref _path, value);
					this.OnPropertyChanged(nameof(FolderName));

					ThreadPool.QueueUserWorkItem(async x =>
					{
						await _folders.ClearTrim();
						await _folders.AddRange(GetFolders(), token: TokenSource.Token);

						IEnumerable<FolderModel> GetFolders()
						{
							var path = Path.Replace(System.IO.Path.AltDirectorySeparatorChar, System.IO.Path.DirectorySeparatorChar);
							var names = path.Split(System.IO.Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

							for (var i = 0; i < names.Length; i++)
							{
								var folderPath = String.Join(System.IO.Path.DirectorySeparatorChar,
									new ArraySegment<string>(names, 0, i + 1));
								var name = names[i];

								if (!String.IsNullOrEmpty(folderPath))
								{
									if (i == 0)
									{
										folderPath += System.IO.Path.DirectorySeparatorChar;
										name = $"{new DriveInfo(name).VolumeLabel} ({name}{System.IO.Path.DirectorySeparatorChar})";
									}


									yield return new FolderModel(folderPath, name, Directory.EnumerateDirectories(folderPath, "*", 
										new EnumerationOptions())
										.Where(Directory.Exists)
										.Select(directory => new FolderModel(directory, System.IO.Path.GetFileName(directory))));
								}
							}
						}
					});

					IsSearching = false;

					if (!String.IsNullOrEmpty(Path))
					{
						watcher = new FileSystemWatcher(Path)
						{
							IncludeSubdirectories = false
						};

						watcher.Renamed += (_, e) =>
						{
							var file = Files.FirstOrDefault(x => System.IO.Path.GetFileName(x.Path) == e.OldName);

							if (file != null)
							{
								file.Path = e.FullPath;
							}
						};

						watcher.EnableRaisingEvents = true;
					}
				}
			}
		}

		public string FolderName => System.IO.Path.GetFileName(Path);

		public string Search
		{
			get => _search;
			set => this.OnPropertyChanged(ref _search, value);
		}

		public Control DisplayControl
		{
			get => _displayControl;
			set => this.OnPropertyChanged(ref _displayControl, value);
		}

		public bool IsSearching { get; set; }

		public bool IsGrid
		{
			get => _isGrid;
			set
			{
				this.OnPropertyChanged(ref _isGrid, value);

				if (IsGrid is false)
				{
					FileModel.ImageSize = 32;

					foreach (var item in Files)
					{
						item.NeedsNewImage = true;
					}

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

					foreach (var item in Files)
					{
						item.NeedsNewImage = true;
					}

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
				this.OnPropertyChanged(ref _popupContent, value);

				if (PopupContent is not null)
				{
					PopupContent.OnClose += delegate { PopupContent = null; };
				}

				this.OnPropertyChanged(nameof(PopupVisible));
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

					this.OnPropertyChanged(nameof(SelectionText));
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

				this.OnPropertyChanged(nameof(SelectionText));
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
			await this.OnPropertyChanged(nameof(SelectionText));

			IsLoading = true;

			var timer = new System.Timers.Timer(1000);
			timer.Elapsed += async delegate { await OnPropertyChanged(nameof(SearchText)); };

			startSearchTime = DateTime.Now;

			timer.Start();

			if (Directory.Exists(Path))
			{
				await Task.Run(async () =>
				{
					var query = Sort is SortEnum.None && !recursive
						? GetDirectories(Path).Concat(GetFiles(Path))
						: GetFileSystemEntries(Path, search, recursive);

					if (Sort is not SortEnum.None)
					{
						ThreadPool.QueueUserWorkItem(x =>
						{
							options.RecurseSubdirectories = recursive;

							var count = GetFileSystemEntriesCount(Path, search, options, TokenSource.Token);

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

			timer.Stop();

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

			if (search is "*" or "*.*" or "" && Sort is SortEnum.None && !recursive)
			{
				return new FileSystemEnumerable<FileModel>(path, GetFileModel, options);
			}
			else
			{
				var query = FileSearcher.PrepareQuery(search);

				return new FileSystemEnumerable<FileModel>(path, GetFileModel, options)
				{
					ShouldIncludePredicate = (ref FileSystemEntry x) =>
						FileSystemName.MatchesSimpleExpression(search, x.FileName) || FileSearcher.IsValid(x, query)
				};
			}
		}

		private int GetFileSystemEntriesCount(string path, string search, EnumerationOptions options,
			CancellationToken token)
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

			var temp = path.Split('/');

			TreeItem<string> item = MainWindowViewModel.Tree.Children[0];

			for (int i = 1; i < temp.Length; i++)
			{
				if (item is not null)
				{
					item = item.EnumerateChildren(0).FirstOrDefault(f => f.Value == temp[i]);
				}
			}

			if (options.RecurseSubdirectories)
			{
				return item.EnumerateChildren().Count();
			}

			return item.GetChilrenCount();
		}

		private IEnumerable<FileModel> GetDirectories(string path)
		{
			return new FileSystemEnumerable<FileModel>(path, GetFileModel, options)
			{
				ShouldIncludePredicate = (ref FileSystemEntry x) => x.IsDirectory,
			};
		}

		private IEnumerable<FileModel> GetFiles(string path)
		{
			return new FileSystemEnumerable<FileModel>(path, GetFileModel, options)
			{
				ShouldIncludePredicate = (ref FileSystemEntry x) => !x.IsDirectory,
			};
		}

		public static FileModel GetFileModel(ref FileSystemEntry entry)
		{
			using var builder = new ValueStringBuilder(entry.Directory.Length + entry.FileName.Length + 1);

			builder.Append(entry.Directory);
			
			if (!System.IO.Path.EndsInDirectorySeparator(entry.Directory))
			{
				builder.Append(System.IO.Path.DirectorySeparatorChar);
			}
			
			builder.Append(entry.FileName);
			
			builder.Replace(System.IO.Path.AltDirectorySeparatorChar, System.IO.Path.DirectorySeparatorChar);

			return new FileModel(builder.AsSpan(), entry.IsDirectory);
		}
	}
}