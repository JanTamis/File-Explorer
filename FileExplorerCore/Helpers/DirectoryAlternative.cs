using System.Runtime.InteropServices;

namespace FileExplorerCore.Helpers
{
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

		public static long GetFileSize(byte[] path, bool isAscii)
		{
			fixed (byte* ptr = path)
			{
				if (isAscii)
				{
					var findFileData = new WIN32_FIND_DATA();
					IntPtr hFindFile = AnsiFileInfo.FindFirstFile(ptr, ref findFileData);

					var attributes = (FileAttributes)findFileData.dwFileAttributes;

					if (hFindFile == INVALID_HANDLE_VALUE || attributes.HasFlag(FileAttributes.Directory))
						return -1L;

					FindClose(hFindFile);

					return ((long)findFileData.nFileSizeHigh << 32) | (findFileData.nFileSizeLow & 0xFFFFFFFFL);
				}
				else
				{
					var findFileData = new WIN32_FIND_DATA();
					IntPtr hFindFile = UnicodeFileInfo.FindFirstFile(ptr, ref findFileData);

					var attributes = (FileAttributes)findFileData.dwFileAttributes;

					if (hFindFile == INVALID_HANDLE_VALUE || attributes.HasFlag(FileAttributes.Directory))
						return -1L;

					FindClose(hFindFile);

					return ((long)findFileData.nFileSizeHigh << 32) | (findFileData.nFileSizeLow & 0xFFFFFFFFL);
				}
			}
		}

		public static unsafe DateTime GetFileWriteDate(byte[] path, bool isAscii)
		{
			fixed (byte* ptr = path)
			{
				if (isAscii)
				{
					var findFileData = new WIN32_FIND_DATA();
					var hFindFile = AnsiFileInfo.FindFirstFile(ptr, ref findFileData);

					FindClose(hFindFile);

					return ConvertDateTime(findFileData.ftLastWriteTime_dwHighDateTime, findFileData.ftLastWriteTime_dwLowDateTime);
				}
				else
				{
					var findFileData = new WIN32_FIND_DATA();
					var hFindFile = UnicodeFileInfo.FindFirstFile(ptr, ref findFileData);

					FindClose(hFindFile);

					return ConvertDateTime(findFileData.ftLastWriteTime_dwHighDateTime, findFileData.ftLastWriteTime_dwLowDateTime);
				}
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

		public static string GetName(byte[] path, bool isAscii)
		{
			fixed (byte* ptr = path)
			{
				if (isAscii)
				{
					var findFileData = new WIN32_FIND_DATA();
					IntPtr hFindFile = AnsiFileInfo.FindFirstFile(ptr, ref findFileData);

					var attributes = (FileAttributes)findFileData.dwFileAttributes;

					if (hFindFile == INVALID_HANDLE_VALUE || attributes.HasFlag(FileAttributes.Directory))
						return String.Empty;

					FindClose(hFindFile);

					return findFileData.cFileName ?? findFileData.cAlternateFileName;
				}
				else
				{
					var findFileData = new WIN32_FIND_DATA();
					IntPtr hFindFile = UnicodeFileInfo.FindFirstFile(ptr, ref findFileData);

					var attributes = (FileAttributes)findFileData.dwFileAttributes;

					if (hFindFile == INVALID_HANDLE_VALUE || attributes.HasFlag(FileAttributes.Directory))
						return String.Empty;

					FindClose(hFindFile);

					return findFileData.cFileName ?? findFileData.cAlternateFileName;
				}
			}
		}
	}

	public static unsafe class UnicodeFileInfo
	{
		[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
		public static extern IntPtr FindFirstFile(byte* pFileName, ref DirectoryAlternative.WIN32_FIND_DATA pFindFileData);
	}

	public static unsafe class AnsiFileInfo
	{
		[DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
		public static extern IntPtr FindFirstFile(byte* pFileName, ref DirectoryAlternative.WIN32_FIND_DATA pFindFileData);
	}
}