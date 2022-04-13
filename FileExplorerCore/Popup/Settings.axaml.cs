using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using FileExplorerCore.Interfaces;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using Avalonia;

namespace FileExplorerCore.Popup;

public partial class Settings : UserControl, IPopup, INotifyPropertyChanged
{
	public new event PropertyChangedEventHandler PropertyChanged = delegate { };

	private bool isDarkMode;

	public bool HasShadow => true;
	public bool HasToBeCanceled => false;

	public string Title => "Settings";

	public event Action OnClose = delegate { };

	public bool IsDarkMode
	{
		get => isDarkMode;
		set
		{
			OnPropertyChanged(ref isDarkMode, value);

			ThreadPool.QueueUserWorkItem(x =>
			{
				var fluentTheme = new FluentTheme(new Uri(@"avares://FileExplorer"))
				{
					Mode = IsDarkMode ? FluentThemeMode.Dark : FluentThemeMode.Light,
				};

				Dispatcher.UIThread.Post(() => Application.Current.Styles[0] = fluentTheme);
			});
		}
	}

	public Settings()
	{
		InitializeComponent();

		DataContext = this;
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}

	public void Close()
	{
		OnClose();
	}

	protected void OnPropertyChanged<T>(ref T property, T value, [CallerMemberName] string name = null)
	{
		property = value;
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}