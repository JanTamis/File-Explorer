using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FileExplorer.Core.Helpers;
using FileExplorer.Core.Interfaces;
using FileExplorer.Interfaces;
using FileExplorer.Models;

namespace FileExplorer.DisplayViews;

public partial class AnalyzerView : UserControl, IFileViewer
{
	public ObservableRangeCollection<IFileItem> Items { get; set; }
	public ValueTask<int> ItemCount => ValueTask.FromResult(0);
	public event Action<IFileItem>? PathChanged;
	public event Action? SelectionChanged;

	public Action SelectAll { get; }
	public Action SelectNone { get; }
	public Action SelectInvert { get; }

	public ObservableRangeCollection<FileIndexModel> Root { get; private set; } = new();
	public ObservableRangeCollection<ExtensionModel> Extensions { get; private set; } = new();

	public AnalyzerView()
	{
		DataContext = this;
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}