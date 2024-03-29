using Avalonia.Controls;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using FileExplorer.Core.Helpers;
using FileExplorer.Core.Interfaces;

namespace FileExplorer.Popup;

public sealed partial class Rename : UserControl, IPopup, INotifyPropertyChanged
{
	public new event PropertyChangedEventHandler? PropertyChanged = delegate { };

	public IFileItem? File => Files?[Index];

	public ObservableRangeCollection<IFileItem?> Files { get; set; }
	public int Index { get; set; }

	public bool HasShadow => false;
	public bool HasToBeCanceled => false;

	public string Title => $"Rename: {File.Name}";

	public event Action OnClose = delegate { };

	public Rename()
	{
		InitializeComponent();

		DataContext = this;
	}

	public void Close()
	{
		OnClose();
	}

	public void PreviousFile()
	{
		Index--;

		if (Index < 0)
		{
			Index = Files.Count - 1;
		}

		OnPropertyChanged(nameof(File));
		OnPropertyChanged(nameof(Title));
	}

	public void NextFile()
	{
		Index = (Index + 1) % Files.Count;

		OnPropertyChanged(nameof(File));
		OnPropertyChanged(nameof(Title));
	}

	private void OnPropertyChanged<T>(ref T property, T value, [CallerMemberName] string? name = null)
	{
		property = value;
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}

	public void OnPropertyChanged(string? name)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}