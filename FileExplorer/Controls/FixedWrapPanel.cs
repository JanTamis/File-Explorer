using Avalonia;
using Avalonia.Layout;

namespace FileExplorer.Controls;

public sealed class FixedWrapPanel : UniformGridLayout
{
	public double ItemWidth { get; set; }

	protected override Size ArrangeOverride(VirtualizingLayoutContext context, Size finalSize)
	{
		MinItemWidth = Math.Floor(finalSize.Width / Math.Max(1, Math.Floor(finalSize.Width / ItemWidth)));

		return base.ArrangeOverride(context, finalSize);
	}
}