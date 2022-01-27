using System;

namespace FileExplorerCore.Helpers
{
	public delegate void ReadOnlySpanAction<T>(ReadOnlySpan<T> span);

	public delegate TResult ReadOnlySpanFunc<T, out TResult>(ReadOnlySpan<T> span);
}