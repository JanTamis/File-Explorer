using Avalonia.Controls;
using Avalonia.Media;
using FileExplorer.Core.Models;
using FileExplorer.Interfaces;

namespace FileExplorer.Core.Interfaces;

public interface IItemProvider
{
	IAsyncEnumerable<IFileItem> GetItemsAsync(IFileItem folder, string filter, bool recursive, CancellationToken token);
	IEnumerable<IFileItem> GetItems(IFileItem folder, string filter, bool recursive, CancellationToken token);

	bool HasItems(IFileItem folder);

	ValueTask<IEnumerable<IPathSegment>> GetPathAsync(IFileItem folder);

	ValueTask<IFileItem?> GetParentAsync(IFileItem folder, CancellationToken token);

	Task<IImage?> GetThumbnailAsync(IFileItem? item, int size, CancellationToken token);

	IEnumerable<IMenuModel> GetMenuItems(IFileItem? item) => Enumerable.Empty<IMenuModel>();

	IFolderUpdateNotificator? GetNotificator(IFileItem folder, string filter, bool recursive);

	Task EnumerateItemsAsync(IFileItem folder, string pattern, Action<IFileItem> action, CancellationToken token);
	Task EnumerateItemsAsync<T>(IFileItem folder, string pattern, Action<IEnumerable<T>> action, Func<IFileItem, T> transformation, CancellationToken token);
}