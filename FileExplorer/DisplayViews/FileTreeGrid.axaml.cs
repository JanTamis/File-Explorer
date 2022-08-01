using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Selection;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Reactive;
using FileExplorerCore.Interfaces;
using FileExplorerCore.Models;
using Humanizer;
using ReactiveUI;

namespace FileExplorerCore.DisplayViews;

public class FileTreeGrid : UserControl, IFileViewer
{
	public IEnumerable<IItem> Items
	{
		get
		{
			if (this.FindControl<TreeDataGrid>("TreeDataGrid") is { Source: HierarchicalTreeDataGridSource<IItem> source })
			{
				return source.Items;
			}

			return Enumerable.Empty<FileModel>();
		}
		set
		{
			var grid = this.FindControl<TreeDataGrid>("TreeDataGrid");

			var source = new HierarchicalTreeDataGridSource<IItem>(value)
			{
				Columns =
				{
					new HierarchicalExpanderColumn<IItem>(
						new TemplateColumn<IItem>("Name", new FuncDataTemplate<FileModel>((x, scope) =>
							new StackPanel
							{
								Orientation = Orientation.Horizontal,
								Margin = new Thickness(0, 5),
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
										Margin = new Thickness(2.5, 0, 0, 0),
										TextTrimming = TextTrimming.CharacterEllipsis,
										[!TextBlock.TextProperty] = new Binding("Name"),
									},
								},
							}), GridLength.Auto, new ColumnOptions<IItem>
						{
							CompareAscending = (x, y) => String.Compare(x?.Name, y?.Name, StringComparison.CurrentCulture),
							CompareDescending = (x, y) => String.Compare(y?.Name, x?.Name, StringComparison.CurrentCulture),
						}),
						x => x.Children,
						x => x.IsFolder && x.Children.Any()),
					new TextColumn<IItem, DateTime>("Edit Date", item => item.EditedOn, GridLength.Auto),
					new TextColumn<IItem, string>("Type", item => item.Extension, GridLength.Auto),
					new TextColumn<IItem, string>("Size", item => item.IsFolder ? null : item.Size.Bytes().ToString(), GridLength.Auto, new TextColumnOptions<IItem>()
					{
						CompareAscending = (x, y) => x.Size.CompareTo(y.Size),
						CompareDescending = (x, y) => y.Size.CompareTo(x.Size),
					}),
				},
			};

			grid.Source = source;

			IItem? previousModel = null;

			TreeDataGridRow.IsSelectedProperty.Changed.Subscribe(args =>
			{
				if (args.Sender is TreeDataGridRow { DataContext: IItem item })
				{
					item.IsSelected = args.NewValue.Value;
				}
			});

			TreeDataGridRow.DataContextProperty.Changed.Subscribe(args =>
			{
				if (args.Sender is TreeDataGridRow row)
				{
					row.IsSelected = args.NewValue.Value is IItem { IsSelected: true };
				}
			});

			DoubleTappedEvent.AddClassHandler<TreeDataGridRow>((sender, args) =>
			{
				if (sender is { DataContext: IItem item } && item != previousModel)
				{
					PathChanged(item.GetPath(path => path.ToString()));

					previousModel = item;
				}
			});

			grid.RowSelection!.SingleSelect = false;
			grid.RowSelection.SelectionChanged += delegate { SelectionChanged(); };
		}
	}

	public Task<int> ItemCount => Task.FromResult(Items.Count());

	public event Action<string> PathChanged = delegate { };
	public event Action SelectionChanged = delegate { };


	public Action SelectAll => () =>
	{
		if (this.FindControl<TreeDataGrid>("TreeDataGrid") is { RowSelection: { } selection })
		{
			selection.BeginBatchUpdate();

			var index = 0;

			foreach (var _ in Items)
			{
				selection.Select(index);

				index++;
			}

			selection.EndBatchUpdate();
		}
	};

	public Action SelectNone => () =>
	{
		if (this.FindControl<TreeDataGrid>("TreeDataGrid") is { RowSelection: { } selection })
		{
			selection.BeginBatchUpdate();

			var index = 0;

			foreach (var _ in Items)
			{
				selection.Deselect(index);

				index++;
			}

			selection.EndBatchUpdate();
		}
	};

	public Action SelectInvert => () =>
	{
		if (this.FindControl<TreeDataGrid>("TreeDataGrid") is { RowSelection: { } selection })
		{
			selection.BeginBatchUpdate();

			var index = 0;

			foreach (var item in Items)
			{
				if (item.IsSelected)
				{
					selection.Deselect(index);
				}
				else
				{
					selection.Select(index);
				}

				index++;
			}

			selection.EndBatchUpdate();
		}
	};

	public FileTreeGrid()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}