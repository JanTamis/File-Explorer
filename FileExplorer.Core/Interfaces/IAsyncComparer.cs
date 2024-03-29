﻿namespace FileExplorer.Core.Interfaces;

public interface IAsyncComparer<in T>
{
	ValueTask<int> CompareAsync(T x, T y);
}

public delegate ValueTask<int> AsyncComparison<in T>(T x, T y);