using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Themes.Fluent;
using FileExplorerCore.ViewModels;
using FileExplorerCore.Views;
using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;

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
			if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
			{
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
					var value = key.GetValue("AppsUseLightTheme");

					if (value is 0)
					{
						var fluentTheme = new FluentTheme(new Uri(@"avares://FileExplorer"))
						{
							Mode = FluentThemeMode.Dark,
						};

						App.Current.Styles[0] = fluentTheme;
					}
				}

				desktop.MainWindow = new MainWindow();
			}

			base.OnFrameworkInitializationCompleted();
		}
	}
}
