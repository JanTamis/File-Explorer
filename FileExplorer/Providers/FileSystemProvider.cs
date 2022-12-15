using System.Diagnostics;
using Avalonia.Media;
using FileExplorer.Core.Interfaces;
using FileExplorer.Helpers;
using FileExplorer.Models;
using Microsoft.Extensions.Caching.Memory;
using System.IO;
using System.IO.Enumeration;
using System.Text.RegularExpressions;
using Avalonia.Svg;
using DialogHostAvalonia;
using FileExplorer.Core.Models;
using FileExplorer.DisplayViews;
using FileExplorer.Popup;
using Humanizer.Bytes;
using Directory = System.IO.Directory;
using File = System.IO.File;

namespace FileExplorer.Providers;

public sealed class FileSystemProvider : IItemProvider
{
	private readonly MemoryCache? _imageCache;

	private IFileItem[]? clipboard;

	public FileSystemProvider()
	{
		_imageCache = new MemoryCache(new MemoryCacheOptions
		{
			ExpirationScanFrequency = TimeSpan.FromMinutes(1),
			TrackStatistics = true,
			SizeLimit = 536_870_912,
		});
	}

	public IAsyncEnumerable<IFileItem> GetItemsAsync(IFileItem folder, string filter, bool recursive, CancellationToken token)
	{
		if (folder is FileModel model)
		{
			IEnumerable<FileSystemTreeItem> enumerable;

			if (filter is "*" or "*.*")
			{
				enumerable = recursive
					? model.TreeItem.EnumerateChildrenRecursive()
					: model.TreeItem.EnumerateChildren();
			}
			else
			{
				enumerable = recursive
					? model.TreeItem.EnumerateChildrenRecursive(name => FileSystemName.MatchesSimpleExpression(filter, name) || Regex.IsMatch(name, filter))
					: model.TreeItem.EnumerateChildren(name => FileSystemName.MatchesSimpleExpression(filter, name) || Regex.IsMatch(name, filter));
			}

			return enumerable
				.Select(s => new FileModel(s))
				.ToAsyncEnumerable();
		}

		return AsyncEnumerable.Empty<IFileItem>();
	}

	public IEnumerable<IFileItem> GetItems(IFileItem folder, string filter, bool recursive, CancellationToken token)
	{
		if (folder is FileModel model)
		{
			IEnumerable<FileSystemTreeItem> enumerable;

			if (filter is "*" or "*.*")
			{
				enumerable = recursive
					? model.TreeItem.EnumerateChildrenRecursive()
					: model.TreeItem.EnumerateChildren();
			}
			else
			{
				enumerable = recursive
					? model.TreeItem.EnumerateChildrenRecursive(name => FileSystemName.MatchesSimpleExpression(filter, name)) // || Regex.IsMatch(name, filter))
					: model.TreeItem.EnumerateChildren(name => FileSystemName.MatchesSimpleExpression(filter, name)); // || Regex.IsMatch(name, filter));
			}

			return enumerable.Select(s => new FileModel(s));
		}

		return Enumerable.Empty<IFileItem>();
	}

	public bool HasItems(IFileItem folder)
	{
		return folder is FileModel { TreeItem.HasChildren: true };
	}

	public async ValueTask<IEnumerable<IPathSegment>> GetPathAsync(IFileItem folder)
	{
		if (folder is FileModel { TreeItem: not null } file)
		{
			return await ValueTask.FromResult(file.TreeItem
				.EnumerateToRoot()
				.Reverse()
				.Select(s => new FolderModel(s)));
		}

		throw new ArgumentException("Provide a valid folder that is created from this provider");
	}

	public ValueTask<IFileItem?> GetParentAsync(IFileItem folder, CancellationToken token)
	{
		if (folder is FileModel { TreeItem.Parent: not null } file)
		{
			return new ValueTask<IFileItem?>(new FileModel(file.TreeItem.Parent));
		}

		return new ValueTask<IFileItem?>(null as IFileItem);
	}

	public Task<IImage?> GetThumbnailAsync(IFileItem? item, int size, CancellationToken token)
	{
		if (item is null or not FileModel)
		{
			return Task.FromResult<IImage?>(null);
		}

		if (_imageCache is not null)
		{
			return _imageCache.GetOrCreateAsync(item.GetHashCode(), async entry =>
			{
				var image = await ThumbnailProvider.GetFileImage(item, this, size, () => !token.IsCancellationRequested && item.IsVisible);

				if (image is not null)
				{
					entry.SetSize((long)image.Size.Width * (long)image.Size.Height * 4L);
				}

				return image;
			});
		}

		return ThumbnailProvider.GetFileImage(item, this, size, () => !token.IsCancellationRequested && item.IsVisible);
	}

	public IEnumerable<MenuItemModel> GetMenuItems(IFileItem? item)
	{
		if (item is null)
		{
			yield break;
		}

		yield return new MenuItemModel(MenuItemType.Button, "Cut", Cut);
		yield return new MenuItemModel(MenuItemType.Button, "Copy", Copy);
		yield return new MenuItemModel(MenuItemType.Button, "Paste", Paste);
		yield return new MenuItemModel(MenuItemType.Button, "Remove", Remove);
		yield return new MenuItemModel(MenuItemType.Separator);
		yield return new MenuItemModel(MenuItemType.Button, "Properties", Properties);
		yield return new MenuItemModel(MenuItemType.Button, "Zip");
		yield return new MenuItemModel(MenuItemType.Separator);
		yield return new MenuItemModel(MenuItemType.Button, "Analyze", Analyze);
	}

	public IFolderUpdateNotificator? GetNotificator(IFileItem? folder, string filter, bool recursive)
	{
		if (folder is null)
		{
			return null;
		}

		return new FileSystemUpdater(folder.GetPath(), filter, recursive);
	}

	public static TimeSpan EstimateTime(TimeSpan elapsedTime, double progress)
	{
		return Math.ReciprocalEstimate(progress) * elapsedTime - elapsedTime;
	}

	private void Cut(MenuItemActionModel model)
	{
		clipboard = model.Files
			.Where(w => w.IsSelected)
			.ToArray();
	}

	private void Copy(MenuItemActionModel model)
	{
		clipboard = model.Files
			.Where(w => w.IsSelected)
			.ToArray();
	}

	private void Paste(MenuItemActionModel model)
	{
		if (clipboard is null or { Length: 0 })
		{
			return;
		}

		var progress = new Progress();

		progress.Loaded += async delegate
		{
			var maxLength = (double)clipboard
				.Select(s => new FileInfo(s.GetPath()).Length)
				.Sum();

			var length = 0L;
			var previousLength = 0L;
			var isDone = false;
			var startTime = Stopwatch.GetTimestamp();

			Task.Run(async () =>
			{
				const int interval = 1000;
				using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(interval));

				while (await timer.WaitForNextTickAsync() && !isDone)
				{
					progress.Speed = (length - previousLength) * (long)(1000d / interval);
					progress.EstimateTime = $"Estimated time: {EstimateTime(Stopwatch.GetElapsedTime(startTime), progress.Process):hh\\:mm\\:ss}";

					previousLength = length;
				}
			});

			foreach (var file in clipboard)
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
					}
				});

				await sourceStream.CopyToAsync(destinationStream);
				currentFileDone = true;
			}

			progress.Process = 1;
			progress.Close();
			isDone = true;
		};

		model.Popup = progress;
	}

	private void Remove(MenuItemActionModel model)
	{
		var selectedCount = model.Files.Count(c => c.IsSelected);

		if (selectedCount > 0)
		{
			var itemText = selectedCount > 1
				? "items"
				: "item";

			var choice = new Choice
			{
				Message = $"Are you sure you want to delete {selectedCount:N0} {itemText}?",
				CloseText = "No",
				SubmitText = "Yes",
				Image = new SvgImage
				{
					Source = SvgSource.Load("avares://FileExplorer/Assets/UIIcons/Question.svg", null),
				},
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

			model.Popup = choice;
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
			ShouldIncludePredicate = (ref FileSystemEntry x) => x.IsDirectory,
		};

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

			await analyzer.Root.AddRangeAsyncComparer<AsyncComparer<FileIndexModel>>(query, token: tokenSource.Token);
		});

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

		model.Popup = analyzer;
	}

	public void Properties(MenuItemActionModel model)
	{
		model.Popup = new Properties
		{
			Provider = this,
			Model = model.CurrentFolder,
		};
	}
}