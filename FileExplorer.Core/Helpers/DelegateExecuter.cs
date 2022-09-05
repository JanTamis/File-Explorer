namespace FileExplorer.Core.Helpers;

public readonly struct DelegateExecutor : IDisposable
{
	private readonly Action _toExecute;

	public DelegateExecutor(Action beginExecute, Action toExecute)
	{
		_toExecute = toExecute;

		beginExecute();
	}

	public void Dispose()
	{
		_toExecute();
	}
}