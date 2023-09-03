using FileExplorer.Core.Interfaces;

namespace FileExplorer.Helpers;

public class AsyncComparer<T>(AsyncComparison<T> comparison) : IAsyncComparer<T>
{
	public ValueTask<int> CompareAsync(T x, T y)
	{
		return comparison(x, y);
	}
}