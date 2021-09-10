using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using FileExplorerCore.Models;

namespace FileExplorerCore.Controls
{
	// https://wpf.2000things.com/2014/10/09/1176-custom-panel-part-viii/
	public class WeightedPanel : Panel
	{
		public static AttachedProperty<double> WeightProperty = AvaloniaProperty.RegisterAttached<WeightedPanel, Control, double>("Weight", 0.0);

		static WeightedPanel()
		{
			AffectsMeasure<WeightedPanel>(WeightProperty);
			AffectsArrange<WeightedPanel>(WeightProperty);
		}

		/// <summary>
		/// Accessor for Attached property <see cref="CommandProperty"/>.
		/// </summary>
		public static void SetWeight(AvaloniaObject element, double weight)
		{
			element.SetValue(WeightProperty, weight);
		}

		/// <summary>
		/// Accessor for Attached property <see cref="CommandProperty"/>.
		/// </summary>
		public static double GetWeight(AvaloniaObject element)
		{
			if (element is ContentPresenter { Content: ExtensionModel model })
			{
				return model.TotalSize;
			}
			else if (element is ContentPresenter { Content: FileIndexModel indexModel })
			{
				if (indexModel.IsFolder)
				{
					indexModel.TaskSize.Wait();

					return indexModel.TaskSize.Result;
				}
				return 0;
			}

			return element.GetValue(WeightProperty);
		}

		protected override Size MeasureOverride(Size availableSize)
		{
			foreach (ChildAndRect child in ChildrenTreemapOrder(GetChildren(), availableSize))
				if (!Double.IsNaN(child.Rectangle.Width) && !Double.IsNaN(child.Rectangle.Height))
					child.Element.Measure(child.Rectangle.Size);

			return availableSize;
		}

		protected override Size ArrangeOverride(Size finalSize)
		{
			foreach (ChildAndRect child in ChildrenTreemapOrder(GetChildren(), finalSize))
				if (!Double.IsNaN(child.Rectangle.Width) && !Double.IsNaN(child.Rectangle.Height))
					child.Element.Arrange(child.Rectangle);

			return finalSize;
		}

		private double TotalChildWeight()
		{
			double weightSum = 0L;
			foreach (Control elem in Children)
				weightSum += GetWeight(elem);

			return weightSum;
		}

		/// <summary>
		/// Return child elements orderd by weight (largest to
		/// smallest), passing back Rect for each child
		/// (size and location), implementing a (crude)
		/// treemap.
		/// </summary>
		/// <param name="elems">Child elements to measure/arrange</param>
		/// <param name="containerSize">Available container size</param>
		/// <returns></returns>
		private IEnumerable<ChildAndRect> ChildrenTreemapOrder(IEnumerable<IControl> elems, Size containerSize)
		{
			double remainingWeight = TotalChildWeight();
			double totalWeight = remainingWeight;

			double top = 0.0;
			double left = 0.0;

			// Alternate between left edge and top edge
			bool leftEdge;

			// Sort by weight
			var childrenByWeight = elems.OrderByDescending(o => GetWeight(o as AvaloniaObject));

			// Allocate space for each child, one at a time.
			// Moving left to right, top to bottom
			foreach (Control child in childrenByWeight)
			{
				leftEdge = (containerSize.Width - left) > (containerSize.Height - top);

				Size size;

				double childWeight = GetWeight(child);

				double pctArea = childWeight / remainingWeight;
				remainingWeight -= childWeight;

				// Entire height, proportionate width
				if (leftEdge)
					size = new Size(pctArea * (containerSize.Width - left), containerSize.Height - top);

				// Top edge - Entire width, proportionate height
				else
					size = new Size(containerSize.Width - left, pctArea * (containerSize.Height - top));

				yield return new ChildAndRect { Element = child, Rectangle = new Rect(new Point(left, top), size) };

				if (leftEdge)
					left += size.Width;
				else
					top += size.Height;
			}
		}

		public IEnumerable<IControl> GetChildren()
		{
			var count = Children.Count;

			for (int i = 0; i < count; i++)
			{
				yield return Children[i];
				count = Children.Count;
			}
		}
	}

	public class ChildAndRect
	{
		public Control Element { get; set; }
		public Rect Rectangle { get; set; }
	}
}
