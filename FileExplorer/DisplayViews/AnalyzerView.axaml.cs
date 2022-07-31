using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FileExplorer.Helpers;
using FileExplorer.Models;

namespace FileExplorer.DisplayViews;

public partial class AnalyzerView : UserControl
{
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