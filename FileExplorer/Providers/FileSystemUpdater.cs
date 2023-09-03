using System.IO;
using FileExplorer.Core.Interfaces;
using FileExplorer.Core.Models;

namespace FileExplorer.Providers;

public class FileSystemUpdater : IFolderUpdateNotificator
{
	public event Action<IFolderUpdateNotificator, ChangeType, string, string?>? Changed;

	private readonly FileSystemWatcher _watcher;

	public FileSystemUpdater(string? folder, string filter, bool recursive)
	{
		_watcher = new FileSystemWatcher(folder, filter)
		{
			IncludeSubdirectories = recursive
		};
		
		_watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.DirectoryName;

		_watcher.Changed += OnChanged;
		_watcher.Created += OnCreated;
		_watcher.Deleted += OnDeleted;
		_watcher.Renamed += OnRenamed;

		_watcher.EnableRaisingEvents = true;
	}

	public bool Equals(IFolderUpdateNotificator? other)
	{
		return other is FileSystemUpdater updater && 
					 _watcher.Path == updater._watcher.Path &&
					 _watcher.Filter == updater._watcher.Filter &&
					 _watcher.IncludeSubdirectories == updater._watcher.IncludeSubdirectories;
	}

	public void Dispose()
	{
		_watcher.EnableRaisingEvents = false;
		_watcher.Dispose();
	}

	private void OnChanged(object sender, FileSystemEventArgs e)
	{
		Changed?.Invoke(this, ChangeType.Changed, e.FullPath, null);
	}

	private void OnCreated(object sender, FileSystemEventArgs e)
	{
		Changed?.Invoke(this, ChangeType.Created, e.FullPath, null);
	}

	private void OnDeleted(object sender, FileSystemEventArgs e)
	{
		Changed?.Invoke(this, ChangeType.Deleted, e.FullPath, null);
	}

	private void OnRenamed(object sender, RenamedEventArgs e)
	{
		Changed?.Invoke(this, ChangeType.Renamed, e.OldFullPath, e.FullPath);
	}
}