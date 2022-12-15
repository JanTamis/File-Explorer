using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using DialogHostAvalonia;
using FileExplorer.Core.Interfaces;

namespace FileExplorer.Popup;

public partial class Progress : UserControl, IPopup, INotifyPropertyChanged
{
	public new event PropertyChangedEventHandler PropertyChanged = delegate { };

	private double _progress;
	private long _speed;
	private string _estimateTime;

	public bool HasShadow => false;
	public bool HasToBeCanceled => true;

	public string Title => "Copying items...";

	public double Process
	{
		get => _progress;
		set => OnPropertyChanged(ref _progress, value);
	}

	public long Speed
	{
		get => _speed;
		set => OnPropertyChanged(ref _speed, value);
	}

	public string EstimateTime
	{
		get => _estimateTime;
		set => OnPropertyChanged(ref _estimateTime, value);
	}

	public event Action? OnClose;

	public Progress()
	{
		AvaloniaXamlLoader.Load(this);
	}

	public void Close()
	{
		DialogHost.Close(null);
		OnClose?.Invoke();
	}

	protected void OnPropertyChanged<T>(ref T property, T value, [CallerMemberName] string name = null)
	{
		property = value;
		OnPropertyChanged(name);
	}

	public void OnPropertyChanged(string name)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}