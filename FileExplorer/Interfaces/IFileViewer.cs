using System.Diagnostics;
using Avalonia.Controls;
using FileExplorer.Core.Helpers;
using FileExplorer.Core.Interfaces;

namespace FileExplorer.Interfaces;

public interface IFileViewer
{
	public ObservableRangeCollection<IFileItem>? Items { get; set; }

	event Action<IFileItem> PathChanged;
	public event Action<int> SelectionChanged;

	void SelectAll();
	void SelectNone();
	void SelectInvert();

	public static int UpdateSelection(IFileViewer viewer, int anchorIndex, int index, out int amount, bool select = true, bool rangeModifier = false, bool toggleModifier = false)
	{
		amount = 0;
		
		if (viewer.Items is null || index < 0 || index >= viewer.Items.Count)
		{
			return anchorIndex;
		}

		var files = viewer.Items;
		var multi = SelectionMode.Multiple.HasFlag(SelectionMode.Multiple);
		var toggle = toggleModifier || SelectionMode.Multiple.HasFlag(SelectionMode.Toggle);
		var range = multi && rangeModifier;

		if (!select)
		{
			files[index].IsSelected = false;
		}
		else if (range)
		{
			amount = SelectRange(files, anchorIndex, index);
		}
		else if (multi && toggle)
		{
			files[index].IsSelected ^= true;
			amount = files[index].IsSelected ? 1 : 0;
		}
		else
		{
			amount = SelectSingle(files, index);
		}

		return range ? anchorIndex : index;
	}

	private static int SelectRange(ObservableRangeCollection<IFileItem> files, int anchorIndex, int index)
	{
		ClearSelection(files);

		var count = 0;

		var start = Math.Min(anchorIndex, index);
		var end = Math.Max(anchorIndex, index);

		for (var i = start; i <= end; i++)
		{
			files[i].IsSelected = true;
			count++;
		}
		
		return count;
	}

	private static int SelectSingle(ObservableRangeCollection<IFileItem> files, int index)
	{
		ClearSelection(files);
		files[index].IsSelected = true;

		return 1;
	}

	private static void ClearSelection(ObservableRangeCollection<IFileItem> files)
	{
		for (var i = 0; i < files.Count; i++)
		{
			var file = files[i];
			
			if (file.IsSelected)
			{
				file.IsSelected = false;
			}
		}
	}
}