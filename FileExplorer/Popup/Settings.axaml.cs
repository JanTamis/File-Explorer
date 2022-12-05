using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using FileExplorer.Core.Interfaces;
using Material.Styles.Themes;
using Material.Styles.Themes.Base;

namespace FileExplorer.Popup;

public sealed partial class Settings : UserControl, IPopup
{
	public bool HasShadow => true;
	public bool HasToBeCanceled => false;

	public bool IsDarkMode
	{
		get => Application.Current?.Styles is [MaterialTheme { BaseTheme: BaseThemeMode.Dark }, ..];
		set
		{
			if (Application.Current?.Styles is [MaterialTheme theme, ..])
			{
				theme.BaseTheme = value
					? BaseThemeMode.Dark
					: BaseThemeMode.Light;

				Application.Current.Resources["WindowBackground"] = theme.BaseTheme switch
				{
					BaseThemeMode.Light => new SolidColorBrush(Color.Parse("#efeff5")),
					BaseThemeMode.Dark => new SolidColorBrush(Color.Parse("#333337")),
				};

				Application.Current.Resources["WindowBorder"] = theme.BaseTheme switch
				{
					BaseThemeMode.Light => new SolidColorBrush(Color.Parse("#bfbfbf")),
					BaseThemeMode.Dark => new SolidColorBrush(Color.Parse("#1a212e")),
				};
			}
		}
	}

	public string Title => "Settings";

	public event Action OnClose = delegate { };

	public Settings()
	{
		DataContext = this;

		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}

	public void Close()
	{
		OnClose();
	}
}