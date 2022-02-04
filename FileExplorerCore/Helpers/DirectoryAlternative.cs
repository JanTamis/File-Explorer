using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace FileExplorerCore.Helpers
{
	[SupportedOSPlatform("Windows")]
	public static unsafe class DirectoryAlternative
	{
		[Serializable, StructLayout(LayoutKind.Sequential)]
		public struct WIN32_FIND_DATA
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

		[DllImport("kernel32.dll")]
		private static extern bool FindNextFile(IntPtr hFindFile, ref WIN32_FIND_DATA lpFindFileData);

		[DllImport("kernel32.dll")]
		private static extern bool FindClose(IntPtr hFindFile);

		private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

		public static long GetFileSize(ReadOnlySpan<char> path)
		{
			fixed (char* ptr = path)
			{
				var findFileData = new WIN32_FIND_DATA();
				var hFindFile = UnicodeFileInfo.FindFirstFile(ptr, ref findFileData);

				var attributes = (FileAttributes)findFileData.dwFileAttributes;

				if (hFindFile == INVALID_HANDLE_VALUE || attributes.HasFlag(FileAttributes.Directory))
					return -1L;

				FindClose(hFindFile);

				return ((long)findFileData.nFileSizeHigh << 32) | (findFileData.nFileSizeLow & 0xFFFFFFFFL);
			}
		}

		public static unsafe DateTime GetFileWriteDate(ReadOnlySpan<char> path)
		{
			fixed (char* ptr = path)
			{
				var findFileData = new WIN32_FIND_DATA();
				var hFindFile = UnicodeFileInfo.FindFirstFile(ptr, ref findFileData);

				FindClose(hFindFile);

				return ConvertDateTime(findFileData.ftLastWriteTime_dwHighDateTime,
					findFileData.ftLastWriteTime_dwLowDateTime);
			}

			static long CombineHighLowInts(uint high, uint low)
			{
				return (((long)high) << 32) | low;
			}

			static DateTime ConvertDateTime(int high, int low)
			{
				long fileTime = CombineHighLowInts((uint)high, (uint)low);
				return DateTime.FromFileTimeUtc(fileTime).ToLocalTime();
			}
		}

		public static string GetName(ReadOnlySpan<char> path)
		{
			fixed (char* ptr = path)
			{
				var findFileData = new WIN32_FIND_DATA();
				IntPtr hFindFile = UnicodeFileInfo.FindFirstFile(ptr, ref findFileData);

				var attributes = (FileAttributes)findFileData.dwFileAttributes;

				if (hFindFile == INVALID_HANDLE_VALUE || attributes.HasFlag(FileAttributes.Directory))
					return Path.GetFileNameWithoutExtension(path).ToString();

				FindClose(hFindFile);

				return findFileData.cFileName;
			}
		}
	}

	public static unsafe class UnicodeFileInfo
	{
		[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
		public static extern IntPtr FindFirstFile(char* pFileName, ref DirectoryAlternative.WIN32_FIND_DATA pFindFileData);
	}
}