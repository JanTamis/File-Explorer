using FileExplorerCore.Interfaces;
using System;
using System.ComponentModel;

namespace FileExplorerCore.ViewModels
{
	public class SettingsViewModel : IPopup
	{
		public bool HasShadow => true;

		public string Title => "Settings";

		public event Action OnClose = delegate { };

		public void Close()
		{
			OnClose();
		}
	}
}