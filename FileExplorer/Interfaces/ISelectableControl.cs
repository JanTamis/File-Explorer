using System;

namespace FileExplorer.Interfaces;

public interface ISelectableControl
{
	event Action<int> SelectionChanged;
}