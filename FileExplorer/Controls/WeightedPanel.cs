﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using FileExplorer.Models;

namespace FileExplorer.Controls;

// https://wpf.2000things.com/2014/10/09/1176-custom-panel-part-viii/
public sealed class WeightedPanel : Panel
{
	public static readonly AttachedProperty<double> WeightProperty = AvaloniaProperty.RegisterAttached<WeightedPanel, Control, double>("Weight", 0.0);

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
		return element switch
		{
			ContentPresenter { Content: ExtensionModel model, }                          => model.TotalSize,
			ContentPresenter { Content: FileIndexModel { IsFolder: true, } indexModel, } => indexModel.TaskSize.Result,
			ContentPresenter { Content: FileIndexModel, }                                => 0,
			_                                                                            => element.GetValue(WeightProperty)
		};
	}

	protected override Size MeasureOverride(Size availableSize)
	{
		foreach (var child in ChildrenTreemapOrder(GetChildren(), availableSize))
		{
			if (!Double.IsNaN(child.Rectangle.Width) && !Double.IsNaN(child.Rectangle.Height))
			{
				child.Element?.Measure(child.Rectangle.Size);
			}
		}

		return availableSize;
	}

	protected override Size ArrangeOverride(Size finalSize)
	{
		foreach (var child in ChildrenTreemapOrder(GetChildren(), finalSize))
		{
			if (!Double.IsNaN(child.Rectangle.Width) && !Double.IsNaN(child.Rectangle.Height))
			{
				child.Element?.Arrange(child.Rectangle);
			}
		}

		return finalSize;
	}

	private double TotalChildWeight()
	{
		double weightSum = 0L;
		
		foreach (var elem in Children)
		{
			weightSum += GetWeight(elem);
		}

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
	private IEnumerable<ChildAndRect> ChildrenTreemapOrder(IEnumerable<Control> elems, Size containerSize)
	{
		var remainingWeight = TotalChildWeight();
		var totalWeight = remainingWeight;

		var top = 0.0;
		var left = 0.0;

		// Alternate between left edge and top edge
		bool leftEdge;

		// Sort by weight
		var childrenByWeight = elems.OrderByDescending(GetWeight);

		// Allocate space for each child, one at a time.
		// Moving left to right, top to bottom
		foreach (Control child in childrenByWeight)
		{
			leftEdge = (containerSize.Width - left) > (containerSize.Height - top);

			Size size;

			var childWeight = GetWeight(child);

			var pctArea = childWeight / remainingWeight;
			remainingWeight -= childWeight;

			// Entire height, proportionate width
			if (leftEdge)
			{
				size = new Size(pctArea * (containerSize.Width - left), containerSize.Height - top);
			}

			// Top edge - Entire width, proportionate height
			else
			{
				size = new Size(containerSize.Width - left, pctArea * (containerSize.Height - top));
			}

			yield return new ChildAndRect { Element = child, Rectangle = new Rect(new Point(left, top), size), };

			if (leftEdge)
			{
				left += size.Width;
			}
			else
			{
				top += size.Height;
			}
		}
	}

	public IEnumerable<Control> GetChildren()
	{
		var count = Children.Count;

		for (var i = 0; i < count; i++)
		{
			yield return Children[i];
			count = Children.Count;
		}
	}
}

public sealed class ChildAndRect
{
	public Control? Element { get; set; }
	public Rect Rectangle { get; set; }
}