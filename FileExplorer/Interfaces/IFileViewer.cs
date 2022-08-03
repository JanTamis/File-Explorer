using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using FileExplorer.Core.Interfaces;

namespace FileExplorer.Interfaces;

public interface IFileViewer
{
	public IEnumerable<IFileItem> Items { get; set; }

	public Task<int> ItemCount { get; }

	event Action<string> PathChanged;
	public event Action SelectionChanged;

	Action SelectAll { get; }
	Action SelectNone { get; }
	Action SelectInvert { get; }

	/// <summary>
	/// Updates the selection for an item based on user interaction.
	/// </summary>
	/// <param name="index">The index of the item.</param>
	/// <param name="select">Whether the item should be selected or unselected.</param>
	/// <param name="rangeModifier">Whether the range modifier is enabled (i.e. shift key).</param>
	/// <param name="toggleModifier">Whether the toggle modifier is enabled (i.e. ctrl key).</param>
	public static async Task<int> UpdateSelection(IFileViewer viewer, int anchorIndex, int index, bool select = true, bool rangeModifier = false, bool toggleModifier = false)
	{
		var count = await viewer.ItemCount;
		var files = viewer.Items;

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
			files.ElementAt(index).IsSelected = false;
		}
		else if (range)
		{
			for (var i = 0; i < count; i++)
			{
				files.ElementAt(i).IsSelected = false;
			}

			if (index > anchorIndex)
			{
				for (var i = anchorIndex; i <= index; i++)
				{
					files.ElementAt(i).IsSelected = true;
				}
			}
			else
			{
				for (var i = index; i <= anchorIndex; i++)
				{
					files.ElementAt(i).IsSelected = true;
				}
			}
		}
		else if (multi && toggle)
		{
			var file = files.ElementAt(index);

			file.IsSelected ^= true;
		}
		else
		{
			for (var i = 0; i < count; i++)
			{
				files.ElementAt(i).IsSelected = false;
			}

			files.ElementAt(index).IsSelected = true;
		}

		if (!range)
		{
			anchorIndex = index;
		}

		return anchorIndex;
	}
}