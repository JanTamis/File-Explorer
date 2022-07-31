using System.IO;
using System.IO.Enumeration;

namespace FileExplorer.Helpers;

public sealed class DelegateFileSystemEnumerator<TResult> : FileSystemEnumerator<TResult>
{
	public FileSystemEnumerable<TResult>.FindTransform? Transformation { get; set; }
	public FileSystemEnumerable<TResult>.FindPredicate? Find { get; set; }

	public DelegateFileSystemEnumerator(string directory,  EnumerationOptions options) : base(directory, options)
	{
      
	}

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