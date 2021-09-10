﻿using System.Collections.Concurrent;

namespace FileExplorerCore.Helpers
{
	public class InfinitePartitioner : Partitioner<bool>
	{
		public override IList<IEnumerator<bool>> GetPartitions(int partitionCount)
		{
			if (partitionCount < 1)
				throw new ArgumentOutOfRangeException(nameof(partitionCount));

			return (from i in Enumerable.Range(0, partitionCount)
							select InfiniteEnumerator())
							.ToArray();
		}

		public override bool SupportsDynamicPartitions => true;

		public override IEnumerable<bool> GetDynamicPartitions()
		{
			while (true)
				yield return true;
		}

		private static IEnumerator<bool> InfiniteEnumerator()
		{
			while (true)
				yield return true;
		}
	}
}
