using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using FileExplorer.Converters;
using FileExplorer.Core.Helpers;
using FileExplorer.Core.Interfaces;
using FileExplorer.Interfaces;
using Humanizer;

namespace FileExplorer.DisplayViews;

public class FileTreeGrid : UserControl, IFileViewer
{
	const string controlName = "TreeDataGrid";

	public IItemProvider? Provider { get; set; }

	public ObservableRangeCollection<IFileItem> Items
	{
		get
		{
			if (this.FindControl< TreeDataGrid>(controlName) is { Source: HierarchicalTreeDataGridSource<IFileItem> source })
			{
				return source.Items as ObservableRangeCollection<IFileItem>;
			}

			return default;
		}
		set
		{
			var grid = this.FindControl<TreeDataGrid>(controlName);

			var source = new HierarchicalTreeDataGridSource<IFileItem>(value)
			{
				Columns =
				{
					new HierarchicalExpanderColumn<IFileItem>(
						new TemplateColumn<IFileItem>("Name", new FuncDataTemplate<IFileItem>((x, _) =>
							new StackPanel
							{
								Orientation = Orientation.Horizontal,
								Margin = new Thickness(0, 5),
								Children =
								{
									new Image
									{
										[!DataContextProperty] = new Binding("")
										{
											Source = Provider?.GetThumbnailAsync(x, 24, default) ?? Task<IImage?>.FromResult(null as IImage),
											Converter = PathToImageConverter.Instance,
											ConverterParameter = 24,
										},
										Width = 24,
										Height = 24,
										[!Image.SourceProperty] = new Binding("Result"),
									},
									new TextBlock
									{
										Margin = new Thickness(2.5, 0, 0, 0),
										TextTrimming = TextTrimming.CharacterEllipsis,
										[!TextBlock.TextProperty] = new Binding("Name"),
									},
								},
							}), GridLength.Auto, new ColumnOptions<IFileItem>
						{
							CompareAscending = (x, y) => String.Compare(x?.Name, y?.Name, StringComparison.CurrentCulture),
							CompareDescending = (x, y) => String.Compare(y?.Name, x?.Name, StringComparison.CurrentCulture),
						}),
						x => Provider.GetItems(x, "*", false, default).OrderByDescending(o => o.IsFolder).ThenBy(t => t.Name),
					x => x is { IsFolder: true } && Provider?.HasItems(x) is true),
					new TextColumn<IFileItem, DateTime>("Edit Date", item => item.EditedOn, GridLength.Auto),
					new TextColumn<IFileItem, string>("Type", item => item.Extension, GridLength.Auto),
					new TextColumn<IFileItem, string>("Size", item => item.IsFolder ? null : item.Size.Bytes().ToString(), GridLength.Auto, new TextColumnOptions<IFileItem>()
					{
						CompareAscending = (x, y) => x.Size.CompareTo(y.Size),
						CompareDescending = (x, y) => y.Size.CompareTo(x.Size),
					}),
				},
			};

			grid.Source = source;

			IFileItem? previousModel = null;

			TreeDataGridRow.IsSelectedProperty.Changed.Subscribe(args =>
			{
				if (args.Sender is TreeDataGridRow { DataContext: IFileItem item })
				{
					item.IsSelected = args.NewValue.Value;
				}
			});

			DataContextProperty.Changed.Subscribe(args =>
			{
				if (args.Sender is TreeDataGridRow row)
				{
					row.IsSelected = args.NewValue.Value is IFileItem { IsSelected: true };
				}
			});

			DoubleTappedEvent.AddClassHandler<TreeDataGridRow>((sender, _) =>
			{
				if (sender is { DataContext: IFileItem item } && item != previousModel)
				{
					PathChanged(item);

					previousModel = item;
				}
			});

			if (grid.RowSelection is not null)
			{
				grid.RowSelection.SingleSelect = false;
				grid.RowSelection.SelectionChanged += delegate { SelectionChanged(); };
			}
		}
	}

	public ValueTask<int> ItemCount => ValueTask.FromResult(Items.Count);

	public event Action<IFileItem> PathChanged = delegate { };
	public event Action SelectionChanged = delegate { };

	public Action SelectAll => () =>
	{
		if (this.FindControl<TreeDataGrid>(controlName) is { RowSelection: { } selection })
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
		if (this.FindControl<TreeDataGrid>(controlName) is { RowSelection: { } selection })
		{
			selection.BeginBatchUpdate();

			var index = 0;
			using var enumerator = Items.GetEnumerator();

			while (enumerator.MoveNext())
			{
				selection.Deselect(index);

				index++;
			}

			selection.EndBatchUpdate();
		}
	};

	public Action SelectInvert => () =>
	{
		if (this.FindControl<TreeDataGrid>(controlName) is { RowSelection: { } selection })
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
		AvaloniaXamlLoader.Load(this);
	}
}