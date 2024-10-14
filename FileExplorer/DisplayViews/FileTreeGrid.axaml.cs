using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindings;
using Avalonia.Media;
using Avalonia.Svg.Skia;
using Avalonia.Threading;
using FileExplorer.Converters;
using FileExplorer.Core.Helpers;
using FileExplorer.Core.Interfaces;
using FileExplorer.Helpers;
using FileExplorer.Interfaces;
using FileExplorer.Resources;
using Humanizer;

namespace FileExplorer.DisplayViews;

public sealed partial class FileTreeGrid : UserControl, IFileViewer
{
	private int anchorIndex = 0;

	private const int ImageSize = 30;

	private static readonly PropertyInfo? IsSelectedProperty = typeof(TreeDataGridRow).GetProperty(nameof(TreeDataGridRow.IsSelected));

	public IItemProvider? Provider { get; set; }

	private ObservableRangeCollection<IFileItem> _items;

	public ObservableRangeCollection<IFileItem>? Items
	{
		get
		{
			if (fileList is { Source.Items: ObservableRangeCollection<IFileItem> items, })
			{
				return items;
			}

			return default;
		}
		set
		{
			var folder = new SvgImage
			{
				Source = SvgSource.Load<SvgSource>("avares://FileExplorer/Assets/Icons/Folder.svg", null)
			};
			var file = new SvgImage
			{
				Source = SvgSource.Load<SvgSource>("avares://FileExplorer/Assets/Icons/File.svg", null)
			};

			var resultBinding = new CompiledBindingPathBuilder()
				.Property(new ClrPropertyInfo(nameof(ImageTaskCompletionNotifier.Result), null, null, typeof(IImage)),
					(reference, info) => new TaskCompletionNotifierAccessor<IImage?, IImage?>(reference, x => x.Result))
				.Build();

			var isSuccessfullyCompletedBinding = new CompiledBindingPathBuilder()
				.Property(new ClrPropertyInfo(nameof(ImageTaskCompletionNotifier.IsSuccessfullyCompleted), null, null, typeof(bool)),
					(reference, info) => new TaskCompletionNotifierAccessor<IImage?, bool>(reference, x => x.IsSuccessfullyCompleted))
				.Build();

			var isNotSuccessfullyCompletedBinding = new CompiledBindingPathBuilder()
				.Property(new ClrPropertyInfo(nameof(ImageTaskCompletionNotifier.IsSuccessfullyCompleted), null, null, typeof(bool)),
					(reference, info) => new TaskCompletionNotifierAccessor<IImage?, bool>(reference, x => !x.IsSuccessfullyCompleted))
				.Build();

			var source = new FlatTreeDataGridSource<IFileItem>(value)
			{
				Columns =
				{
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
									[!DataContextProperty] = new MultiBinding
									{
										Bindings =
										{
											new Binding("$parent[1].DataContext"),
											new Binding { Source = Provider, },
											new Binding { Source = ImageSize, }
										},
										Converter = PathToImageConverter.Instance
									},
									
									Children =
									{
										new Image
										{
											Width = ImageSize,
											Height = ImageSize,
											[!IsVisibleProperty] = new CompiledBindingExtension(isSuccessfullyCompletedBinding),
											[!Image.SourceProperty] = new CompiledBindingExtension(resultBinding)
										},
									
										new Image
										{
											Width = ImageSize,
											Height = ImageSize,
											[!IsVisibleProperty] = new MultiBinding
											{
												Bindings =
												{
													new CompiledBindingExtension(isNotSuccessfullyCompletedBinding),
													new Binding("$parent[1].DataContext.IsFolder")
												},
												Converter = BoolConverters.And
											},
											Source = folder
										},
										new Image
										{
											Width = ImageSize,
											Height = ImageSize,
											[!IsVisibleProperty] = new MultiBinding
											{
												Bindings =
												{
													new CompiledBindingExtension(isNotSuccessfullyCompletedBinding),
													new Binding("!$parent[1].DataContext.IsFolder")
												},
												Converter = BoolConverters.And
											},
											Source = file
										}
									}
								}
							}
						}), null, GridLength.Auto, new TemplateColumnOptions<IFileItem>
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
						}
					}),
					// x => Provider?.GetItems(x, "*", false, default).OrderByDescending(o => o.IsFolder).ThenBy(t => t.Name) ?? Enumerable.Empty<IFileItem>(),
					// x => x is { IsFolder: true } && Provider?.HasItems(x) is true),
					new TextColumn<IFileItem, string>(ResourceDefault.Name, item => item.Name, GridLength.Auto, new TextColumnOptions<IFileItem>()
					{
						CompareAscending = (x, y) => String.Compare(x!.Name, y!.Name),
						CompareDescending = (x, y) => String.Compare(y!.Name, x!.Name),
						CanUserSortColumn = false
					}),
					new TextColumn<IFileItem, string>(ResourceDefault.EditDate, item => item.EditedOn.Humanize(false, DateTime.Now, CultureInfo.CurrentCulture), GridLength.Auto, new TextColumnOptions<IFileItem>()
					{
						CompareAscending = (x, y) => DateTime.Compare(x!.EditedOn, y!.EditedOn),
						CompareDescending = (x, y) => DateTime.Compare(y!.EditedOn, x!.EditedOn),
						CanUserSortColumn = false
					}),
					new TextColumn<IFileItem, string>(ResourceDefault.Extension, item => item.Extension, GridLength.Auto, new TextColumnOptions<IFileItem>
					{
						CompareAscending = (x, y) => String.Compare(x?.Extension, y?.Extension),
						CompareDescending = (x, y) => String.Compare(y?.Extension, x?.Extension),
						CanUserSortColumn = false
					}),
					new TextColumn<IFileItem, string>(ResourceDefault.Size, item => item.IsFolder ? null : item.Size.Bytes().ToString(), GridLength.Auto, new TextColumnOptions<IFileItem>
					{
						CompareAscending = (x, y) => x!.Size.CompareTo(y!.Size),
						CompareDescending = (x, y) => y!.Size.CompareTo(x!.Size),
						CanUserSortColumn = false
					})
				}
			};

			fileList.Source = source;

			IFileItem? previousModel = null;

			TreeDataGridRow.IsSelectedProperty.Changed.Subscribe(args =>
			{
				if (args.Sender is TreeDataGridRow { DataContext: IFileItem item, })
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
				if (sender is { DataContext: IFileItem item, } && item != previousModel)
				{
					PathChanged(item);

					previousModel = item;
				}
			});

			if (fileList.RowSelection is not null)
			{
				fileList.RowSelection.SingleSelect = false;
				fileList.RowSelection.SelectionChanged += (_, args) => SelectionChanged(args.SelectedIndexes.Count);
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

		fileList.KeyDown += Grid_KeyDown;
	}
	public FileTreeGrid(IItemProvider? provider, ObservableRangeCollection<IFileItem>? items) : this()
	{
		Provider = provider;
		Items = items;
	}

	protected override void OnInitialized()
	{
		if (fileList.RowSelection is not null)
		{
			fileList.RowSelection.SelectionChanged += (sender, args) =>
			{
				SelectionChanged.Invoke(fileList.RowSelection.Count);
			};
			
			fileList.RowSelection.SingleSelect = false;

			fileList.RowPrepared += (sender, args) =>
			{
				if (args.Row is { DataContext: IFileItem item, })
				{
					IsSelectedProperty?.SetValue(args.Row, item.IsSelected);
				}

				args.Row[!TreeDataGridRow.IsSelectedProperty] = new Binding("DataContext.IsSelected", BindingMode.OneWayToSource);
			};
		}

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

	private void Item_DoubleTapped(object? sender, RoutedEventArgs e)
	{
		if (sender is ListBoxItem { DataContext: IFileItem model, })
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

public sealed class TaskCompletionNotifierAccessor<T, TResult> : IPropertyAccessor
{
	private readonly TaskCompletionNotifier<T> _notifier;
	private readonly Func<TaskCompletionNotifier<T>, TResult> _getter;
	
	public TaskCompletionNotifierAccessor(WeakReference<object> target, Func<TaskCompletionNotifier<T>, TResult> getter)
	{
		if (target.TryGetTarget(out var temp) && temp is TaskCompletionNotifier<T> completionNotifier)
		{
			_notifier = completionNotifier;
		}
		else
		{
			throw new ArgumentException("Target is not a TaskCompletionNotifier<T>");
		}
		
		_getter = getter;
	}
	
	public void Dispose()
	{
		
	}
	
	public bool SetValue(object? value, BindingPriority priority)
	{
		return false;
	}
	
	public void Subscribe(Action<object?> listener)
	{
		listener(_getter(_notifier));
		_notifier.Task.ContinueWith(x => listener(_getter(_notifier)), TaskContinuationOptions.ExecuteSynchronously);
	}
	
	public void Unsubscribe()
	{
		
	}

	public Type? PropertyType => typeof(TResult);

	public object? Value => _getter(_notifier);
}

public sealed class SelectorAccessor<T, TResult> : IPropertyAccessor
{
	private readonly T _item;
	private readonly Func<T, TResult> _getter;

	public SelectorAccessor(WeakReference<object> target, Func<T, TResult> getter)
	{
		if (target.TryGetTarget(out var temp) && temp is T completionNotifier)
		{
			_item = completionNotifier;
		}
		else
		{
			throw new ArgumentException("Target is not a TaskCompletionNotifier<T>");
		}

		_getter = getter;
	}

	public void Dispose()
	{

	}

	public bool SetValue(object? value, BindingPriority priority)
	{
		return false;
	}

	public void Subscribe(Action<object?> listener)
	{
		listener(_getter(_item));
	}

	public void Unsubscribe()
	{

	}

	public Type? PropertyType => typeof(TResult);

	public object? Value => _getter(_item);
}