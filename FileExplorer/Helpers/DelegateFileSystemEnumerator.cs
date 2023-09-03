using System.IO;
using System.IO.Enumeration;

namespace FileExplorer.Helpers;

public sealed class DelegateFileSystemEnumerator<TResult>(string directory, EnumerationOptions options) : FileSystemEnumerator<TResult>(directory, options)
{
	public FileSystemEnumerable<TResult>.FindTransform? Transformation { get; init; }
	public FileSystemEnumerable<TResult>.FindPredicate? Find { get; init; }

	protected override TResult TransformEntry(ref FileSystemEntry entry)
	{
		return Transformation is not null 
			? Transformation(ref entry) 
			: default!;
	}

	protected override bool ShouldRecurseIntoEntry(ref FileSystemEntry entry) => false;
	protected override bool ShouldIncludeEntry(ref FileSystemEntry entry) => Find is null || Find(ref entry);

	protected override bool ContinueOnError(int error) => true;
}