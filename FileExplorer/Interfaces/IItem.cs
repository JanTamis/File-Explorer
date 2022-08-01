using System;
using System.Collections.Generic;
using FileExplorerCore.Helpers;

namespace FileExplorerCore.Interfaces;

public interface IItem
{
	bool IsSelected { get; set; }
	bool IsFolder { get; }
	bool IsRoot { get; }

	IEnumerable<IItem> Children { get; }

	string Extension { get; }
	string? Path { get; }
	string Name { get; set; }

	long Size { get; }

	public DateTime EditedOn { get; }

	T GetPath<T>(ReadOnlySpanFunc<char, T> action);
	T GetPath<T, TParameter>(ReadOnlySpanFunc<char, TParameter, T> action, TParameter parameter);
}