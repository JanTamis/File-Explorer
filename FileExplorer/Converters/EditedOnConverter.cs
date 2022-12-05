using Avalonia.Data.Converters;
using System.Globalization;
using FileExplorer.Models;
using Humanizer;

namespace FileExplorer.Converters;

public sealed class EditedOnConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is FileModel model)
		{
			return model.EditedOn.Humanize();
		}

		return Task.FromResult(String.Empty);
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return String.Empty;
	}
}