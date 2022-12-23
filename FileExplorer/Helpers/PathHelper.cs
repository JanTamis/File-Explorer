using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using FileExplorer.Models;

namespace FileExplorer.Helpers;

public static class PathHelper
{
	public static char DirectorySeparator => OperatingSystem.IsWindows()
		? '\\'
		: '/';

	public static ReadOnlySpan<byte> DirectorySeparatorUTF8 => OperatingSystem.IsWindows()
		? @"\"u8
		: "/"u8;
}