using System.Globalization;
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
	private const string ControlName = "TreeDataGrid";

	public IItemProvider? Provider { get; set; }

	public ObservableRangeCollection<IFileItem> Items
	{
		get
		{
			if (this.FindControl<TreeDataGrid>(ControlName) is { Source: HierarchicalTreeDataGridSource<IFileItem> source })
			{
				return source.Items as ObservableRangeCollection<IFileItem>;
			}

			return default;
		}
		set
		{
			var grid = this.FindControl<TreeDataGrid>(ControlName);

			var source = new HierarchicalTreeDataGridSource<IFileItem>(value)
			{
				Columns =
				{
					new TemplateColumn<IFileItem>("", new FuncDataTemplate<IFileItem>((x, _) =>
						new CheckBox()
						{
							[!CheckBox.IsCheckedProperty] = new Binding("IsSelected", BindingMode.TwoWay)
						}), GridLength.Auto, new ColumnOptions<IFileItem>
						{
							CompareAscending = (x, y) => String.Compare(x?.Name, y?.Name, StringComparison.CurrentCulture),
							CompareDescending = (x, y) => String.Compare(y?.Name, x?.Name, StringComparison.CurrentCulture),
						}),
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
										Width = 24,
										Height = 24,
										[!DataContextProperty] = new Binding
										{
											ConverterParameter = Provider,
											Converter = PathToImageConverter.Instance,
										},
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
						x => Provider?.GetItems(x, "*", false, default).OrderByDescending(o => o.IsFolder).ThenBy(t => t.Name) ?? Enumerable.Empty<IFileItem>(),
						x => x is { IsFolder: true } && Provider?.HasItems(x) is true),
					new TextColumn<IFileItem, string>("Edit Date", item => item.EditedOn.Humanize(false, DateTime.Now, CultureInfo.CurrentCulture), GridLength.Auto, new TextColumnOptions<IFileItem>()
					{
						CompareAscending = (x, y) => x.EditedOn.CompareTo(y.EditedOn),
						CompareDescending = (x, y) => y.EditedOn.CompareTo(x.EditedOn),
					}),
					new TextColumn<IFileItem, string>("Type", item => item.Extension, GridLength.Auto),
					new TextColumn<IFileItem, string>("Size", item => item.IsFolder ? null : item.Size.Bytes().ToString(), GridLength.Auto, new TextColumnOptions<IFileItem>
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

				if (args.NewValue.GetValueOrDefault() is IFileItem newItem)
				{
					newItem.IsVisible = true;
				}

				if (args.OldValue.GetValueOrDefault() is IFileItem oldItem)
				{
					oldItem.IsVisible = false;
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

	public void SelectAll()
	{
		if (this.FindControl<TreeDataGrid>(ControlName) is { RowSelection: { } selection })
		{
			using var delegator = new DelegateExecutor(selection.BeginBatchUpdate, selection.EndBatchUpdate);

			var index = 0;
			var enumerator = Items.GetEnumerator();

			while (enumerator.MoveNext())
			{
				selection.Select(index++);
			}
		}
	}

	public void SelectNone()
	{
		if (this.FindControl<TreeDataGrid>(ControlName) is { RowSelection: { } selection })
		{
			using var delegator = new DelegateExecutor(selection.BeginBatchUpdate, selection.EndBatchUpdate);

			var index = 0;
			using var enumerator = Items.GetEnumerator();

			while (enumerator.MoveNext())
			{
				selection.Deselect(index);

				index++;
			}
		}
	}

	public void SelectInvert()
	{
		if (this.FindControl<TreeDataGrid>(ControlName) is { RowSelection: { } selection })
		{
			using var delegator = new DelegateExecutor(selection.BeginBatchUpdate, selection.EndBatchUpdate);

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
		}
	}

	public FileTreeGrid()
	{
		AvaloniaXamlLoader.Load(this);
	}
}