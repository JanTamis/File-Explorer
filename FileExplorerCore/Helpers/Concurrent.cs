using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace FileExplorerCore.Helpers
{
	public static class Concurrent
	{
		private static ExecutionDataflowBlockOptions options = new()
		{
			MaxDegreeOfParallelism = 1,//(int)Math.Log2(Environment.ProcessorCount)
		};
		
		public static Task For(int begin, int count, Action<int> body)
		{
			var block = new ActionBlock<int>(body, options);

			for (var i = begin; i < count; i++)
			{
				block.Post(i);
			}
			
			block.Complete();
			return block.Completion;
		}

		public static Task ForEach<T>(IEnumerable<T> collection, Action<T> body)
		{
			var block = new ActionBlock<T>(body, options);

			foreach (var item in collection)
			{
				block.Post(item);
			}
			
			block.Complete();
			return block.Completion;
		}

		public static IEnumerable<T> AsEnumerable<T>(ConcurrentStack<T> stack)
		{
			var attempts = 0;

			while (!stack.IsEmpty && ++attempts <= 10)
			{
				while (stack.TryPop(out var result))
				{
					yield return result;
				}
			}
		}

		public static IEnumerable<T> AsEnumerable<T>(ConcurrentBag<T> stack)
		{
			var attempts = 0;

			while (!stack.IsEmpty && ++attempts <= 10)
			{
				while (stack.TryTake(out var result))
				{
					yield return result;
				}
			}
		}
	}
}