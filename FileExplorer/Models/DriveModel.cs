using System.IO;

namespace FileExplorer.Models;

public class DriveModel(string path, string name, long totalSize, long size, DriveType driveType)
{
	public string Path { get; init; } = path;
	public string Name { get; set; } = name;

	public long TotalSize { get; set; } = totalSize;
	public long Size { get; set; } = size;

	public DriveType DriveType { get; set; } = driveType;
}