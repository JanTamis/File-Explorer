using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace FileExplorer.Controls;

public class ProgressChart : TemplatedControl
{
	private readonly SortedList<double, long> _data = new();
	
	private long _max;

	public override void Render(DrawingContext context)
	{
		var geometry = new StreamGeometry();

		using (var streamGeometry = geometry.Open())
		{
			if (_data.Count > 0)
			{
				using var enumerator = _data.GetEnumerator();

				if (enumerator.MoveNext())
				{
					streamGeometry.BeginFigure(new Point(0,  Height - Height / (enumerator.Current.Value / (double)_max)), true);
				}

				var last = enumerator.Current;

				while (enumerator.MoveNext())
				{
					streamGeometry.LineTo(new Point(Width * enumerator.Current.Value, Height / (enumerator.Current.Value / (double)_max)));

					last = enumerator.Current;
				}
				
				streamGeometry.LineTo(new Point(Width * last.Key, Height));
				streamGeometry.LineTo(new Point(0, Height));
				
				streamGeometry.EndFigure(true);
			}
		}
		
		context.DrawGeometry(Background, new ImmutablePen(Foreground!.ToImmutable()), geometry.Clone());
	}
}