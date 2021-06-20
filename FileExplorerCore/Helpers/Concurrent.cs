using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FileExplorerCore.Helpers
{
	public static class Concurrent
	{
		static readonly ParallelOptions options = new()
		{
			MaxDegreeOfParallelism = Environment.ProcessorCount / 4
		};

		public static void While(Func<bool> condition, Action body)
		{
			Parallel.ForEach(new InfinitePartitioner(), options, (ignored, loopState) =>
			{
				if (condition())
					body();
				else
					loopState.Stop();
			});
		}

		public static ParallelLoopResult For(int begin, int count, Action<int> body)
		{
			return Parallel.For(begin, count, options, body);
		}

		public static ParallelLoopResult ForEach<T>(IEnumerable<T> collection, Action<T> body)
		{
			return Parallel.ForEach(collection, options, body);
		}

		public static IEnumerable<T> AsEnumerable<T>(ConcurrentStack<T> stack)
		{
			while (stack.TryPop(out var result))
			{
				yield return result;
			}
		}
	}
}