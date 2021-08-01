using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Markup.Xaml;
using Avalonia.Themes.Fluent;
using DiscUtils.FileSystems;
using FileExplorerCore.ViewModels;
using FileExplorerCore.Views;
using System;

namespace FileExplorerCore
{
	public class App : Application
	{
		public override void Initialize()
		{
			AvaloniaXamlLoader.Load(this);
		}

		public override void OnFrameworkInitializationCompleted()
		{
			SetupHelper.SetupFileSystems();

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

				App.Current.Styles[0] = fluentTheme;
				//	}
				//}

				desktop.MainWindow = new MainWindow();
				desktop.MainWindow.DataContext = new MainWindowViewModel(new WindowNotificationManager(desktop.MainWindow)
				{
					Position = NotificationPosition.BottomLeft,
					Margin = new Thickness(0, 10),
				});
			}

			base.OnFrameworkInitializationCompleted();
		}
	}
}
