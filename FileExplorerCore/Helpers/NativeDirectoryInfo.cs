using FileExplorerCore.Models;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace FileExplorerCore.Helpers
{
	public static class DirectoryAlternative
	{
		[Serializable, StructLayout(LayoutKind.Sequential)]
		private struct WIN32_FIND_DATA
		{
			public int dwFileAttributes;
			public int ftCreationTime_dwLowDateTime;
			public int ftCreationTime_dwHighDateTime;
			public int ftLastAccessTime_dwLowDateTime;
			public int ftLastAccessTime_dwHighDateTime;
			public int ftLastWriteTime_dwLowDateTime;
			public int ftLastWriteTime_dwHighDateTime;
			public int nFileSizeHigh;
			public int nFileSizeLow;
			public int dwReserved0;
			public int dwReserved1;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
			public string cFileName;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
			public string cAlternateFileName;
		}

		[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
		private static extern IntPtr FindFirstFile(string pFileName, ref WIN32_FIND_DATA pFindFileData);
		[DllImport("kernel32.dll")]
		private static extern bool FindNextFile(IntPtr hFindFile, ref WIN32_FIND_DATA lpFindFileData);
		[DllImport("kernel32.dll")]
		private static extern bool FindClose(IntPtr hFindFile);

		private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

		public static long GetFileSize(string path)
		{
			var findFileData = new WIN32_FIND_DATA();
			IntPtr hFindFile = FindFirstFile(path, ref findFileData);

			var attributes = (FileAttributes)findFileData.dwFileAttributes;

			if (hFindFile == INVALID_HANDLE_VALUE || attributes.HasFlag(FileAttributes.Directory))
				return -1L;

			FindClose(hFindFile);

			return ((long)findFileData.nFileSizeHigh << 32) | (findFileData.nFileSizeLow & 0xFFFFFFFFL);
		}

		public static DateTime GetFileWriteDate(FileModel model)
		{
			//var findFileData = new WIN32_FIND_DATA();
			//IntPtr hFindFile = FindFirstFile(path, ref findFileData);

			//var attributes = (FileAttributes)findFileData.dwFileAttributes;

			//if (hFindFile == INVALID_HANDLE_VALUE)
			//	return DateTime.MinValue;

			//FindClose(hFindFile);

			//return ConvertDateTime(findFileData.ftLastWriteTime_dwHighDateTime, findFileData.ftLastWriteTime_dwLowDateTime);

			//static long CombineHighLowInts(int high, int low)
			//{
			//	return (((long)high) << 0x20) | low;
			//}

			//static DateTime ConvertDateTime(int high, int low)
			//{
			//	long fileTime = CombineHighLowInts(high, low);
			//	return DateTime.FromFileTimeUtc(fileTime);
			//}

			if (model.IsFolder)
			{
				return new DirectoryInfo(model.Path).LastWriteTime;
			}

			return new FileInfo(model.Path).LastWriteTime;
		}
	}
}
