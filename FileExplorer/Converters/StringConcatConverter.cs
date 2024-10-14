using System.Globalization;
using Avalonia.Data.Converters;
using FileExplorer.Interfaces;

namespace FileExplorer.Converters;

public class StringConcatConverter : IMultiValueConverter, ISingleton<StringConcatConverter>
{
	public static StringConcatConverter Instance { get; } = new();
	
	public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
	{
		return String.Concat(values);
	}
}