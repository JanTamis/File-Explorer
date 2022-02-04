using System;
using System.Collections.Generic;

namespace FileExplorerCore.Interfaces
{
	public interface ITreeItem<TValue, TChildren> where TChildren : ITreeItem<TValue, TChildren>
	{
		List<TChildren> Children { get; }

		TChildren Parent { get; set; }

		TValue Value { get; set; }

		bool HasParent { get; }
		bool HasChildren { get; }

		TChildren this[Index index] { get; set; }

		TChildren GetRoot();
		void Remove();

		IEnumerable<TChildren> EnumerateToRoot();


		IEnumerable<TValue> EnumerateValuesToRoot()
		{
			var item = this;

			while (item!.HasParent)
			{
				yield return item.Value;

				item = item.Parent;
			}

			yield return item.Value;
		}

		public int GetChildrenCount()
		{
			var currentChildren = Children;
			var count = currentChildren.Count;

			var currentChildrenCount = currentChildren.Count;

			for (var i = 0; i < currentChildrenCount; i++)
			{
				count += currentChildren[i].GetChildrenCount();

				currentChildrenCount = currentChildren.Count;
			}

			return count;
		}

		IEnumerable<TChildren> EnumerateChildren(uint layers = UInt32.MaxValue);

		IAsyncEnumerable<TChildren> EnumerateChildrenAsync(uint layers = UInt32.MaxValue);

		IEnumerable<TChildren> EnumerateChildrenWithoutInitialize();
	}
}