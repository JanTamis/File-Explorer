using System;
using System.Text;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Markup.Xaml;
using Avalonia.Themes.Fluent;
using FileExplorerCore.Helpers;
using FileExplorerCore.Injection;
using FileExplorerCore.ViewModels;
using FileExplorerCore.Views;

namespace FileExplorerCore;

public class App : Application
{
	public static MainWindowViewModel MainViewModel { get; set; }
	public static Container Container { get; } = new Container();

	public override void Initialize()
	{
		AvaloniaXamlLoader.Load(this);
	}

	public override void OnFrameworkInitializationCompleted()
	{
		// SetupHelper.SetupFileSystems();

		var world = "hello world";
		var bytes = Encoding.UTF8.GetBytes(world[0..10]);

		var temp = new DynamicString(world);

		var result = temp[..10];
		var temporary = Encoding.UTF8.GetString(result.AsBytes());

		var item = result[3];

		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			//if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			//{
			//	var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
			//	var value = key.GetValue("AppsUseLightTheme");

			//	if (value is 0)
			//	{
			var fluentTheme = new FluentTheme(new Uri(@"avares://FileExplorer"))
			{
				Mode = FluentThemeMode.Dark,
			};

			Current!.Styles[0] = fluentTheme;
			//	}
			//}

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