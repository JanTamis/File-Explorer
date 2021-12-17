using FileExplorerCore.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FileExplorerCore.Models
{
	public class FileIndexModel
	{
		static readonly EnumerationOptions options = new()
		{
			IgnoreInaccessible = true,
			AttributesToSkip = FileAttributes.System,
		};

		static readonly EnumerationOptions sizeOptions = new()
		{
			IgnoreInaccessible = true,
			AttributesToSkip = FileAttributes.Temporary,
			RecurseSubdirectories = true,
		};

		Task<long> _taskSize;
		ObservableRangeCollection<FileIndexModel> _items;

		private readonly IEnumerable<FileIndexModel> query;
		private readonly IEnumerable<long> sizeQuery;

		public bool IsFolder { get; init; }

		public ObservableRangeCollection<FileIndexModel> Items
		{
			get
			{
				if (_items == null)
				{
					_items = new ObservableRangeCollection<FileIndexModel>();

					if (query != null)
					{
						var comparer = new AsyncComparer<FileIndexModel>(async (x, y) =>
						{
							var resultX = await x.TaskSize;
							var resultY = await y.TaskSize;

							return resultY.CompareTo(resultX);
						});

						ThreadPool.QueueUserWorkItem(async x => await _items.AddRange(query, comparer));
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
				if (sizeQuery != null && Size == 0)
				{
					return _taskSize ??= Task.Run(() => Size = sizeQuery.Sum());
				}

				return Task.FromResult(Size);
			}
		}

		public FileIndexModel(ReadOnlySpan<char> name, bool isFolder, long size, FileIndexModel parent = null)
		{
			Name = name.ToString();
			Size = size;
			Parent = parent;

			if (isFolder)
			{
				var pathBuilder = new ValueStringBuilder(name.Length);

				if (parent != null)
				{
					GetPath(this, ref pathBuilder);
				}
				else
				{
					pathBuilder.Append(name);
				}

				var path = pathBuilder.ToString();

				var folderQuery = new FileSystemEnumerable<FileIndexModel>(path, (ref FileSystemEntry x) => new FileIndexModel(x.FileName, x.IsDirectory, x.Length, this), options)
				{
					ShouldIncludePredicate = (ref FileSystemEntry x) => x.IsDirectory,
				};

				var fileQuery = new FileSystemEnumerable<FileIndexModel>(path, (ref FileSystemEntry x) => new FileIndexModel(x.FileName, x.IsDirectory, x.Length, this), options)
				{
					ShouldIncludePredicate = (ref FileSystemEntry x) => !x.IsDirectory,
				};

				query = folderQuery.Concat(fileQuery);

				if (isFolder)
				{
					sizeQuery = new FileSystemEnumerable<long>(path, (ref FileSystemEntry x) => x.Length, sizeOptions);
				}
			}

			IsFolder = isFolder;
		}

		public override string ToString()
		{
			return Name;
		}

		private void GetPath(FileIndexModel model, ref ValueStringBuilder builder)
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