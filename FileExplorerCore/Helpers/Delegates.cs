using System;

namespace FileExplorerCore.Helpers;

public delegate void ReadOnlySpanAction<T>(ReadOnlySpan<T> span);

public delegate TResult ReadOnlySpanFunc<T, out TResult>(ReadOnlySpan<T> span);
public delegate TResult ReadOnlySpanFunc<T, in TParameter, out TResult>(ReadOnlySpan<T> span, TParameter parameter);