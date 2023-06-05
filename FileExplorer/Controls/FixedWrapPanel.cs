using Avalonia;
using Avalonia.Layout;

namespace FileExplorer.Controls;

public sealed class FixedWrapPanel : UniformGridLayout
{
	protected override Size ArrangeOverride(VirtualizingLayoutContext context, Size finalSize)
	{
		MinItemWidth = Math.Floor(finalSize.Width / Math.Max(1, Math.Floor(finalSize.Width / 175)));

		return base.ArrangeOverride(context, finalSize);
	}
}