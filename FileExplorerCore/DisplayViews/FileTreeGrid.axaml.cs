using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using FileExplorerCore.Helpers;
using FileExplorerCore.Models;

namespace FileExplorerCore.DisplayViews;

public class FileTreeGrid : UserControl
{
	public ObservableRangeCollection<FileModel> Files
	{
		set
		{
			var grid = this.FindControl<TreeDataGrid>("TreeDataGrid");

			grid.Source = new HierarchicalTreeDataGridSource<FileModel>(value)
			{
				Columns =
				{
					new HierarchicalExpanderColumn<FileModel>(
						new TemplateColumn<FileModel>("Name", new FuncDataTemplate<FileModel>((x, scope) =>
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