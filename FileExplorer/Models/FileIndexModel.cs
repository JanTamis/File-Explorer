using System.IO;
using FileExplorer.Core.Helpers;

namespace FileExplorer.Models;

public class FileIndexModel
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
		.EnumerateChildren()
		.Select(s => new FileIndexModel(s));

	private IEnumerable<long> sizeQuery => _treeItem
		.EnumerateChildren()
		.Select(s => s.GetPath((path, isFolder) => !isFolder ? new FileInfo(path.ToString()).Length : 0, IsFolder));

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

	public ValueTask<long> ParentSize => Parent?.TaskSize ?? ValueTask.FromResult(0L);

	public string Name { get; init; }

	public long Size { get; set; }

	public ValueTask<long> TaskSize
	{
		get
		{
			if (_taskSize is null or { IsCompleted: false } && Size is 0)
			{
				return new ValueTask<long>(_taskSize ??= Task.Run(() => Size = sizeQuery.Sum()));
			}

			return ValueTask.FromResult(Size);
		}
	}

	public FileIndexModel(FileSystemTreeItem item)
	{
		_treeItem = item;

		if (item.Parent is not null)
		{
			Parent = new FileIndexModel(item.Parent);
		}
	}

	public override string ToString()
	{
		return Name;
	}
}