using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FileExplorerCore.Helpers
{
	public static class Concurrent
	{
		static readonly ParallelOptions options = new()
		{
			MaxDegreeOfParallelism = 2
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

		public static ParallelLoopResult ForEach<T>(IEnumerable<T> collection, Action<T> body, int maxConcurrency)
		{
			var options = new ParallelOptions()
			{
				MaxDegreeOfParallelism = Math.Max(maxConcurrency, 1),
			};

			return Parallel.ForEach(collection, options, body);
		}

		public static IEnumerable<T> AsEnumerable<T>(ConcurrentStack<T> stack)
		{
			return new StackEnumerable<T>(stack);
			//while (stack.TryPop(out var result))
			//{
			//	yield return result;
			//}
		}

		class StackEnumerable<T> : IEnumerable<T>
		{
			private ConcurrentStack<T> stack;

			public StackEnumerable(ConcurrentStack<T> stack)
			{
				this.stack = stack;
			}

			IEnumerator<T> IEnumerable<T>.GetEnumerator()
			{
				return new StackEnumerator<T>(stack);
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return new StackEnumerator<T>(stack);
			}

		}
		struct StackEnumerator<T> : IEnumerator<T>
		{
			private ConcurrentStack<T> stack;
			private T current;

			public StackEnumerator(ConcurrentStack<T> stack)
			{
				this.stack = stack;
				current = default;
			}

			public T Current => current;

			object IEnumerator.Current => current;

			public void Dispose()
			{
				
			}

			public bool MoveNext()
			{
				return stack.TryPop(out current);
			}

			public void Reset()
			{

			}
		}
	}
}