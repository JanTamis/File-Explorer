using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Platform;
using Avalonia.Styling;

namespace FileExplorer.Views;

public partial class FluentWindow : Window, IStyleable, INotifyPropertyChanged
{
	Type IStyleable.StyleKey => typeof(Window);

	public Thickness TitleBarMargin { get; set; }

	public FluentWindow()
	{
		ExtendClientAreaToDecorationsHint = true;
		ExtendClientAreaTitleBarHeightHint = -1;
		base.ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome | ExtendClientAreaChromeHints.OSXThickTitleBar;

		this.GetObservable(WindowStateProperty)
			.Subscribe(x =>
			{
				PseudoClasses.Set(":maximized", x == WindowState.Maximized);
				PseudoClasses.Set(":fullscreen", x == WindowState.FullScreen);
			});

		PseudoClasses.Set(":windows", OperatingSystem.IsWindows());
		PseudoClasses.Set(":macos", OperatingSystem.IsMacOS());
		PseudoClasses.Set(":linux", OperatingSystem.IsLinux());

		if (OperatingSystem.IsMacOS())
		{
			TitleBarMargin = new Thickness(70, 0, 0, 0);
		}
		
		// WindowStateProperty.Changed.AddClassHandler<FluentWindow>((x, e) => x.HandleWindowStateChanged((WindowState)e.NewValue!));
	}

	// protected override void HandleWindowStateChanged(WindowState state)
	// {
	// 	if (OperatingSystem.IsMacOS())
	// 	{
	// 		if (state is WindowState.FullScreen)
	// 		{
	// 			TitleBarMargin = default;
	// 		}
	// 		else
	// 		{
	// 			TitleBarMargin = new Thickness(70, 0, 0, 0);
	// 		}
	//
	// 		OnPropertyChanged(nameof(TitleBarMargin));
	// 	}
	// 	
	// 	base.HandleWindowStateChanged(state);
	// }

	protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
	{
		base.OnApplyTemplate(e);

		if (OperatingSystem.IsWindows())
		{
			ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;
		}
		else
		{
			ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.OSXThickTitleBar | ExtendClientAreaChromeHints.SystemChrome;
		}


	}

	public new event PropertyChangedEventHandler? PropertyChanged;

	private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}