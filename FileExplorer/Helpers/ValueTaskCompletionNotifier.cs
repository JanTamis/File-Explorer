using CommunityToolkit.Mvvm.ComponentModel;

namespace FileExplorer.Helpers;

/// <summary>
/// Watches a task and raises property-changed notifications when the task completes.
/// </summary>
public sealed partial class ValueTaskCompletionNotifier<TResult> : ObservableObject
{
	// Gets the task being watched. This property never changes and is never <c>null</c>.
	public ValueTask<TResult> Task { get; }

	// Gets the result of the task. Returns the default value of TResult if the task has not completed successfully.
	public TResult? Result => IsCompleted
		? Task.Result
		: _defaultResult;

	// Gets whether the task has completed.
	public bool IsCompleted => Task.IsCompleted;

	// Gets whether the task has completed successfully.
	public bool IsSuccessfullyCompleted => Task.IsCompletedSuccessfully;

	// Gets whether the task has been canceled.
	public bool IsCanceled => Task.IsCanceled;

	// Gets whether the task has faulted.
	public bool IsFaulted => Task.IsFaulted;

	private readonly TResult? _defaultResult;

	public ValueTaskCompletionNotifier(ValueTask<TResult> task, TResult? defaultValue = default)
	{
		Task = task;
		_defaultResult = defaultValue;

		if (!task.IsCompleted)
		{
			var scheduler = SynchronizationContext.Current is null
				? TaskScheduler.Current
				: TaskScheduler.FromCurrentSynchronizationContext();
			
			task.GetAwaiter().OnCompleted(() =>
			{
				OnPropertyChanged(nameof(IsCompleted));

				if (task.IsCanceled)
				{
					OnPropertyChanged(nameof(IsCanceled));
				}
				else if (task.IsFaulted)
				{
					OnPropertyChanged(nameof(IsFaulted));
				}
				else
				{
					OnPropertyChanged(nameof(IsSuccessfullyCompleted));
					OnPropertyChanged(nameof(Result));
				}
			});
		}
	}
	
	private async ValueTask<TResult> HandleTaskCompletion(ValueTask<TResult> task, TResult value)
	{
		try
		{
			value = await task.ConfigureAwait(false);
		}
		finally
		{
			OnPropertyChanged(nameof(IsCompleted));

			if (task.IsCanceled)
			{
				OnPropertyChanged(nameof(IsCanceled));
			}
			else if (task.IsFaulted)
			{
				OnPropertyChanged(nameof(IsFaulted));
			}
			else
			{
				OnPropertyChanged(nameof(IsSuccessfullyCompleted));
				OnPropertyChanged(nameof(Result));
			}
		}

		return value;
	}

	private async static ValueTask ContinueWith<TResult>(ValueTask<TResult> source,
	                                                     Action<ValueTask<TResult>> continuationAction)
	{
		// The source task is consumed after the await, and cannot be used further.
		ValueTask<TResult> completed;
		try
		{
			completed = new ValueTask<TResult>(await source.ConfigureAwait(false));
		}
		catch (OperationCanceledException oce)
		{
			var tcs = new TaskCompletionSource<TResult>();
			tcs.SetCanceled(oce.CancellationToken);
			completed = new ValueTask<TResult>(tcs.Task);
		}
		catch (Exception ex)
		{
			completed = ValueTask.FromException<TResult>(ex);
		}

		continuationAction(completed);
	}
}