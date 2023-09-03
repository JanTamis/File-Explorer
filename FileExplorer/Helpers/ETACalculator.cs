using System.Diagnostics;
using ProgressItem = System.Collections.Generic.KeyValuePair<long, double>;

namespace FileExplorer.Helpers;

/// <summary> Calculates the "Estimated Time of Arrival"
/// (or more accurately, "Estimated Time of Completion"),
/// based on a "rolling average" of progress over time.
/// </summary>
public class EtaCalculator
{
	/// <summary>
	/// </summary>
	/// <param name="minimumData">
	/// The minimum number of data points required before ETA can be calculated.
	/// </param>
	/// <param name="maximumDuration">
	/// Determines how many seconds of data will be used to calculate the ETA.
	/// </param>
	public EtaCalculator(int minimumData, double maximumDuration)
	{
		_minimumData = minimumData;
		_maximumTicks = (long) (maximumDuration * Stopwatch.Frequency);
		_queue = new Queue<ProgressItem>(minimumData * 2);
		_timer = Stopwatch.StartNew();
	}

	private readonly int _minimumData;
	private readonly long _maximumTicks;
	private readonly Stopwatch _timer;
	private readonly Queue<ProgressItem> _queue;

	private ProgressItem current;
	private ProgressItem oldest;

	public void Reset()
	{
		_queue.Clear();

		_timer.Reset();
		_timer.Start();
	}

	private void ClearExpired()
	{
		var expired = _timer.ElapsedTicks - _maximumTicks;
		while (_queue.Count > _minimumData && _queue.Peek().Key < expired)
		{
			oldest = _queue.Dequeue();
		}
	}

	/// <summary> Adds the current progress to the calculation of ETA.
	/// </summary>
	/// <param name="progress">The current level of completion.
	/// Must be between 0.0 and 1.0 (inclusively).</param>
	public void Update(double progress)
	{
		// If progress hasn't changed, ignore:
		if (Math.Abs(current.Value - progress) < Double.Epsilon)
		{
			return;
		}

		// Clear space for this item:
		ClearExpired();

		// Queue this item:
		var currentTicks = _timer.ElapsedTicks;
		current = new ProgressItem(currentTicks, progress);
		_queue.Enqueue(current);

		// See if its the first item:
		if (_queue.Count == 1)
		{
			oldest = current;
		}
	}

	/// <summary> Calculates the Estimated Time Remaining
	/// </summary>
	public TimeSpan ETR
	{
		get
		{
			// Create local copies of the oldest & current,
			// so that another thread can update them without locking:
			var (oldestKey, oldestValue) = oldest;
			var (currentKey, currentValue) = current;

			// Make sure we have enough items:
			if (_queue.Count < _minimumData || Math.Abs(oldestValue - currentValue) < Double.Epsilon)
			{
				return TimeSpan.MaxValue;
			}

			// Calculate the estimated finished time:
			var finishedInTicks = (1.0d - currentValue) * (currentKey - oldestKey) / (currentValue - oldestValue);

			return TimeSpan.FromSeconds(finishedInTicks / Stopwatch.Frequency);
		}
	}

	/// <summary> Calculates the Estimated Time of Arrival (Completion)
	/// </summary>
	public DateTime ETA => DateTime.Now.Add(ETR);

	/// <summary> Returns True when there is enough data to calculate the ETA.
	/// Returns False if the ETA is still calculating.
	/// </summary>
	public bool ETAIsAvailable =>
		// Make sure we have enough items:
		_queue.Count >= _minimumData && Math.Abs(oldest.Value - current.Value) > Single.Epsilon;
}