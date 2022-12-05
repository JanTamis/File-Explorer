using CommunityToolkit.Mvvm.ComponentModel;

namespace FileExplorer.Models;

[INotifyPropertyChanged]
public sealed partial class ExtensionModel : IComparable<ExtensionModel>
{
	[ObservableProperty]
	private long _totalSize;

	[ObservableProperty]
	private long _totalFiles;

	[ObservableProperty]
	private string _extension;

	public ExtensionModel(string extension, long totalSize)
	{
		_totalSize = totalSize;
		_extension = extension;
		_totalFiles = 1;
	}

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
		return y.TotalSize.CompareTo(x.TotalSize);
	}
}