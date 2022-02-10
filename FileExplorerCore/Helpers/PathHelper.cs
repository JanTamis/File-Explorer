using System;

namespace FileExplorerCore.Helpers;

public static class PathHelper
{
	public static char DirectorySeparator { get; }

	static PathHelper()
	{
		DirectorySeparator = OperatingSystem.IsWindows()
			? '\\'
			: '/';
	}
}