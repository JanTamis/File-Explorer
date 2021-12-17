using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using FileExplorerCore.Models;
using System;
using System.Collections.Generic;

namespace FileExplorerCore.Controls
{
	public class TreeMapsPanel : Panel
	{
		#region fields

		private Rect _emptyArea;
		private double _weightSum = 0;
		private readonly List<WeightUIElement> _items = new List<WeightUIElement>();

		#endregion

		#region dependency properties

		public static AttachedProperty<double> WeightProperty = AvaloniaProperty.RegisterAttached<WeightedPanel, Control, double>("Weight", 0.0);

		#endregion

		#region enum

		protected enum RowOrientation
		{
			Horizontal,
			Vertical
		}

		#endregion

		#region properties

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

		protected Rect EmptyArea
		{
			get { return _emptyArea; }
			set { _emptyArea = value; }
		}

		protected List<WeightUIElement> ManagedItems
		{
			get { return _items; }
		}

		#endregion

		#region protected methods

		protected override Size ArrangeOverride(Size arrangeSize)
		{
			foreach (WeightUIElement child in ManagedItems)
				if (!double.IsNaN(child.ComputedSize.Width) && !double.IsNaN(child.ComputedSize.Height))
					child.UIElement.Arrange(new Rect(child.ComputedLocation, child.ComputedSize));

			return arrangeSize;
		}

		protected override Size MeasureOverride(Size constraint)
		{
			EmptyArea = new Rect(0, 0, constraint.Width, constraint.Height);
			PrepareItems();

			double area = EmptyArea.Width * EmptyArea.Height;
			foreach (WeightUIElement item in ManagedItems)
				item.RealArea = area * item.Weight / _weightSum;

			ComputeBounds();

			foreach (WeightUIElement child in ManagedItems)
			{
				if (IsValidSize(child.ComputedSize))
					child.UIElement.Measure(child.ComputedSize);
				else
					child.UIElement.Measure(new Size(0, 0));
			}

			return constraint;
		}

		protected virtual void ComputeBounds()
		{
			ComputeTreeMaps(ManagedItems);
		}

		protected double GetShortestSide()
		{
			return Math.Min(EmptyArea.Width, EmptyArea.Height);
		}

		protected RowOrientation GetOrientation()
		{
			return (EmptyArea.Width > EmptyArea.Height ? RowOrientation.Horizontal : RowOrientation.Vertical);
		}

		protected virtual Rect GetRectangle(RowOrientation orientation, WeightUIElement item, double x, double y, double width, double height)
		{
			if (orientation == RowOrientation.Horizontal)
				return new Rect(x, y, item.RealArea / height, height);
			else
				return new Rect(x, y, width, item.RealArea / width);
		}

		protected virtual void ComputeNextPosition(RowOrientation orientation, ref double xPos, ref double yPos, double width, double height)
		{
			if (orientation == RowOrientation.Horizontal)
				xPos += width;
			else
				yPos += height;
		}

		protected void ComputeTreeMaps(List<WeightUIElement> items)
		{
			RowOrientation orientation = GetOrientation();

			double areaSum = 0;

			foreach (WeightUIElement item in items)
				areaSum += item.RealArea;

			Rect currentRow;
			if (orientation == RowOrientation.Horizontal)
			{
				currentRow = new Rect(_emptyArea.X, _emptyArea.Y, areaSum / _emptyArea.Height, _emptyArea.Height);
				_emptyArea = new Rect(_emptyArea.X + currentRow.Width, _emptyArea.Y, Math.Max(0, _emptyArea.Width - currentRow.Width), _emptyArea.Height);
			}
			else
			{
				currentRow = new Rect(_emptyArea.X, _emptyArea.Y, _emptyArea.Width, areaSum / _emptyArea.Width);
				_emptyArea = new Rect(_emptyArea.X, _emptyArea.Y + currentRow.Height, _emptyArea.Width, Math.Max(0, _emptyArea.Height - currentRow.Height));
			}

			double prevX = currentRow.X;
			double prevY = currentRow.Y;

			foreach (WeightUIElement item in items)
			{
				Rect rect = GetRectangle(orientation, item, prevX, prevY, currentRow.Width, currentRow.Height);

				item.AspectRatio = rect.Width / rect.Height;
				item.ComputedSize = rect.Size;
				item.ComputedLocation = rect.TopLeft;

				ComputeNextPosition(orientation, ref prevX, ref prevY, rect.Width, rect.Height);
			}
		}

		#endregion

		#region private methods

		private bool IsValidSize(Size size)
		{
			return (!size.IsDefault && size.Width > 0 && !double.IsNaN(size.Width) && size.Height > 0 && !double.IsNaN(size.Height));
		}

		private bool IsValidItem(WeightUIElement item)
		{
			return (item != null && !double.IsNaN(item.Weight) && Math.Round(item.Weight, 0) != 0);
		}

		private void PrepareItems()
		{
			_weightSum = 0;
			ManagedItems.Clear();

			foreach (Control child in Children)
			{
				var element = new WeightUIElement(child, TreeMapsPanel.GetWeight(child));

				if (IsValidItem(element))
				{
					_weightSum += element.Weight;
					ManagedItems.Add(element);
				}
				else
				{
					element.ComputedSize = Size.Empty;
					element.ComputedLocation = new Point(0, 0);
					element.UIElement.Measure(element.ComputedSize);
					element.UIElement.IsVisible = false;
				}
			}

			ManagedItems.Sort(WeightUIElement.CompareByValueDecreasing);
		}

		#endregion

		#region inner classes

		protected class WeightUIElement
		{
			#region fields

			private readonly double _weight;
			private double _area;
			private readonly Control _element;
			private Size _desiredSize;
			private Point _desiredLocation;
			private double _ratio;

			#endregion

			#region ctors

			public WeightUIElement(Control element, double weight)
			{
				_element = element;
				_weight = weight;
			}

			#endregion

			#region properties

			internal Size ComputedSize
			{
				get { return _desiredSize; }
				set { _desiredSize = value; }
			}

			internal Point ComputedLocation
			{
				get { return _desiredLocation; }
				set { _desiredLocation = value; }
			}
			public double AspectRatio
			{
				get { return _ratio; }
				set { _ratio = value; }
			}
			public double Weight
			{
				get { return _weight; }
			}
			public double RealArea
			{
				get { return _area; }
				set { _area = value; }
			}

			public Control UIElement
			{
				get { return _element; }
			}

			#endregion

			#region static members

			public static int CompareByValueDecreasing(WeightUIElement x, WeightUIElement y)
			{
				if (x == null)
				{
					if (y == null)
						return -1;
					else
						return 0;
				}
				else
				{
					if (y == null)
						return 1;
					else
						return x.Weight.CompareTo(y.Weight) * -1;
				}
			}

			#endregion
		}

		#endregion

	}
}
