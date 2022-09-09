using Avalonia;
using Avalonia.Layout;

namespace FileExplorer.Controls
{
	public class FixedWrapPanel : UniformGridLayout
	{
		public FixedWrapPanel()
		{
			
		}

		protected override Size ArrangeOverride(VirtualizingLayoutContext context, Size finalSize)
		{
			MinItemWidth = (int)(finalSize.Width / Math.Max(1, (int)(finalSize.Width / 200)));

			return base.ArrangeOverride(context, finalSize);
		}
	}
}