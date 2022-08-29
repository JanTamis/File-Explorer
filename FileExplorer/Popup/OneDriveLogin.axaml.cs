using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FileExplorer.Interfaces;

namespace FileExplorer.Popup;

public partial class OneDriveLogin : UserControl, IPopup, INotifyPropertyChanged
{
	public new event PropertyChangedEventHandler PropertyChanged = delegate { };

	public bool HasShadow => true;
	public bool HasToBeCanceled => true;
	public string Title => "OneDrive Login";

	private Uri _redirectUri;
	private string _code;

	public Uri RedirectUri
	{
		get => _redirectUri;
		set => OnPropertyChanged(ref _redirectUri, value);
	}

	public string Code
	{
		get => _code;
		set => OnPropertyChanged(ref _code, value);
	}

	public OneDriveLogin()
	{
		AvaloniaXamlLoader.Load(this);
		DataContext = this;
	}

	public void Close()
	{
		OnClose();
	}

	public event Action? OnClose;

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