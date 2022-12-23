namespace FileExplorer.Core.Extensions;

public static class Runner
{
	private static readonly ConcurrentExclusiveSchedulerPair ConcurrentExclusiveSchedulerPrimary = new(TaskScheduler.Default, Environment.ProcessorCount / 2);
	private static readonly ConcurrentExclusiveSchedulerPair ConcurrentExclusiveSchedulerSecundairy = new(TaskScheduler.Default, Environment.ProcessorCount / 4);

	public static TaskScheduler PrimaryScheduler => ConcurrentExclusiveSchedulerPrimary.ConcurrentScheduler;
	public static TaskScheduler SecundairyScheduler => ConcurrentExclusiveSchedulerSecundairy.ConcurrentScheduler;

	public static Task Run(Action action, CancellationToken token = default)
	{
		return Task.Run(action, token);
	}

	public static Task<T> Run<T>(Func<T> action, CancellationToken token = default)
	{
		return Task.Run(action, token);
	}

	public static Task RunPrimary(Action action, CancellationToken token = default)
	{
		return Task.Factory.StartNew(action, token, TaskCreationOptions.None, ConcurrentExclusiveSchedulerPrimary.ConcurrentScheduler);
	}

	public static Task<T> RunPrimary<T>(Func<T> action, CancellationToken token = default)
	{
		return Task.Factory.StartNew(action, token, TaskCreationOptions.None, ConcurrentExclusiveSchedulerPrimary.ConcurrentScheduler);
	}

	public static Task RunSecundairy(Action action, CancellationToken token = default)
	{
		return Task.Factory.StartNew(action, token, TaskCreationOptions.None, ConcurrentExclusiveSchedulerSecundairy.ConcurrentScheduler);
	}

	public static Task<T> RunSecundairy<T>(Func<T> action, CancellationToken token = default)
	{
		return Task.Factory.StartNew(action, token, TaskCreationOptions.None, ConcurrentExclusiveSchedulerSecundairy.ConcurrentScheduler);
	}
}