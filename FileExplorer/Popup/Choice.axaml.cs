using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using DialogHostAvalonia;
using FileExplorer.Core.Interfaces;

namespace FileExplorer.Popup;

public sealed partial class Choice : UserControl, IPopup, INotifyPropertyChanged
{
	private string _submitText;
	private string _closeText;
	private string _message;
	private IImage _image;

	public bool HasShadow => true;
	public bool HasToBeCanceled => false;


	public string SubmitText
	{
		get => _submitText;
		set => OnPropertyChanged(ref _submitText, value);
	}

	public string CloseText
	{
		get => _closeText;
		set => OnPropertyChanged(ref _closeText, value);
	}

	public string Message
	{
		get => _message;
		set => OnPropertyChanged(ref _message, value);
	}

	public IImage Image
	{
		get => _image;
		set => OnPropertyChanged(ref _image, value);
	}

	public string Title => Message;

	public event Action OnClose = delegate { };
	public event Action OnSubmit = delegate { };

	public Choice()
	{
		InitializeComponent();
		DataContext = this;
	}

	public void Confirm()
	{
		OnSubmit();

		try
		{
			DialogHost.Close(null);
		}
		catch (Exception e)
		{
			Console.WriteLine(e);
		}
	}

	public void Close()
	{
		OnClose();

		try
		{
			DialogHost.Close(null);
		}
		catch (Exception e)
		{
			Console.WriteLine(e);
		}
	}

	public new event PropertyChangedEventHandler PropertyChanged = delegate { };

	protected void OnPropertyChanged<T>(ref T property, T value, [CallerMemberName] string name = null)
	{
		property = value;
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}

	public void OnPropertyChanged([CallerMemberName] string name = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}