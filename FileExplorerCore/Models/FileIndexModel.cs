using System;
using System.Collections.Generic;
using System.IO;
using FileExplorerCore.Helpers;
using System.IO.Enumeration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FileExplorerCore.Models
{
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
			.EnumerateChildren(1)
			.Cast<FileSystemTreeItem>()
			.Select(s => new FileIndexModel(s));

		private IEnumerable<long> sizeQuery => _treeItem
			.EnumerateChildren(1)
			.Cast<FileSystemTreeItem>()
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
						var comparer = new AsyncComparer<FileIndexModel>(async (x, y) =>
						{
							var resultX = await x.TaskSize;
							var resultY = await y.TaskSize;

							return resultY.CompareTo(resultX);
						});

						ThreadPool.QueueUserWorkItem(async x => await _items.AddRangeAsync(query, comparer));
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
				Parent = new FileIndexModel(item.Parent as FileSystemTreeItem);
			}
		}

		public override string ToString()
		{
			return Name;
		}

		private static void GetPath(FileIndexModel model, ref ValueStringBuilder builder)
		{
			builder.Insert(0, Path.DirectorySeparatorChar, 1);
			builder.Insert(0, model.Name);

			if (model.Parent != null)
			{
				GetPath(model.Parent, ref builder);
			}
		}
	}
}