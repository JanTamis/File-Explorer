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
	
	public static int UpdateSelection(IFileViewer viewer, int anchorIndex, int index, bool select = true, bool rangeModifier = false, bool toggleModifier = false)
	{
		if (viewer.Items is null)
		{
			return anchorIndex;
		}
		
		var files = viewer.Items;
		var count = files.Count;

		if (index < 0 || index >= count)
		{
			return anchorIndex;
		}

		var mode = SelectionMode.Multiple;
		var multi = mode.HasFlag(SelectionMode.Multiple);
		var toggle = toggleModifier || mode.HasFlag(SelectionMode.Toggle);
		var range = multi && rangeModifier;

		if (!select)
		{
			files[index].IsSelected = false;
		}
		else if (range)
		{
			for (var i = 0; i < count; i++)
			{
				files[i].IsSelected = false;
			}

			if (index > anchorIndex)
			{
				for (var i = anchorIndex; i <= index; i++)
				{
					files[i].IsSelected = true;
				}
			}
			else
			{
				for (var i = index; i <= anchorIndex; i++)
				{
					files[i].IsSelected = true;
				}
			}
		}
		else if (multi && toggle)
		{
			var file = files[index];

			file.IsSelected ^= true;
		}
		else
		{
			for (var i = 0; i < count; i++)
			{
				files[i].IsSelected = false;
			}

			files[index].IsSelected = true;
		}

		if (!range)
		{
			anchorIndex = index;
		}

		return anchorIndex;
	}
}