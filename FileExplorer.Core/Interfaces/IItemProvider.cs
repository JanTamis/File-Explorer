using Avalonia.Media;

namespace FileExplorer.Core.Interfaces;

public interface IItemProvider
{
	IAsyncEnumerable<IFileItem> GetItemsAsync(IFileItem folder, string filter, bool recursive, CancellationToken token);
	IEnumerable<IFileItem> GetItems(IFileItem folder, string filter, bool recursive, CancellationToken token);

	bool HasItems(IFileItem folder);

	ValueTask<IEnumerable<IPathSegment>> GetPathAsync(IFileItem folder);

	ValueTask<IFileItem?> GetParentAsync(IFileItem folder, CancellationToken token);

	Task<IImage?> GetThumbnailAsync(IFileItem? item, int size, CancellationToken token);
}