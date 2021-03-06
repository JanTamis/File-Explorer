using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FileExplorerCore.Helpers;
using FileExplorerCore.Interfaces;
using FileExplorerCore.Models;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FileExplorerCore.Popup;

public partial class Rename : UserControl, IPopup, INotifyPropertyChanged
{
	public new event PropertyChangedEventHandler PropertyChanged = delegate { };

	public FileModel File => Files?[Index];

	public ObservableRangeCollection<FileModel> Files { get; set; }
	public int Index { get; set; }

	public bool HasShadow => false;
	public bool HasToBeCanceled => false;

	public string Title => $"Rename: {File.Name}";

	public event Action OnClose = delegate { };

	public Rename()
	{
		AvaloniaXamlLoader.Load(this);

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

	protected void OnPropertyChanged<T>(ref T property, T value, [CallerMemberName] string name = null)
	{
		property = value;
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}

	public void OnPropertyChanged(string name)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}