using Avalonia.Data.Converters;
using FileExplorerCore.Models;
using System.Globalization;

namespace FileExplorerCore.Converters
{
	public class SizeConverter : IValueConverter
	{
		static readonly string[] sizeSuffixes = { "B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is FileModel model)
			{
				if (model.Size == -1)
				{
					return null;
				}

				return ByteSize(model.Size);
			}
			else if (value is long size)
			{
				if (size == -1)
				{
					return null;
				}

				return ByteSize(size);
			}

			return String.Empty;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return String.Empty;
		}

		public static string ByteSize(long size)
		{
			const string formatTemplate = "{0}{1:0.##} {2}";

			if (size == 0)
			{
				return String.Format(formatTemplate, null, 0, sizeSuffixes[0]);
			}

			var absSize = Math.Abs((double)size);
			var fpPower = Math.Log(absSize, 1000);
			var intPower = (int)fpPower;
			var iUnit = intPower >= sizeSuffixes.Length
					? sizeSuffixes.Length - 1
					: intPower;
			var normSize = absSize / Math.Pow(1000, iUnit);

			return String.Format(
					formatTemplate,
					size < 0 ? "-" : null, normSize, sizeSuffixes[iUnit]);
		}
	}
}
