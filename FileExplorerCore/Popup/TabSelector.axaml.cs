using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FileExplorerCore.Helpers;
using FileExplorerCore.Interfaces;
using FileExplorerCore.ViewModels;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FileExplorerCore.Popup;

public partial class TabSelector : UserControl, IPopup, INotifyPropertyChanged
{
	public new event PropertyChangedEventHandler PropertyChanged = delegate { };

	public event Action<TabItemViewModel> TabSelectionChanged = delegate { };

	private ObservableRangeCollection<TabItemViewModel> _tabs;
	private TabItemViewModel _currentTab;

	public TabItemViewModel CurrentTab
	{
		get => _currentTab;
		set
		{
			_currentTab = value;

			TabSelectionChanged(CurrentTab);
		}
	}

	public bool HasShadow => false;
	public bool HasToBeCanceled => false;

	public string Title => "Select Tab";

	public ObservableRangeCollection<TabItemViewModel> Tabs
	{
		get => _tabs;
		set => OnPropertyChanged(ref _tabs, value);
	}

	public event Action OnClose = delegate { };

	public TabSelector()
	{
		DataContext = this;
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}

	public void Close()
	{
		OnClose();
	}

	protected void OnPropertyChanged<T>(ref T property, T value, [CallerMemberName] string name = null)
	{
		property = value;
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}