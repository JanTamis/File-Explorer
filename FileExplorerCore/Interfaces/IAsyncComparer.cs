namespace FileExplorerCore.Interfaces
{
	public interface IAsyncComparer<T>
	{
		Task<int> CompareAsync(T? x, T? y);
	}

	public delegate Task<int> AsyncComparison<in T>(T x, T y);
}
