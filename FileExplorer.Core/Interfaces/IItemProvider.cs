namespace FileExplorer.Core.Interfaces;

public interface IItemProvider
{
	IEnumerable<IFileItem> GetItems(string path, string filter, bool recursive);

	IEnumerable<IPathSegment> GetPath(string? path);
}