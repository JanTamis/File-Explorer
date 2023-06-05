using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Svg.Skia;
using FileExplorer.Converters;
using FileExplorer.Core.Helpers;
using FileExplorer.Core.Interfaces;
using FileExplorer.Interfaces;
using FileExplorer.Resources;
using Humanizer;

namespace FileExplorer.DisplayViews;

public sealed partial class FileTreeGrid : UserControl, IFileViewer
{
	private int anchorIndex = 0;

	private const int ImageSize = 35;

	public IItemProvider? Provider { get; set; }

	private ObservableRangeCollection<IFileItem> _items;

	// public ObservableRangeCollection<IFileItem> Items
	// {
	// 	get => _items;
	// 	set
	// 	{
	// 		OnPropertyChanged(ref _items, value);
	//
	// 		fileList.ItemsSource = value;
	// 	}
	// }

	public ObservableRangeCollection<IFileItem> Items
	{
		get
		{
			if (fileList is { Source: HierarchicalTreeDataGridSource<IFileItem> source })
			{
				return source.Items as ObservableRangeCollection<IFileItem>;
			}

			return default;
		}
		set
		{
			var grid = fileList;

			var folder = new SvgImage
			{
				Source = SvgSource.Load<SvgSource>($"avares://FileExplorer/Assets/Icons/Folder.svg", null),
			};
			var file = new SvgImage
			{
				Source = SvgSource.Load<SvgSource>($"avares://FileExplorer/Assets/Icons/File.svg", null),
			};

			var source = new FlatTreeDataGridSource<IFileItem>(value)
			{
				Columns =
				{
					// new TemplateColumn<IFileItem>("", new FuncDataTemplate<IFileItem>((x, _) =>
					// 	new CheckBox()
					// 	{
					// 		[!CheckBox.IsCheckedProperty] = new Binding("IsSelected", BindingMode.TwoWay)
					// 	}), GridLength.Auto, new ColumnOptions<IFileItem>
					// 	{
					// 		CompareAscending = (x, y) => String.Compare(x?.Name, y?.Name, StringComparison.CurrentCulture),
					// 		CompareDescending = (x, y) => String.Compare(y?.Name, x?.Name, StringComparison.CurrentCulture),
					// 	}),
					// new HierarchicalExpanderColumn<IFileItem>(
					new TemplateColumn<IFileItem>(null, new FuncDataTemplate<IFileItem>((x, _) =>
						new StackPanel
						{
							Orientation = Orientation.Horizontal,
							Margin = new Thickness(0, 5),
							Children =
							{
								new Panel
								{
									Margin = new Thickness(5, 0, 0, 0),
									[!DataContextProperty] = new Binding
									{
										ConverterParameter = Provider,
										Converter = PathToImageConverter.Instance,
									},

									Children =
									{
										new Image
										{
											Width = ImageSize,
											Height = ImageSize,
											[!IsVisibleProperty] = new Binding("IsSuccessfullyCompleted"),
											[!Image.SourceProperty] = new Binding("Result"),
										},

										new Image
										{
											Width = ImageSize,
											Height = ImageSize,
											[!IsVisibleProperty] = new MultiBinding
											{
												Bindings =
												{
													new Binding("!IsSuccessfullyCompleted"),
													new Binding("$parent[1].DataContext.IsFolder"),
												},
												Converter = BoolConverters.And,
											},
											Source = folder,
										},
										new Image
										{
											Width = ImageSize,
											Height = ImageSize,
											[!IsVisibleProperty] = new MultiBinding
											{
												Bindings =
												{
													new Binding("!IsSuccessfullyCompleted"),
													new Binding("!$parent[1].DataContext.IsFolder"),
												},
												Converter = BoolConverters.And,
											},
											Source = file,
										},
									},
								},
							},
						}), GridLength.Auto, new TemplateColumnOptions<IFileItem>
					{
						CanUserResizeColumn = false,
						CompareAscending = (x, y) =>
						{
							var result = x.IsFolder.CompareTo(y.IsFolder);

							if (result is 0)
							{
								result = String.CompareOrdinal(x!.Name, y!.Name);
							}
							
							return result;
						},
						CompareDescending = (x, y) =>
						{
							var result = y.IsFolder.CompareTo(x.IsFolder);

							if (result is 0)
							{
								result = String.CompareOrdinal(y!.Name, x!.Name);
							}
							
							return result;
						},
					}),
					// x => Provider?.GetItems(x, "*", false, default).OrderByDescending(o => o.IsFolder).ThenBy(t => t.Name) ?? Enumerable.Empty<IFileItem>(),
					// x => x is { IsFolder: true } && Provider?.HasItems(x) is true),
					new TextColumn<IFileItem, string>(ResourceDefault.Name, item => item.Name, GridLength.Auto, new TextColumnOptions<IFileItem>()
					{
						SingleTapEdit = true,
						CompareAscending = (x, y) => String.Compare(x!.Name, y!.Name),
						CompareDescending = (x, y) => String.Compare(y!.Name, x!.Name),
					}),
					new TextColumn<IFileItem, string>(ResourceDefault.EditDate, item => item.EditedOn.Humanize(false, DateTime.Now, CultureInfo.CurrentCulture), GridLength.Auto, new TextColumnOptions<IFileItem>()
					{
						CompareAscending = (x, y) => DateTime.Compare(x!.EditedOn, y!.EditedOn),
						CompareDescending = (x, y) => DateTime.Compare(y!.EditedOn, x!.EditedOn),
					}),
					new TextColumn<IFileItem, string>(ResourceDefault.Extension, item => item.Extension, GridLength.Auto, new TextColumnOptions<IFileItem>
					{
						CompareAscending = (x, y) => String.Compare(x?.Extension, y?.Extension),
						CompareDescending = (x, y) => String.Compare(y?.Extension, x?.Extension),
					}),
					new TextColumn<IFileItem, string>(ResourceDefault.Size, item => item.IsFolder ? null : item.Size.Bytes().ToString(), GridLength.Auto, new TextColumnOptions<IFileItem>
					{
						CompareAscending = (x, y) => x!.Size.CompareTo(y!.Size),
						CompareDescending = (x, y) => y!.Size.CompareTo(x!.Size),
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
				// if (args.Sender is TreeDataGridRow row)
				// {
				// 	row.IsSelected = args.NewValue.Value is IFileItem { IsSelected: true };
				// }

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
				grid.RowSelection.SelectionChanged += (_, args) => SelectionChanged(args.SelectedIndexes.Count);
			}
		}
	}

	new event PropertyChangedEventHandler? PropertyChanged = delegate { };

	public event Action<IFileItem> PathChanged = delegate { };
	public event Action<int> SelectionChanged = delegate { };


	public void SelectAll()
	{
		foreach (var item in Items)
		{
			item.IsSelected = true;
		}
	}

	public void SelectNone()
	{
		foreach (var item in Items)
		{
			item.IsSelected = false;
		}
	}

	public void SelectInvert()
	{
		foreach (var item in Items)
		{
			item.IsSelected ^= true;
		}
	}

	public FileTreeGrid()
	{
		InitializeComponent();

		// fileList.ElementPrepared += Grid_ElementPrepared;
		// fileList.ElementClearing += Grid_ElementClearing;
		//
		// fileList.KeyDown += Grid_KeyDown;1

		
		
	}

	protected override void OnInitialized()
	{
		fileList.RowSelection.SelectionChanged += delegate { SelectionChanged.Invoke(fileList.RowSelection.Count); };
		fileList.RowSelection.SingleSelect = false;
		base.OnInitialized();
	}

	private void Grid_KeyDown(object? sender, KeyEventArgs e)
	{
		if (e.Key is Key.A && e.KeyModifiers is KeyModifiers.Control)
		{
			foreach (var file in Items.Where(x => !x.IsSelected))
			{
				file.IsSelected = true;
			}

			SelectionChanged?.Invoke(Items.Count);
		}
	}

	private void Grid_ElementClearing(object? sender, ItemsRepeaterElementClearingEventArgs e)
	{
		if (e.Element is ListBoxItem item)
		{
			item.DoubleTapped -= Item_DoubleTapped;
			item.PointerPressed -= Item_PointerPressed;
		}
	}

	private async void Item_PointerPressed(object? sender, PointerPressedEventArgs e)
	{
		if (sender is ListBoxItem { DataContext: IFileItem model } item)
		{
			var point = e.GetCurrentPoint(item);

			if (point.Properties.IsLeftButtonPressed || point.Properties.IsRightButtonPressed)
			{
				var index = Items.IndexOf(model);

				anchorIndex = IFileViewer.UpdateSelection(
					this,
					anchorIndex,
					index,
					true,
					e.KeyModifiers.HasFlag(KeyModifiers.Shift),
					e.KeyModifiers.HasFlag(KeyModifiers.Control));
			}
		}
	}

	private void Grid_ElementPrepared(object? sender, ItemsRepeaterElementPreparedEventArgs e)
	{
		if (e.Element is ListBoxItem { DataContext: IFileItem model } item)
		{
			item.DoubleTapped += Item_DoubleTapped;
			item.PointerPressed += Item_PointerPressed;

			model.IsVisible = true;
		}
	}

	private void Item_DoubleTapped(object? sender, RoutedEventArgs e)
	{
		if (sender is ListBoxItem { DataContext: IFileItem model })
		{
			PathChanged(model);
		}
	}

	public void OnPropertyChanged<T>(ref T field, T value, [CallerMemberName] string? name = null)
	{
		field = value;
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}