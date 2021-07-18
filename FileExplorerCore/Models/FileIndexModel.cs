using FileExplorerCore.Helpers;
using NetFabric.Hyperlinq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
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
			AttributesToSkip = FileAttributes.System,
			RecurseSubdirectories = true,
		};

		Task<long> _taskSize;

		private readonly IEnumerable<FileIndexModel> query;
		private readonly IEnumerable<long> sizeQuery;

		public IEnumerable<FileIndexModel> Items => query;

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
					return _taskSize ??= Task.Run(() =>
					{
						var size = sizeQuery.Sum();

						Size = size;

						return Size;
					});
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

				sizeQuery = new FileSystemEnumerable<long>(path, (ref FileSystemEntry x) => x.Length, sizeOptions);
			}
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