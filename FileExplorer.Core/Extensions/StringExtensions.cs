using System.Collections;

namespace FileExplorer.Core.Extensions;

public static class StringExtensions
{
	public static IEnumerable<string> Split(this string str, char separator)
	{
		var index = 0;

		do
		{
			var nextIndex = str.AsSpan(index).IndexOf(separator) - 1;

			if (nextIndex >= 0)
			{
				yield return str.Substring(index, nextIndex - index);

				index = nextIndex + 2;
			}
		} while (index < str.Length && index >= 0);
	}
}