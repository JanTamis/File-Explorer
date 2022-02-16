using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace FileExplorerCore.Helpers
{
	/// <summary>
	/// Watches a task and raises property-changed notifications when the task completes.
	/// </summary>
	public sealed class TaskCompletionNotifier<TResult> : INotifyPropertyChanged
	{
		public TaskCompletionNotifier(Task<TResult> task)
		{
			Task = task;

			if (!task.IsCompleted)
			{
				var scheduler = (SynchronizationContext.Current is null) 
					? TaskScheduler.Current 
					: TaskScheduler.FromCurrentSynchronizationContext();

				task.ContinueWith(t =>
				{
					var propertyChanged = PropertyChanged;

					if (propertyChanged is not null)
					{
						propertyChanged(this, new PropertyChangedEventArgs(nameof(IsCompleted)));

						if (t.IsCanceled)
						{
							propertyChanged(this, new PropertyChangedEventArgs(nameof(IsCanceled)));
						}
						else if (t.IsFaulted)
						{
							propertyChanged(this, new PropertyChangedEventArgs(nameof(IsFaulted)));
						}
						else
						{
							propertyChanged(this, new PropertyChangedEventArgs(nameof(IsSuccessfullyCompleted)));
							propertyChanged(this, new PropertyChangedEventArgs(nameof(Result)));
						}
					}
				},
				CancellationToken.None,
				TaskContinuationOptions.ExecuteSynchronously,
				scheduler);
			}
		}

		// Gets the task being watched. This property never changes and is never <c>null</c>.
		public Task<TResult> Task { get; private set; }

		// Gets the result of the task. Returns the default value of TResult if the task has not completed successfully.
		public TResult? Result => Task.Status is TaskStatus.RanToCompletion ? Task.Result : default;

		// Gets whether the task has completed.
		public bool IsCompleted => Task.IsCompleted;

		// Gets whether the task has completed successfully.
		public bool IsSuccessfullyCompleted => Task.Status is TaskStatus.RanToCompletion;

		// Gets whether the task has been canceled.
		public bool IsCanceled => Task.IsCanceled;

		// Gets whether the task has faulted.
		public bool IsFaulted => Task.IsFaulted;

		public event PropertyChangedEventHandler? PropertyChanged = delegate { };
	}
}