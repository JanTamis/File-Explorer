using System;
using System.Collections.Generic;

namespace FileExplorerCore.Models
{
	public class FileModelComparer : Comparer<FileModel>
	{
		SortEnum sortMember;

		public FileModelComparer(SortEnum sortMember)
		{
			this.sortMember = sortMember;
		}

		public override int Compare(FileModel x, FileModel y)
		{
			var result = sortMember switch
			{
				SortEnum.Name => String.Compare(x.Name, y.Name),
				SortEnum.Edited => x.EditedOn.CompareTo(y.EditedOn),
				SortEnum.Size => x.Size.CompareTo(y.Size),
				SortEnum.Extension => String.Compare(x.Extension, y.Extension),
				_ => 0
			};

			if (sortMember is SortEnum.None)
			{
				var xIsFolder = x.IsFolder;
				var yIsFolder = y.IsFolder;

				if (xIsFolder == yIsFolder)
				{
					result = String.Compare(x.Name, y.Name);
				}
				else if (xIsFolder)
				{
					result = -1;
				}
				else if (yIsFolder)
				{
					result = 1;
				}
			}

			return result;
		}
	}
}
