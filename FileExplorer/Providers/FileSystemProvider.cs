using System.Runtime.CompilerServices;
using FileExplorer.Core.Interfaces;
using FileExplorer.Models;
using Avalonia.Media;
using FileExplorer.Helpers;
using Microsoft.Extensions.Caching.Memory;
using System.IO.Enumeration;
using System.Text.RegularExpressions;

namespace FileExplorer.Providers;

public class FileSystemProvider : IItemProvider
{
	private readonly MemoryCache? _imageCache;

	public FileSystemProvider()
	{
			_imageCache = new MemoryCache(new MemoryCacheOptions
			{
				ExpirationScanFrequency = TimeSpan.FromMinutes(1),
				TrackStatistics = true,
				SizeLimit = 536_870_912,
			});
	}

	public IAsyncEnumerable<IFileItem> GetItemsAsync(IFileItem folder, string filter, bool recursive, [EnumeratorCancellation] CancellationToken token)
	{
		if (folder is FileModel model)
		{
			//var regex = new Regex(filter);

			return model.TreeItem
				.EnumerateChildren(name => FileSystemName.MatchesSimpleExpression(filter, name), recursive ? uint.MaxValue : 0)
				.Select(s => new FileModel(s))
				.ToAsyncEnumerable();
		}

		return AsyncEnumerable.Empty<IFileItem>();
	}

	public IEnumerable<IFileItem> GetItems(IFileItem folder, string filter, bool recursive, CancellationToken token)
	{
		if (folder is FileModel model)
		{
			return model.TreeItem
				.EnumerateChildren(name => FileSystemName.MatchesSimpleExpression(filter, name), recursive ? uint.MaxValue : 0)
				.Select(s => new FileModel(s));
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
		if (item is null)
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
}