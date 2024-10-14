using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace FileExplorer.Controls;

public class DateTimeTextBlock : TextBlock
{
	public static readonly StyledProperty<DateTime> DateTimeProperty =
		AvaloniaProperty.Register<TimeSpanTextBlock, DateTime>(nameof(DateTime));

	public DateTime DateTime
	{
		get => GetValue(DateTimeProperty);
		set => SetValue(DateTimeProperty, value);
	}
	protected override void OnSizeChanged(SizeChangedEventArgs e)
	{
		var dateText = DateTime.ToString("F");
		var layout = CreateTextLayout(dateText);

		if (layout.Width > e.NewSize.Width)
		{
			dateText = $"{DateTime:ddd} {DateTime:M} {DateTime:yyyy} {DateTime:T}";
			layout = CreateTextLayout(dateText);
		}

		if (layout.Width > e.NewSize.Width)
		{
			dateText = $"{DateTime:M} {DateTime:yyyy} {DateTime:T}";
			layout = CreateTextLayout(dateText);
		}

		if (layout.Width > e.NewSize.Width)
		{
			dateText = DateTime.ToString("G");
			layout = CreateTextLayout(dateText);
		}
		
		if (layout.Width > e.NewSize.Width)
		{
			dateText = DateTime.ToString("g");
			layout = CreateTextLayout(dateText);
		}

		if (layout.Width > e.NewSize.Width)
		{
			dateText = DateTime.ToString("d");
			layout = CreateTextLayout(dateText);
		}

		Text = dateText;
		base.OnSizeChanged(e);
	}

	private FormattedText CreateTextLayout(string text)
	{
		return new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(FontFamily, FontStyle, FontWeight), FontSize, Brushes.Black);
	}
}