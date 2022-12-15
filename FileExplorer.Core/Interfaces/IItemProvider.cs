using Avalonia.Controls;
using Avalonia.Media;
using FileExplorer.Core.Models;

namespace FileExplorer.Core.Interfaces;

public interface IItemProvider
{
	IAsyncEnumerable<IFileItem> GetItemsAsync(IFileItem folder, string filter, bool recursive, CancellationToken token);
	IEnumerable<IFileItem> GetItems(IFileItem folder, string filter, bool recursive, CancellationToken token);

	bool HasItems(IFileItem folder);

	ValueTask<IEnumerable<IPathSegment>> GetPathAsync(IFileItem folder);

	ValueTask<IFileItem?> GetParentAsync(IFileItem folder, CancellationToken token);

	Task<IImage?> GetThumbnailAsync(IFileItem? item, int size, CancellationToken token);

	IEnumerable<MenuItemModel> GetMenuItems(IFileItem item) => Enumerable.Empty<MenuItemModel>();

	IFolderUpdateNotificator? GetNotificator(IFileItem folder, string filter, bool recursive);
}