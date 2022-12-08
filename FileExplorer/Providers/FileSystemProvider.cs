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

		yield return new MenuItemModel(MenuItemType.Button, "Cut", async x =>
		{
			clipboard = x.Files
				.Where(w => w.IsSelected)
				.ToArray();
		});

		yield return new MenuItemModel(MenuItemType.Button, "Copy", x =>
		{
			clipboard = x.Files
				.Where(w => w.IsSelected)
				.ToArray();
		});

		yield return new MenuItemModel(MenuItemType.Button, "Paste", async x =>
		{
			foreach (var file in clipboard ?? Enumerable.Empty<IFileItem>())
			{
				var path = file.GetPath();
				File.Copy(file.GetPath(), Path.Combine(x.CurrentFolder.GetPath(), Path.GetFileName(path)));
			}
		});

		yield return new MenuItemModel(MenuItemType.Button, "Remove", x =>
		{
			var selectedCount = x.Files.Count(c => c.IsSelected);

			if (selectedCount > 0)
			{
				var source = SvgSource.Load("avares://FileExplorer/Assets/UIIcons/Question.svg", null);

				var image = new SvgImage
				{
					Source = source,
				};

				var choice = new Choice
				{
					Message = $"Are you sure you want to delete {selectedCount:N0} item(s)?",
					CloseText = "No",
					SubmitText = "Yes",
					Image = image,
				};
				x.Popup = choice;

				choice.OnSubmit += delegate
				{
					foreach (var item in x.Files.Where(w => w.IsSelected))
					{
						if (item.IsFolder)
						{
							Directory.Delete(item.GetPath(), true);
						}
						else
						{
							File.Delete(item.GetPath());
						}

						x.Files.Remove(item);
					}

					if (!DialogHost.IsDialogOpen(null))
					{
						DialogHost.Close(null);
					}
				};
			}
		});
		yield return new MenuItemModel(MenuItemType.Separator);
		yield return new MenuItemModel(MenuItemType.Button, "Properties", x =>
		{
			x.Popup = new Properties
			{
				Provider = this,
				Model = x.CurrentFolder,
			};
		});
		yield return new MenuItemModel(MenuItemType.Button, "Zip");
		yield return new MenuItemModel(MenuItemType.Separator);
		yield return new MenuItemModel(MenuItemType.Button, "Analyze", x =>
		{
			var analyzer = new AnalyzerView();
			var path = x.CurrentFolder.GetPath(x => x.ToString());
			var tokenSource = new CancellationTokenSource();

			var options = FileSystemTreeItem.Options;

			var folderQuery = new FileSystemEnumerable<FileIndexModel>(path, (ref FileSystemEntry x) => new FileIndexModel(PathHelper.FromPath(x.ToFullPath())), options)
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

			x.Popup = analyzer;
		});
	}
}