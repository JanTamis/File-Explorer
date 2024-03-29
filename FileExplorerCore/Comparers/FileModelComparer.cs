﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FileExplorerCore.Interfaces;

namespace FileExplorerCore.Models;

public struct FileModelComparer : IComparer<FileModel>, IAsyncComparer<FileModel>
{
	private readonly SortEnum sortMember;

	public FileModelComparer(SortEnum sortMember)
	{
		this.sortMember = sortMember;
	}

	public int Compare(FileModel x, FileModel y)
	{
		var result = sortMember switch
		{
			SortEnum.Name => String.Compare(x.Name, y.Name),
			SortEnum.Edited => y.EditedOn.CompareTo(x.EditedOn),
			SortEnum.Size => x.Size.CompareTo(y.Size),
			SortEnum.Extension => String.Compare(x.Extension, y.Extension),
			_ => 0,
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

	public ValueTask<int> CompareAsync(FileModel x, FileModel y)
	{
		return ValueTask.FromResult(Compare(x, y));
	}
}