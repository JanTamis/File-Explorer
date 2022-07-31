namespace FileExplorer.Core.Interfaces;

public interface IFileProvider
{
	IEnumerable<IPathSegment> GetPathSegments();
}