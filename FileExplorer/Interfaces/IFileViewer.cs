using Avalonia;
using Avalonia.Controls;
using FileExplorer.Core.Helpers;
using FileExplorer.Core.Interfaces;

namespace FileExplorer.Interfaces;

public interface IFileViewer
{
	public ObservableRangeCollection<IFileItem> Items { get; set; }

	public ValueTask<int> ItemCount { get; }

	event Action<IFileItem> PathChanged;
	public event Action SelectionChanged;

	void SelectAll();
	void SelectNone();
	void SelectInvert();

	/// <summary>
	/// Updates the selection for an item based on user interaction.
	/// </summary>
	/// <param name="index">The index of the item.</param>
	/// <param name="select">Whether the item should be selected or unselected.</param>
	/// <param name="rangeModifier">Whether the range modifier is enabled (i.e. shift key).</param>
	/// <param name="toggleModifier">Whether the toggle modifier is enabled (i.e. ctrl key).</param>
	public static async ValueTask<int> UpdateSelection(IFileViewer viewer, int anchorIndex, int index, bool select = true, bool rangeModifier = false, bool toggleModifier = false)
	{
		var files = viewer.Items;
		var count = files.Count;

		if (index < 0 || index >= count)
		{
			return anchorIndex;
		}

		var mode = SelectionMode.Multiple;
		var multi = mode.HasAllFlags(SelectionMode.Multiple);
		var toggle = toggleModifier || mode.HasAllFlags(SelectionMode.Toggle);
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