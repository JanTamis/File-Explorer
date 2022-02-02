using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace FileExplorerCore.Converters
{
	[SupportedOSPlatform("Windows")]
	public unsafe class NativeMethods
	{
		private const int FILE_ATTRIBUTE_NORMAL = 0x80;
		private const int SHGFI_TYPENAME = 0x400;

		[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
		private static extern IntPtr SHGetFileInfo(
				char* pszPath,
				int dwFileAttributes,
				ref SHFILEINFO shinfo,
				uint cbfileInfo,
				int uFlags);


		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
		private struct SHFILEINFO
		{
			public SHFILEINFO(bool b)
			{
				hIcon = IntPtr.Zero;
				iIcon = 0;
				dwAttributes = 0;
				szDisplayName = "";
				szTypeName = "";
			}

			public IntPtr hIcon;
			public int iIcon;
			public uint dwAttributes;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
			public string szDisplayName;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
			public string szTypeName;
		};


		public static string GetShellFileType(ReadOnlySpan<char> fileName)
		{
			var shinfo = new SHFILEINFO(true);
			const int flags = SHGFI_TYPENAME;

			fixed (char* name = &fileName[0])
			{
				if (SHGetFileInfo(name, FILE_ATTRIBUTE_NORMAL, ref shinfo, (uint)Unsafe.SizeOf<SHFILEINFO>(), flags) == IntPtr.Zero)
				{
					return "File";
				}
			}

			return shinfo.szTypeName;
		}
	}
}
