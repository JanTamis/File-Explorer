namespace FileExplorer.Core.Interfaces;

public interface IItemProvider
{
	ValueTask<IEnumerable<IFileItem>> GetItems(string path, string filter, bool recursive, CancellationToken token);

	IEnumerable<IPathSegment> GetPath(string? path);
}