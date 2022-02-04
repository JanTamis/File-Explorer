using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace FileExplorerCore.Helpers
{
	// Watches a task and raises property-changed notifications when the task completes.
	public sealed class TaskCompletionNotifier<TResult> : INotifyPropertyChanged
	{
		public TaskCompletionNotifier(Task<TResult> task)
		{
			Task = task;

			InitializeListener();

			//if (!task.IsCompleted)
			//{
			//	var scheduler = (SynchronizationContext.Current is null) ? TaskScheduler.Current : TaskScheduler.FromCurrentSynchronizationContext();
			//	task.ContinueWith(t =>
			//	{
					
			//	},
			//	CancellationToken.None,
			//	TaskContinuationOptions.ExecuteSynchronously,
			//	scheduler);
			//}
		}

		private async Task InitializeListener()
		{
			if (!Task.IsCompleted)
			{
				await Task;
			}

			var propertyChanged = PropertyChanged;

			if (propertyChanged is not null)
			{
				propertyChanged(this, new PropertyChangedEventArgs(nameof(IsCompleted)));

				if (Task.IsCanceled)
				{
					propertyChanged(this, new PropertyChangedEventArgs(nameof(IsCanceled)));
				}
				else if (Task.IsFaulted)
				{
					propertyChanged(this, new PropertyChangedEventArgs(nameof(IsFaulted)));
				}
				else
				{
					propertyChanged(this, new PropertyChangedEventArgs(nameof(IsSuccessfullyCompleted)));
					propertyChanged(this, new PropertyChangedEventArgs(nameof(Result)));
				}
			}
		}

		// Gets the task being watched. This property never changes and is never <c>null</c>.
		public Task<TResult> Task { get; private set; }

		// Gets the result of the task. Returns the default value of TResult if the task has not completed successfully.
		public TResult? Result => (Task.Status is TaskStatus.RanToCompletion) ? Task.Result : default;

		// Gets whether the task has completed.
		public bool IsCompleted => Task.IsCompleted;

		// Gets whether the task has completed successfully.
		public bool IsSuccessfullyCompleted => Task.Status == TaskStatus.RanToCompletion;

		// Gets whether the task has been canceled.
		public bool IsCanceled => Task.IsCanceled;

		// Gets whether the task has faulted.
		public bool IsFaulted => Task.IsFaulted;

		public event PropertyChangedEventHandler PropertyChanged = delegate { };
	}
}
