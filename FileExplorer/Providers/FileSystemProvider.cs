using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading.Tasks;
using FileExplorer.Core.Interfaces;
using FileExplorer.Models;
using FileExplorer.Helpers;

namespace FileExplorer.Providers;

public class FileSystemProvider : IItemProvider
{
	public IEnumerable<IFileItem> GetItems(string path, string filter, bool recursive)
	{
		var treeItem = PathHelper.FromPath(path);

		if (treeItem is null)
		{
			return Enumerable.Empty<IFileItem>();
		}

		if (recursive)
		{
			return GetFileSystemEntriesRecursive(treeItem, filter);
		}

		return GetFileSystemEntries(treeItem, filter);
	}

	public async Task<int> GetItemCountAsync(string path, string filter, bool recursive)
	{
		return await Task.Run(() =>
		{
			var options = new EnumerationOptions
			{
				IgnoreInaccessible = true,
				AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
				RecurseSubdirectories = recursive,
			};

			var query = Directory.Exists(path)
				? new FileSystemEnumerable<bool>(path, (ref FileSystemEntry _) => false, options)
					{
						ShouldIncludePredicate = (ref FileSystemEntry entry) => FileSystemName.MatchesSimpleExpression(filter, entry.FileName),
					}
				: Enumerable.Empty<bool>();

			return query.Count();
		});
	}

	public IEnumerable<IPathSegment> GetPath(string path)
	{
		var treeItem = PathHelper.FromPath(path);

		return treeItem?
			.EnumerateToRoot()
			.Reverse()
			.Select(s => new FolderModel(s)) ?? Enumerable.Empty<FolderModel>();
	}

	private static IEnumerable<FileModel> GetFileSystemEntriesRecursive(FileSystemTreeItem path, string search)
	{
		if (search is "*" or "*.*")
		{
			return path
				.EnumerateChildren((ref FileSystemEntry file) => !file.IsHidden)
				.Select(s => new FileModel(s));
		}

		return path
			.EnumerateChildren((ref FileSystemEntry entry) => !entry.IsHidden)
			.Where(w => FileSystemName.MatchesSimpleExpression(search, w.Value))
			.Select(s => new FileModel(s));
	}

	private static IEnumerable<FileModel> GetFileSystemEntries(FileSystemTreeItem path, string search)
	{
		return path
			.EnumerateChildren((ref FileSystemEntry file) => !file.IsHidden, 0)
			.Where(w => FileSystemName.MatchesSimpleExpression(search, w.Value))
			.Select(s => new FileModel(s));

	}
}