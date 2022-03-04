using System;
using Avalonia.Data.Converters;
using FileExplorerCore.Models;
using System.Globalization;
using Humanizer;

namespace FileExplorerCore.Converters
{
	public class EditedOnConverter : IValueConverter
	{
		public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			if (value is FileModel model)
			{
				return model.EditedOn.Humanize();
			}

			return String.Empty;
		}

		public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			return String.Empty;
		}
	}
}