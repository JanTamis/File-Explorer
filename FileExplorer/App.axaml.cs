using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using FileExplorer.Injection;
using FileExplorer.ViewModels;
using FileExplorer.Views;
using Material.Styles.Themes;
using Material.Styles.Themes.Base;

namespace FileExplorer;

public partial class App : Application
{
	public static MainWindowViewModel? MainViewModel { get; set; }
	public static Container Container { get; } = new Container();

	public override void Initialize()
	{
		AvaloniaXamlLoader.Load(this);

		// configuration = new ConfigurationBuilder()
		// 	.AddJsonFile("appsettings.json")
		// 	.Build();
		//
		// var value = await SettingsHelpers.GetSettings<AppSettings>();
		//
		// await SettingsHelpers.UpdateSettings(value);
		//
		// value = await SettingsHelpers.GetSettings<AppSettings>();
	}

	public override void OnFrameworkInitializationCompleted()
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			var theme = this.LocateMaterialTheme<MaterialTheme>();
			
			if (OperatingSystem.IsWindows() && SystemThemeProbe.GetSystemBaseThemeMode() is { } mode)
			{
				theme.BaseTheme = mode;
			}

			Resources["WindowBackground"] = theme.BaseTheme switch
			{
				BaseThemeMode.Light => new SolidColorBrush(Color.Parse("#efeff5")),
				BaseThemeMode.Dark => new SolidColorBrush(Color.Parse("#333337")),
			};

			Resources["WindowBorder"] = theme.BaseTheme switch
			{
				BaseThemeMode.Light => new SolidColorBrush(Color.Parse("#bfbfbf")),
				BaseThemeMode.Dark => new SolidColorBrush(Color.Parse("#1a212e")),
			};

			desktop.MainWindow = new MainWindow();
			desktop.MainWindow.DataContext = MainViewModel = new MainWindowViewModel(new WindowNotificationManager(desktop.MainWindow)
			{
				Position = NotificationPosition.TopRight,
				Margin = new Thickness(0, 40, 0, 0),
			});
		}

		base.OnFrameworkInitializationCompleted();
	}
}