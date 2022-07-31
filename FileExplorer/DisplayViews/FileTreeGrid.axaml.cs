using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using FileExplorer.Core.Interfaces;
using FileExplorer.Helpers;
using FileExplorer.Models;

namespace FileExplorer.DisplayViews;

public class FileTreeGrid : UserControl
{
	public ObservableRangeCollection<IFileItem> Files
	{
		set
		{
			var grid = this.FindControl<TreeDataGrid>("TreeDataGrid");

			grid.Source = new HierarchicalTreeDataGridSource<IFileItem>(value)
			{
				Columns =
				{
					new HierarchicalExpanderColumn<IFileItem>(
						new TemplateColumn<IFileItem>("Name", new FuncDataTemplate<IFileItem>((x, scope) =>
							new StackPanel
							{
								Orientation = Orientation.Horizontal,
								Children =
								{
									new Image
									{
										Width = 24,
										Height = 24,
										[!Image.SourceProperty] = new Binding("Image^"),
									},
									new TextBlock
									{
										TextTrimming = TextTrimming.CharacterEllipsis,
										[!TextBlock.TextProperty] = new Binding("Name"),
									},
								},
							})),
						x => x.Children,
						x => x.IsFolder && x.Children.Any()),
				},
			};
		}
	}

	public event Action<FileSystemTreeItem> PathChanged = delegate { };
	public event Action<int> SelectionChanged = delegate { };
	
	public FileTreeGrid()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}