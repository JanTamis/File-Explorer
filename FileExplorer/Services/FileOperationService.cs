using FileExplorer.Core.Helpers;
using FileExplorer.Core.Interfaces;

namespace FileExplorer.Services;

public class FileOperationService()
{
	private readonly Stack<IFileItem?> _undoStack = new();
	private readonly Stack<IFileItem?> _redoStack = new();

	public IFileItem? Undo(IFileItem? currentFolder)
	{
		// if (_undoStack.TryPop(out var path))
		// {
		// 	_redoStack.Push(currentFolder);
		// }
		//
		// return path;

		return currentFolder;
	}

	public IFileItem? Redo(IFileItem? currentFolder)
	{
		// if (_redoStack.TryPop(out var path))
		// {
		// 	_undoStack.Push(currentFolder);
		// }
		//
		// return path;

		return currentFolder;
	}
	
	public void UpdateUndoRedoStack(IFileItem? currentFolder)
	{
		if (!_undoStack.TryPeek(out var tempPath) || tempPath != currentFolder)
		{
			_undoStack.Push(currentFolder);
			_redoStack.Clear();
		}
	}
}