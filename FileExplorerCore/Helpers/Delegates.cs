using System;

namespace FileExplorerCore.Helpers
{
	public delegate void ReadOnlySpanAction<T>(ReadOnlySpan<T> span);
}