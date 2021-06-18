using System;
using System.Linq;
using System.Threading.Tasks;

namespace FileExplorerCore.Helpers
{
	public static class Concurrent
	{
		public static void While(ParallelOptions options, Func<bool> condition, Action body)
		{
			Parallel.ForEach(new InfinitePartitioner(), options, (ignored, loopState) =>
			{
				if (condition())
					body();
				else
					loopState.Stop();
			});
		}
	}
}
