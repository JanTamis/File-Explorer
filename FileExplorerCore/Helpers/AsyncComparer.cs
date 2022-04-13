using System;
using System.Threading.Tasks;
using FileExplorerCore.Interfaces;

namespace FileExplorerCore.Helpers;

public readonly struct AsyncComparer<T> : IAsyncComparer<T>
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