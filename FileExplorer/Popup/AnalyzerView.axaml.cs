using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Markup.Xaml;
using FileExplorer.Core.Helpers;
using FileExplorer.Core.Interfaces;
using FileExplorer.Models;

namespace FileExplorer.DisplayViews;

public sealed partial class AnalyzerView : UserControl, IPopup
{
	private ObservableRangeCollection<FileIndexModel> _root;

	public ObservableRangeCollection<FileIndexModel> Root
	{
		get => _root;
		private set
		{
			_root = value;

			var source = new HierarchicalTreeDataGridSource<FileIndexModel>(value)
			{
				Columns =
				{
					new HierarchicalExpanderColumn<FileIndexModel>(new TextColumn<FileIndexModel,string>("Name", x => x.Name, GridLength.Auto), x => Enumerable.Empty<FileIndexModel>(), x => false),
				},
			};

			var grid = this.FindControl<TreeDataGrid>("RootDataGrid");
			grid.Source = source;
		}
	}

	public ObservableRangeCollection<ExtensionModel> Extensions { get; private set; } = new();

	public bool HasShadow => true;
	public bool HasToBeCanceled => false;
	public string Title => "Analyze";
	public event Action? OnClose;

	public AnalyzerView()
	{
		DataContext = this;
		InitializeComponent();

		Root = new ObservableRangeCollection<FileIndexModel>();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}

	public void Close()
	{

	}
}