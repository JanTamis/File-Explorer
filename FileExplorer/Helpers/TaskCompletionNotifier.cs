using CommunityToolkit.Mvvm.ComponentModel;

namespace FileExplorer.Helpers;

/// <summary>
/// Watches a task and raises property-changed notifications when the task completes.
/// </summary>
public sealed partial class TaskCompletionNotifier<TResult> : ObservableObject
{
	// Gets the task being watched. This property never changes and is never <c>null</c>.
	public Task<TResult> Task { get; }

	// Gets the result of the task. Returns the default value of TResult if the task has not completed successfully.
	public TResult? Result => IsCompleted
		? Task.Result
		: _defaultResult;

	// Gets whether the task has completed.
	public bool IsCompleted => Task.IsCompleted;

	// Gets whether the task has completed successfully.
	public bool IsSuccessfullyCompleted => Task.Status is TaskStatus.RanToCompletion;

	// Gets whether the task has been canceled.
	public bool IsCanceled => Task.IsCanceled;

	// Gets whether the task has faulted.
	public bool IsFaulted => Task.IsFaulted;

	private readonly TResult? _defaultResult;

	public TaskCompletionNotifier(Task<TResult> task, TResult? defaultValue = default)
	{
		Task = task;
		_defaultResult = defaultValue;

		if (!task.IsCompleted)
		{
			var scheduler = SynchronizationContext.Current is null
				? TaskScheduler.Current
				: TaskScheduler.FromCurrentSynchronizationContext();
			
			task.ContinueWith(t =>
			{
				OnPropertyChanged(nameof(IsCompleted));

				if (t.IsCanceled)
				{
					OnPropertyChanged(nameof(IsCanceled));
				}
				else if (t.IsFaulted)
				{
					OnPropertyChanged(nameof(IsFaulted));
				}
				else
				{
					OnPropertyChanged(nameof(IsSuccessfullyCompleted));
					OnPropertyChanged(nameof(Result));
				}
			},
				CancellationToken.None,
				TaskContinuationOptions.ExecuteSynchronously,
				scheduler);
		}
	}
}