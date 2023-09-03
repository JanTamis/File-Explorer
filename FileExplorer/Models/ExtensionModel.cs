using CommunityToolkit.Mvvm.ComponentModel;

namespace FileExplorer.Models;

public sealed partial class ExtensionModel(string extension, long totalSize) : ObservableObject, IComparable<ExtensionModel>
{
	[ObservableProperty]
	private long _totalSize = totalSize;

	[ObservableProperty]
	private long _totalFiles = 1;

	[ObservableProperty]
	private string _extension = extension;

	public override string ToString()
	{
		return Extension;
	}

	public int CompareTo(ExtensionModel? other)
	{
		return String.Compare(Extension, other?.Extension, StringComparison.CurrentCulture);
	}
}

public sealed class ExtensionModelComparer : IComparer<ExtensionModel>
{
	public int Compare(ExtensionModel? x, ExtensionModel? y)
	{
		if (x is null && y is null)
		{
			return 0;
		}

		if (x is null)
		{
			return 1;
		}

		if (y is null)
		{
			return -1;
		}
		
		return y.TotalSize.CompareTo(x.TotalSize);
	}
}