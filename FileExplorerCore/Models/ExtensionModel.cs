using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FileExplorerCore.Models
{
	public class ExtensionModel : INotifyPropertyChanged, IComparable<ExtensionModel>
	{
		private long _totalSize;
		private long _totalFiles = 1;
		private string _extension;

		public event PropertyChangedEventHandler? PropertyChanged = delegate { };

		public long TotalSize
		{
			get => _totalSize;
			set => OnPropertyChanged(ref _totalSize, value);
		}

		public long TotalFiles
		{
			get => _totalFiles;
			set => OnPropertyChanged(ref _totalFiles, value);
		}

		public string Extension
		{
			get => _extension;
			set => OnPropertyChanged(ref _extension, value);
		}

		public ExtensionModel(string extension, long totalSize)
		{
			TotalSize = totalSize;
			Extension = extension;
		}

		public override string ToString()
		{
			return Extension;
		}

		public void OnPropertyChanged<T>(ref T field, T value, [CallerMemberName] string name = null)
		{
			field = value;

			OnPropertyChanged(name);
		}

		public void OnPropertyChanged([CallerMemberName] string name = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		}

		public int CompareTo(ExtensionModel? other)
		{
			return Extension.CompareTo(other.Extension);
		}
	}

	public class ExtensionModelComparer : Comparer<ExtensionModel>
	{
		public override int Compare(ExtensionModel? x, ExtensionModel? y)
		{
			return x.Extension.CompareTo(y.Extension);
		}
	}
}
