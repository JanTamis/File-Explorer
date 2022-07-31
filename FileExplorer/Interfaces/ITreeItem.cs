using System;
using System.Collections.Generic;

namespace FileExplorer.Interfaces;

public interface ITreeItem<TValue, TChildren> where TChildren : class, ITreeItem<TValue, TChildren>
{
	IEnumerable<TChildren> Children { get; }

	TChildren? Parent { get; set; }

	TValue Value { get; set; }

	bool HasParent { get; }
	bool HasChildren { get; }

	TChildren GetRoot();

	IEnumerable<TChildren> EnumerateToRoot();

	IEnumerable<TValue> EnumerateValuesToRoot();

	public int GetChildrenCount();

	IEnumerable<TChildren> EnumerateChildren(uint layers = UInt32.MaxValue);
}