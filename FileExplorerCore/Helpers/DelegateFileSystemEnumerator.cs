using System;
using System.IO;
using System.IO.Enumeration;

namespace FileExplorerCore.Helpers
{
  public sealed class DelegateFileSystemEnumerator<TResult> : FileSystemEnumerator<TResult> where TResult : struct
  {
    public FileSystemEnumerable<TResult>.FindTransform? Transformation { get; }

    public DelegateFileSystemEnumerator(string directory,  EnumerationOptions options) : base(directory, options)
    {
      
    }

    protected override TResult TransformEntry(ref FileSystemEntry entry)
    {
      return Transformation is not null 
        ? Transformation(ref entry) 
        : default;
    }

    protected override bool ShouldRecurseIntoEntry(ref FileSystemEntry entry) => false;
    protected override bool ShouldIncludeEntry(ref FileSystemEntry entry) => true;

    protected override bool ContinueOnError(int error) => true;
  }
}
