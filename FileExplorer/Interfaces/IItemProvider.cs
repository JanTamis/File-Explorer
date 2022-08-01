using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using FileExplorerCore.Models;

namespace FileExplorerCore.Interfaces;

public interface IItemProvider
{
	IEnumerable<IItem> GetItems(string path, string filter, bool recursive);
	Task<int> GetItemCountAsync(string path, string filter, bool recursive);

	IEnumerable<FolderModel> GetPath(string? path);
}