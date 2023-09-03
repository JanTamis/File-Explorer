using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using FileExplorer.Core.Helpers;
using FileExplorer.Core.Interfaces;
using FileExplorer.Models;
using FileExplorer.Resources;

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
					new HierarchicalExpanderColumn<FileIndexModel>(new TextColumn<FileIndexModel, string>(ResourceDefault.Name, x => x.Name, GridLength.Auto), x => Enumerable.Empty<FileIndexModel>(), x => false)
				}
			};
			RootDataGrid.Source = source;
		}
	}

	public ObservableRangeCollection<ExtensionModel> Extensions { get; private set; } = new();

	public bool HasShadow => true;
	public bool HasToBeCanceled => false;
	public string Title => "Analyze";
	public event Action? OnClose;

	public AnalyzerView()
	{
		InitializeComponent();

		DataContext = this;
		Root = new ObservableRangeCollection<FileIndexModel>();
	}

	public void Close()
	{
	}
}