namespace FileExplorerCore.ViewModels
{
	public interface IPopupViewModel
	{
		bool HasShadow { get; }

		void Close();
	}
}