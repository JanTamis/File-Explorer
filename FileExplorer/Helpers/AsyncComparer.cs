using FileExplorer.Core.Interfaces;
using FileExplorer.Interfaces;

namespace FileExplorer.Helpers;

public class AsyncComparer<T> : IAsyncComparer<T>
{
	readonly AsyncComparison<T> comparison;

	public AsyncComparer(AsyncComparison<T> comparison)
	{
		this.comparison = comparison;
	}

	public ValueTask<int> CompareAsync(T x, T y)
	{
		return comparison(x, y);
	}
}