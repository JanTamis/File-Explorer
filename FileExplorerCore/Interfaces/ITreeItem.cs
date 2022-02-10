using System;
using System.Collections.Generic;

namespace FileExplorerCore.Interfaces
{
	public interface ITreeItem<TValue, TChildren> where TChildren : ITreeItem<TValue, TChildren>
	{
		IEnumerable<TChildren> Children { get; }

		TChildren? Parent { get; set; }

		TValue Value { get; set; }

		bool HasParent { get; }
		bool HasChildren { get; }

		TChildren GetRoot();

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
			var count = 0;

			foreach (var child in Children)
			{
				count += child.GetChildrenCount();
				count++;
			}

			return count;
		}

		IEnumerable<TChildren> EnumerateChildren(uint layers = UInt32.MaxValue);
	}
}