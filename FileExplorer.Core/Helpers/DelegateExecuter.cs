namespace FileExplorer.Core.Helpers;

public readonly struct DelegateExecutor : IDisposable
{
	private readonly Action _toExecute;

	public DelegateExecutor(Action beginExecute, Action toExecute) : this(toExecute)
	{
		beginExecute();
	}

	public DelegateExecutor(Action toExecute)
	{
		_toExecute = toExecute;
	}

	public void Dispose()
	{
		_toExecute();
	}
}