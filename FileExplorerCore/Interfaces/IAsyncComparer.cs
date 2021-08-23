namespace FileExplorerCore.Interfaces
{
	public interface IAsyncComparer<T>
	{
		ValueTask<int> Compare(T? x, T? y);
	}
}
