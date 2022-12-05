using System.IO;
using System.IO.Enumeration;
using FileExplorer.Core.Helpers;

namespace FileExplorer.Models;

public sealed class FileIndexModel
{
	private readonly FileSystemTreeItem _treeItem;

	private static readonly EnumerationOptions sizeOptions = new()
	{
		IgnoreInaccessible = true,
		AttributesToSkip = FileAttributes.Temporary,
		RecurseSubdirectories = true,
	};

	private Task<long> _taskSize;
	private ObservableRangeCollection<FileIndexModel>? _items;

	private IEnumerable<FileIndexModel>? query => _treeItem
		.EnumerateChildrenRecursive()
		.Select(s => new FileIndexModel(s));

	private IEnumerable<long> sizeQuery
	{
		get
		{
			var path = _treeItem.GetPath(x => x.ToString());

			var options = FileSystemTreeItem.Options;
			options.RecurseSubdirectories = true;

			return new FileSystemEnumerable<long>(path, (ref FileSystemEntry x) => x.Length, options);
		}
	}

	public bool IsFolder => _treeItem.IsFolder;

	public ObservableRangeCollection<FileIndexModel> Items
	{
		get
		{
			if (_items is null)
			{
				_items = new ObservableRangeCollection<FileIndexModel>();

				if (query is not null)
				{
					var comparer = Comparer<FileIndexModel>.Create((x, y) =>
					{
						var resultX = x.Size;
						var resultY = y.Size;

						return resultY.CompareTo(resultX);
					});

					ThreadPool.QueueUserWorkItem(async _ => await _items.AddRange(query, comparer));
				}
			}

			return _items;
		}
	}

	public FileIndexModel? Parent { get; init; }

	public Task<long> ParentSize => Parent?.TaskSize ?? Task.FromResult(0L);

	public string Name { get; init; }

	public long Size { get; set; }

	public Task<long> TaskSize
	{
		get
		{
			if (_taskSize is null or { IsCompleted: false } && Size is 0)
			{
				return _taskSize ??= Task.Run(() => Size = sizeQuery.Sum());
			}

			return Task.FromResult(Size);
		}
	}

	public FileIndexModel(FileSystemTreeItem item)
	{
		_treeItem = item;

		if (item.Parent is not null)
		{
			Parent = new FileIndexModel(item.Parent);
		}

		Name = _treeItem.Value;
	}

	public override string ToString()
	{
		return Name;
	}
}