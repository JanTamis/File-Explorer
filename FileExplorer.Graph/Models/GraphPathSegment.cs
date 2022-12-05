using FileExplorer.Core.Interfaces;

namespace FileExplorer.Graph.Models;

public sealed class GraphPathSegment : IPathSegment
{
	private readonly GraphFileModel _file;

	public GraphPathSegment(GraphFileModel file)
	{
		_file = file;
	}

	public string Name => _file.Name;
	public bool HasItems => false;

	public IEnumerable<IPathSegment> SubSegments => Enumerable.Empty<IPathSegment>();
}