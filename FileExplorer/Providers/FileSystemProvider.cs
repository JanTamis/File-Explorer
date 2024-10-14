using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Avalonia.Media;
using FileExplorer.Core.Interfaces;
using FileExplorer.Helpers;
using FileExplorer.Models;
using System.IO;
using System.IO.Enumeration;
using System.Numerics;
using System.Text;
using Avalonia.Svg.Skia;
using DialogHostAvalonia;
using FileExplorer.Core.Extensions;
using FileExplorer.Core.Models;
using FileExplorer.DisplayViews;
using FileExplorer.Interfaces;
using FileExplorer.Popup;
using FileExplorer.Providers.FileSystemMenuItems;
using FileExplorer.Resources;
using Humanizer;
using Humanizer.Localisation;
using Material.Icons;
using Directory = System.IO.Directory;
using File = System.IO.File;
using FileSystemTreeItem = FileExplorer.Models.FileSystemTreeItem;

namespace FileExplorer.Providers;

public sealed class FileSystemProvider : IItemProvider
{
	// private readonly MemoryCache? _imageCache = new(new MemoryCacheOptions
	// {
	// 	ExpirationScanFrequency = TimeSpan.FromMinutes(1)
	// });

	private IEnumerable<IFileItem> _clipboard = Enumerable.Empty<IFileItem>();

	private static readonly CompositeFormat DeleteFormat = CompositeFormat.Parse(ResourceDefault.DeleteTextformat);

	public IAsyncEnumerable<IFileItem> GetItemsAsync(IFileItem folder, string filter, bool recursive, CancellationToken token)
	{
		return GetItems(folder, filter, recursive, token)
			.ToAsyncEnumerable();
	}

	public IEnumerable<IFileItem> GetItems(IFileItem folder, string filter, bool recursive, CancellationToken token)
	{
		if (folder is FileModel model)
		{
			return GetEnumerable(model.TreeItem, filter, recursive)
				.Select(s => new FileModel(s));
		}

		return Enumerable.Empty<IFileItem>();
	}

	public bool HasItems(IFileItem folder)
	{
		return folder is FileModel { TreeItem.HasChildren: true, };
	}

	public ValueTask<IEnumerable<IPathSegment>> GetPathAsync(IFileItem folder)
	{
		if (folder is FileModel { TreeItem: not null, } file)
		{
			return ValueTask.FromResult(file.TreeItem
				.EnumerateToRoot()
				.Reverse()
				.Select(s => new FolderModel(s))
				.OfType<IPathSegment>());
		}

		return ValueTask.FromException<IEnumerable<IPathSegment>>(new ArgumentException("Provide a valid folder that is created from this provider"));
	}

	public ValueTask<IFileItem?> GetParentAsync(IFileItem folder, CancellationToken token)
	{
		if (folder is FileModel { TreeItem.Parent: { } parent })
		{
			return new ValueTask<IFileItem?>(new FileModel(parent));
		}

		return new ValueTask<IFileItem?>(null as IFileItem);
	}

	public async Task<IImage?> GetThumbnailAsync(IFileItem? item, int size, CancellationToken token)
	{
		if (item is null or not FileModel)
		{
			return null;
		}

		// if (_imageCache is not null)
		// {
		// 	return await _imageCache.GetOrCreateAsync(item.GetHashCode(), async entry =>
		// 	{
		// 		var image = await ThumbnailProvider.GetFileImage(item, this, size, () => !token.IsCancellationRequested && item.IsVisible);
		//
		// 		if (image is not null)
		// 		{
		// 			entry.SetSize((long)image.Size.Width * (long)image.Size.Height * 4L);
		// 		}
		//
		// 		return image;
		// 	});
		// }

		return await ThumbnailProvider.GetFileImage(item, this, size, () => !token.IsCancellationRequested && item.IsVisible);
	}

	public async Task EnumerateItemsAsync(IFileItem folder, string pattern, Action<IFileItem> action, CancellationToken token)
	{
		if (folder is FileModel model)
		{
			var bag = new ConcurrentQueue<Task>();
			bag.Enqueue(Run(bag, model.TreeItem, pattern, action, token));

			while (bag.TryDequeue(out var task))
			{
				try
				{
					await task;
				}
				catch (Exception e)
				{
				}
			}
		}

		return;

		Task Run(ConcurrentQueue<Task> bag, FileSystemTreeItem folder, string pattern, Action<FileModel> action, CancellationToken token)
		{
			return Runner.RunPrimary(() =>
			{
				using var enumerable = new DelegateFileSystemEnumerator<FileSystemTreeItem>(folder.GetPath(), FileSystemTreeItem.Options)
				{
					Transformation = (ref FileSystemEntry entry) => new FileSystemTreeItem(entry.FileName, entry.IsDirectory, folder),
					Find = (ref FileSystemEntry entry) => entry.IsDirectory || FileSystemName.MatchesSimpleExpression(pattern, entry.FileName) || IsCategory(entry.FileName, pattern)
				};

				while (enumerable.MoveNext() && !token.IsCancellationRequested)
				{
					var item = enumerable.Current;

					if (item.IsFolder)
					{
						bag.Enqueue(Run(bag, item, pattern, action, token));

						if (FileSystemName.MatchesSimpleExpression(pattern, item.Value) || IsCategory(item.Value, pattern))
						{
							action(new FileModel(item));
						}
					}
					else
					{
						action(new FileModel(item));
					}
				}
			}, token);
		}
	}

	public async Task EnumerateItemsAsync<T>(IFileItem folder, string pattern, Action<IEnumerable<T>> action, Func<IFileItem, T> transformation, CancellationToken token)
	{
		if (folder is FileModel model)
		{
			var bag = new ConcurrentQueue<Task>();
			bag.Enqueue(Run(bag, model.TreeItem, pattern, action, transformation, token));

			try
			{
				while (!token.IsCancellationRequested && bag.TryDequeue(out var task))
				{
					await task;
				}
			}
			catch (Exception)
			{
			}
		}

		return;

		async Task Run(ConcurrentQueue<Task> bag, FileSystemTreeItem folder, string pattern, Action<IEnumerable<T>> action, Func<IFileItem, T> transformation, CancellationToken token)
		{
			var result = await Runner.RunPrimary(() =>
			{
				var list = new List<T>();

				using var enumerable = new DelegateFileSystemEnumerator<FileSystemTreeItem>(folder.GetPath(), FileSystemTreeItem.Options)
				{
					Transformation = (ref FileSystemEntry entry) => new FileSystemTreeItem(entry.FileName, entry.IsDirectory, folder),
					Find = (ref FileSystemEntry entry) => entry.IsDirectory || FileSystemName.MatchesSimpleExpression(pattern, entry.FileName)
				};

				while (enumerable.MoveNext() && !token.IsCancellationRequested)
				{
					var item = enumerable.Current;

					if (item.IsFolder)
					{
						bag.Enqueue(Run(bag, item, pattern, action, transformation, token));

						if (FileSystemName.MatchesSimpleExpression(pattern, item.Value))
						{
							list.Add(transformation(new FileModel(item)));
						}
					}
					else
					{
						list.Add(transformation(new FileModel(item)));
					}
				}

				return list;
			}, token);

			action(result);
		}
	}

	public IEnumerable<IMenuModel> GetMenuItems(IFileItem? item)
	{
		if (item is null)
		{
			yield break;
		}

		yield return new ButtonMenuItemModel(MaterialIconKind.Scissors, Cut);
		yield return new ButtonMenuItemModel(MaterialIconKind.ContentCopy, Copy);
		yield return new ButtonMenuItemModel(MaterialIconKind.ContentPaste, Paste);
		yield return new ButtonMenuItemModel(MaterialIconKind.TrashCanOutline, Remove);
		yield return new SeparatorMenuItemModel();
		yield return new ButtonMenuItemModel(MaterialIconKind.InformationOutline, Properties);
		yield return new ButtonMenuItemModel(MaterialIconKind.FolderZipOutline, Zip);
		yield return new SeparatorMenuItemModel();
		// yield return new ButtonMenuItemModel(MaterialIconKind.Analytics, Analyze);
	}

	public IFolderUpdateNotificator? GetNotificator([NotNullIfNotNull(nameof(folder))] IFileItem? folder, string filter, bool recursive)
	{
		if (folder is null)
		{
			return null;
		}

		return new FileSystemUpdater(folder.GetPath(), filter, recursive);
	}

	private void Cut(MenuItemActionModel model)
	{
		_clipboard = model.Files
			.Where(w => w.IsSelected)
			.ToList();
	}

	private void Copy(MenuItemActionModel model)
	{
		_clipboard = model.Files
			.Where(w => w.IsSelected)
			.ToList();
	}

	private void Paste(MenuItemActionModel model)
	{
		if (!_clipboard.Any())
		{
			return;
		}

		var progress = new Progress();

		progress.Loaded += async delegate
		{
			var maxLength = (double) _clipboard
				.Select(s => new FileInfo(s.GetPath()).Length)
				.Sum();

			var length = 0L;
			var previousLength = 0L;
			var isDone = false;

			var etaCalculator = new EtaCalculator(1, TimeSpan.FromSeconds(30));

			Task.Run(async () =>
			{
				const int interval = 1000;
				using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(interval));

				while (await timer.WaitForNextTickAsync() && !isDone)
				{
					progress.Speed = (length - previousLength) * (long) (1000d / interval);
					progress.EstimateTime = $"{etaCalculator.ETR.Humanize(3, minUnit: TimeUnit.Second)}";

					previousLength = length;
				}
			});

			foreach (var file in _clipboard)
			{
				var sourcePath = file.GetPath();
				var destinationPath = Path.Combine(model.CurrentFolder.GetPath(), Path.GetFileName(sourcePath));

				if (File.Exists(destinationPath) || Directory.Exists(destinationPath))
				{
					continue;
				}

				await using var sourceStream = File.OpenRead(sourcePath);
				await using var destinationStream = File.Create(destinationPath);

				var currentFileDone = false;

				Task.Run(async () =>
				{
					using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(25));
					var currentFileLengthCount = 0L;

					while (await timer.WaitForNextTickAsync() && !currentFileDone)
					{
						var streamPosition = sourceStream.Position;

						length += streamPosition - currentFileLengthCount;
						currentFileLengthCount = streamPosition;

						progress.Process = length / maxLength;
						etaCalculator.Update(progress.Process);
					}
				});

				await sourceStream.CopyToAsync(destinationStream);
				currentFileDone = true;
			}

			progress.Process = 1;
			progress.Close();
			isDone = true;
		};

		model.Popup(progress);
	}

	private void Remove(MenuItemActionModel model)
	{
		var selectedCount = model.Files.Count(c => c.IsSelected);

		if (selectedCount > 0)
		{
			var itemText = GetItemText(selectedCount);

			var choice = new Choice
			{
				Message = String.Format(CultureInfo.CurrentCulture, DeleteFormat, selectedCount, itemText),
				CloseText = ResourceDefault.Close,
				SubmitText = ResourceDefault.Delete,
				Image = new SvgImage
				{
					Source = SvgSource.Load<SvgSource>("avares://FileExplorer/Assets/UIIcons/Question.svg", null)
				}
			};

			choice.OnSubmit += delegate
			{
				foreach (var item in model.Files.Where(w => w.IsSelected))
				{
					Task.Factory.StartNew(parameter =>
					{
						var path = parameter as string;

						if (Directory.Exists(path))
						{
							Directory.Delete(path, true);
						}
						else if (File.Exists(path))
						{
							File.Delete(path);
						}
					}, item.GetPath());
				}

				if (!DialogHost.IsDialogOpen(null))
				{
					DialogHost.Close(null);
				}
			};

			model.Popup(choice);
		}
	}

	private void Analyze(MenuItemActionModel model)
	{
		var analyzer = new AnalyzerView();
		var path = model.CurrentFolder.GetPath(x => x.ToString());
		var tokenSource = new CancellationTokenSource();

		var options = FileSystemTreeItem.Options;

		var folderQuery = new FileSystemEnumerable<FileIndexModel>(path, (ref FileSystemEntry x) => new FileIndexModel(FileSystemTreeItem.FromPath(x.ToFullPath())), options)
		{
			ShouldIncludePredicate = (ref FileSystemEntry x) => x.IsDirectory
		};

		analyzer.Initialized += delegate
		{
			ThreadPool.QueueUserWorkItem(async x =>
			{
				var query = folderQuery; //.Concat(fileQuery);

				var comparer = new AsyncComparer<FileIndexModel>(async (x, y) =>
				{
					var resultX = x.TaskSize;
					var resultY = y.TaskSize;

					Task.WhenAll(resultX, resultY);

					return resultY.Result.CompareTo(resultX.Result);
				});

				await analyzer.Root.AddRange(folderQuery, token: tokenSource.Token);
			});
		};

		// ThreadPool.QueueUserWorkItem(async x =>
		// {
		// 	var options = new EnumerationOptions()
		// 	{
		// 		IgnoreInaccessible = true,
		// 		AttributesToSkip = FileAttributes.System,
		// 		RecurseSubdirectories = true,
		// 	};
		//
		// 	var extensionQuery = new FileSystemEnumerable<(string Extension, long Size)>(path, (ref FileSystemEntry y) => (Path.GetExtension(y.FileName).ToString(), y.Length), options)
		// 		{
		// 			ShouldIncludePredicate = (ref FileSystemEntry z) => !z.IsDirectory,
		// 		}
		// 		.Where(w => !String.IsNullOrEmpty(w.Extension))
		// 		.GroupBy(g => g.Extension)
		// 		.Where(w => tokenSource?.IsCancellationRequested != true);
		//
		// 	var comparer = new ExtensionModelComparer();
		//
		// 	analyzer.Extensions.AddRange(extensionQuery.Select(s => new ExtensionModel(s.Key, s.Sum(s => s.Size))
		// 	{
		// 		TotalFiles = s.Count(),
		// 	}), comparer);
		// });

		// analyzer.OnClose += tokenSource.Cancel;

		model.Popup(analyzer);
	}

	private void Properties(MenuItemActionModel model)
	{
		model.Popup(new Properties
		{
			Provider = this,
			Model = model.CurrentFolder
		});
	}

	private void Zip(MenuItemActionModel? model)
	{
		if (model?.Files is not null)
		{
			model.Popup(new Zip
			{
				SelectedFiles = model.Files
					.Where(w => w is { IsSelected: true, IsFolder: false })
					.ToList(),
				Folder = model.CurrentFolder
			});
		}
	}

	private static bool IsCategory(ReadOnlySpan<char> name, string filter)
	{
		var extension = Path.GetExtension(name).ToString();

		if (filter.Contains("[Images]", StringComparison.CurrentCultureIgnoreCase) &&
		    (ThumbnailProvider.FileTypes.TryGetValue("Jpeg", out var files) && files.Contains(extension) ||
		     ThumbnailProvider.FileTypes.TryGetValue("Png", out files) && files.Contains(extension) ||
		     ThumbnailProvider.FileTypes.TryGetValue("RawImage", out files) && files.Contains(extension)))
		{
			return true;
		}

		foreach (var (groupName, extensions) in ThumbnailProvider.FileTypes)
		{
			if (filter.Contains($"[{groupName}]", StringComparison.CurrentCultureIgnoreCase) && extensions.Contains(extension))
			{
				return true;
			}
		}

		return false;
	}

	private IEnumerable<FileSystemTreeItem> GetEnumerable(FileSystemTreeItem item, string filter, bool recursive)
	{
		if (filter is "*" or "*.*")
		{
			return recursive
				? item.EnumerateChildrenRecursive()
				: item.EnumerateChildren();
		}

		return recursive
			? item.EnumerateChildrenRecursive(name => FileSystemName.MatchesSimpleExpression(filter, name) || IsCategory(name, filter)) // || Regex.IsMatch(name, filter))
			: item.EnumerateChildren(name => FileSystemName.MatchesSimpleExpression(filter, name) || IsCategory(name, filter)); // || Regex.IsMatch(name, filter));
	}

	private string GetItemText<T>(T itemCount) where T : IBinaryInteger<T>
	{
		return itemCount > T.One
			? ResourceDefault.Items
			: ResourceDefault.Item;
	}
}