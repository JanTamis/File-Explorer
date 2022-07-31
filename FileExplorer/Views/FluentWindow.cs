using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;

namespace FileExplorer.Views;

public class FluentWindow : Window, IStyleable, INotifyPropertyChanged
{
	Type IStyleable.StyleKey => typeof(Window);

	public event PropertyChangedEventHandler PropertyChanged = delegate { };

	public Thickness TitleBarMargin
	{
		get
		{
			if (OperatingSystem.IsMacOS())
			{
				if (WindowState is WindowState.FullScreen)
				{
					return new Thickness(0);
				}
				else
				{
					return new Thickness(75, 0, 0, 0);
				}
			}
			if (OperatingSystem.IsWindows())
			{
				return new Thickness(0, 0, 150, 0);
			}

			return new Thickness(0);
		}
	}

	public FluentWindow()
	{
		ExtendClientAreaToDecorationsHint = true;
		ExtendClientAreaTitleBarHeightHint = -1;
			
		this.GetObservable(WindowStateProperty)
			.Subscribe(async x =>
			{
				PseudoClasses.Set(":maximized", x == WindowState.Maximized);
				PseudoClasses.Set(":fullscreen", x == WindowState.FullScreen);

				await OnPropertyChanged(nameof(TitleBarMargin));
			});
	}

	public async ValueTask OnPropertyChanged([CallerMemberName] string? name = null)
	{
		if (Dispatcher.UIThread.CheckAccess())
		{
			PropertyChanged(this, new PropertyChangedEventArgs(name));
		}
		else
		{
			await Dispatcher.UIThread.InvokeAsync(() => PropertyChanged(this, new PropertyChangedEventArgs(name)));
		}
	}

	protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
	{
		base.OnApplyTemplate(e);
		ExtendClientAreaChromeHints =
			ExtendClientAreaChromeHints.PreferSystemChrome |
			ExtendClientAreaChromeHints.OSXThickTitleBar;
	}
}