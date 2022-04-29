using System;
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
		var temp = Utf8String.Create($"hello  ");


    foreach (var _string in temp.Split(' ', StringSplitOptions.RemoveEmptyEntries))
    {
      Console.WriteLine(_string);
    }

    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			Current!.Styles[0] = new FluentTheme(new Uri(@"avares://FileExplorer"))
			{
				Mode = FluentThemeMode.Dark,
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