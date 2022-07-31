namespace FileExplorer.Core.Interfaces;

public interface IPathSegment
{
	string Name { get; }

	bool HasItems { get; }

	IEnumerable<IPathSegment> SubSegments { get; }
}