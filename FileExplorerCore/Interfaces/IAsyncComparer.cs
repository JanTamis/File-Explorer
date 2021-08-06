using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileExplorerCore.Interfaces
{
	public interface IAsyncComparer<T>
	{
		ValueTask<int> Compare(T? x, T? y);
	}
}
