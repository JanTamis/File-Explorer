using System;
using System.Collections.Generic;
using System.Globalization;
using FileExplorer.Core.Interfaces;

namespace FileExplorer.Models;

public struct FileModelComparer : IComparer<IFileItem>
{
	private readonly SortEnum sortMember;

	public FileModelComparer(SortEnum sortMember)
	{
		this.sortMember = sortMember;
	}

	public int Compare(IFileItem x, IFileItem y)
	{
		var result = sortMember switch
		{
			SortEnum.Name => String.Compare(x.Name, y.Name, StringComparison.CurrentCulture),
			SortEnum.Edited => y.EditedOn.CompareTo(x.EditedOn),
			SortEnum.Size => x.Size.CompareTo(y.Size),
			SortEnum.Extension => String.Compare(x.Extension, y.Extension, StringComparison.CurrentCulture),
			_ => 0,
		};

		if (sortMember is SortEnum.None)
		{
			result = x.IsFolder.CompareTo(y.IsFolder);

			if (result == 0)
			{
				result = String.Compare(x.Name, y.Name, StringComparison.CurrentCulture);
			}
		}

		return result;
	}
}