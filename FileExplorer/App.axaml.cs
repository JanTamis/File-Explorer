using System;
using System.IO;
using System.Reflection;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Markup.Xaml;
using Avalonia.Themes.Fluent;
using FileExplorer.Helpers;
using FileExplorer.Injection;
using FileExplorer.Models;
using FileExplorer.ViewModels;
using FileExplorer.Views;
using Microsoft.Extensions.Configuration;

namespace FileExplorer;

public class App : Application
{
	public static MainWindowViewModel? MainViewModel { get; set; }
	public static Container Container { get; } = new Container();

	private IConfiguration configuration;

	public override async void Initialize()
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