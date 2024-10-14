using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Humanizer;

namespace FileExplorer.Controls;

public class TimeSpanTextBlock : TextBlock
{
	public static readonly StyledProperty<TimeSpan> TimeSpanProperty =
		AvaloniaProperty.Register<TimeSpanTextBlock, TimeSpan>(nameof(TimeSpan));
	
	public TimeSpan TimeSpan
	{
		get => GetValue(TimeSpanProperty);
		init => SetValue(TimeSpanProperty, value);
	}

	protected override void OnSizeChanged(SizeChangedEventArgs e)
	{
		string text;
		FormattedText layout;

		var i = 4;

		do
		{
			text = TimeSpan.Humanize(i);
			layout = CreateFormattedText(text);

			i--;
		} while (layout.Width > e.NewSize.Width && i > 1);

		if (layout.Width > e.NewSize.Width)
		{
			text = TimeSpan.ToString("g");
		}

		Text = text;
		base.OnSizeChanged(e);
	}
	
	private FormattedText CreateFormattedText(string text)
	{
		return new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(FontFamily, FontStyle, FontWeight), FontSize, Brushes.Black);
	}
}