using FileExplorer.Core.Interfaces;

namespace FileExplorer.Models;

public readonly struct FileModelComparer(SortEnum sortMember) : IComparer<IFileItem>
{
	public int Compare(IFileItem? x, IFileItem? y)
	{
		if (x is null && y is null)
		{
			return 0;
		}

		if (x is null)
		{
			return -1;
		}

		if (y is null)
		{
			return 1;
		}
		
		var result = sortMember switch
		{
			SortEnum.Name => String.Compare(x.Name, y.Name, StringComparison.CurrentCulture),
			SortEnum.Edited => y.EditedOn.CompareTo(x.EditedOn),
			SortEnum.Size => x.Size.CompareTo(y.Size),
			SortEnum.Extension => String.Compare(x.Extension, y.Extension, StringComparison.CurrentCulture),
			_ => 0
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