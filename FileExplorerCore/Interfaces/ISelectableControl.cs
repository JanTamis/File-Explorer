using System;

namespace FileExplorerCore.Interfaces;

public interface ISelectableControl
{
	event Action SelectionChanged;
}