using System.IO;
using FileExplorer.Core.Interfaces;
using FileExplorer.Core.Models;

namespace FileExplorer.Providers;

public class FileSystemUpdater : IFolderUpdateNotificator
{
	public event Action<ChangeType, string, string?>? Changed;

	private readonly FileSystemWatcher _watcher;

	public FileSystemUpdater(string? folder, string filter, bool recursive)
	{
		_watcher = new FileSystemWatcher(folder, filter)
		{
			IncludeSubdirectories = recursive,
		};

		_watcher.NotifyFilter = NotifyFilters.Attributes
		                        | NotifyFilters.CreationTime
		                        | NotifyFilters.DirectoryName
		                        | NotifyFilters.FileName
		                        | NotifyFilters.LastAccess
		                        | NotifyFilters.LastWrite
		                        | NotifyFilters.Security
		                        | NotifyFilters.Size;

		_watcher.Changed += OnChanged;
		_watcher.Created += OnCreated;
		_watcher.Deleted += OnDeleted;
		_watcher.Renamed += OnRenamed;

		_watcher.EnableRaisingEvents = true;
	}

	public void Dispose()
	{
		_watcher.Dispose();
	}

	private void OnChanged(object sender, FileSystemEventArgs e)
	{
		Changed?.Invoke(ChangeType.Changed, e.FullPath, null);
	}

	private void OnCreated(object sender, FileSystemEventArgs e)
	{
		Changed?.Invoke(ChangeType.Created, e.FullPath, null);
	}

	private void OnDeleted(object sender, FileSystemEventArgs e)
	{
		Changed?.Invoke(ChangeType.Deleted, e.FullPath, null);
	}

	private void OnRenamed(object sender, RenamedEventArgs e)
	{
		Changed?.Invoke(ChangeType.Renamed, e.OldFullPath, e.FullPath);
	}
}