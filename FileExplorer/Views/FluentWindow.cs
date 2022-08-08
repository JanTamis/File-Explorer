using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Platform;
using Avalonia.Styling;

namespace FileExplorer.Views;

public partial class FluentWindow : Window, IStyleable
{
	Type IStyleable.StyleKey => typeof(Window);

	public FluentWindow()
	{
		ExtendClientAreaToDecorationsHint = true;
		ExtendClientAreaTitleBarHeightHint = -1;

		this.GetObservable(WindowStateProperty)
			.Subscribe(x =>
			{
				PseudoClasses.Set(":maximized", x == WindowState.Maximized);
				PseudoClasses.Set(":fullscreen", x == WindowState.FullScreen);
			});

		PseudoClasses.Set(":windows", OperatingSystem.IsWindows());
		PseudoClasses.Set(":macos", OperatingSystem.IsMacOS());
		PseudoClasses.Set(":linux", OperatingSystem.IsLinux());
	}

	public void Minimize()
	{
		WindowState = WindowState.Minimized;
	}

	public void ChangeState()
	{
		WindowState = WindowState == WindowState.Normal
			? WindowState.Maximized
			: WindowState.Normal;
	}

	protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
	{
		base.OnApplyTemplate(e);

		ExtendClientAreaChromeHints =	ExtendClientAreaChromeHints.NoChrome;
	}
}