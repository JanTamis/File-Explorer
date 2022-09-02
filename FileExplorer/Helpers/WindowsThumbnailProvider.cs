using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace FileExplorer.Helpers;

[Flags]
public enum ThumbnailOptions
{
	None = 0x00,        // Shrink the bitmap as necessary to fit, preserving its aspect ratio. Returns thumbnail if available, else icon.
	BiggerSizeOk = 0x01,    // Passed by callers if they want to stretch the returned image themselves. For example, if the caller passes an icon size of 80x80, a 96x96 thumbnail could be returned. This action can be used as a performance optimization if the caller expects that they will need to stretch the image. Note that the Shell implementation of IShellItemImageFactory performs a GDI stretch blit. If the caller wants a higher quality image stretch than provided through that mechanism, they should pass this flag and perform the stretch themselves.
	InMemoryOnly = 0x02,    // Return the item only if it is already in memory. Do not access the disk even if the item is cached. Note that this only returns an already-cached icon and can fall back to a per-class icon if an item has a per-instance icon that has not been cached. Retrieving a thumbnail, even if it is cached, always requires the disk to be accessed, so GetImage should not be called from the UI thread without passing SIIGBF_MEMORYONLY.
	IconOnly = 0x04,      // Return only the icon, never the thumbnail.
	ThumbnailOnly = 0x08,   // Return only the thumbnail, never the icon. Note that not all items have thumbnails, so SIIGBF_THUMBNAILONLY will cause the method to fail in these cases.
	InCacheOnly = 0x10,     // Allows access to the disk, but only to retrieve a cached item. This returns a cached thumbnail if it is available. If no cached thumbnail is available, it returns a cached per-instance icon but does not extract a thumbnail or icon.
	Win8CropToSquare = 0x20,  // Introduced in Windows 8. If necessary, crop the bitmap to a square.
	Win8WideThumbnails = 0x40,  // Introduced in Windows 8. Stretch and crop the bitmap to a 0.7 aspect ratio.
	Win8IconBackground = 0x80,  // Introduced in Windows 8. If returning an icon, paint a background using the associated app's registered background color.
	Win8ScaleUp = 0x100     // Introduced in Windows 8. If necessary, stretch the bitmap so that the height and width fit the given size.
}

[SupportedOSPlatform("Windows")]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public unsafe partial class WindowsThumbnailProvider
{
	private const string IShellItem2Guid = "7E9FB0D3-919F-4307-AB2E-9B1860310C93";

	private static readonly object lockObject = new();

	[DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	internal static extern int SHCreateItemFromParsingName(
		char* path,
		// The following parameter is not used - binding context.
		IntPtr pbc,
		ref Guid riid,
		[MarshalAs(UnmanagedType.Interface)] out IShellItem shellItem);

	[LibraryImport("gdi32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static partial bool DeleteObject(IntPtr hObject);

	private static readonly int bitmapSize = Unsafe.SizeOf<NativeMethods.BITMAP>();

	public static Bitmap? GetThumbnail(ReadOnlySpan<char> fileName, int width, int height)
	{
		return GetThumbnail(fileName, width, height, ThumbnailOptions.IconOnly, () => true);
	}

	public static Bitmap? GetThumbnail(ReadOnlySpan<char> fileName, int width, int height, ThumbnailOptions options, Func<bool> shouldRotate)
	{
		Bitmap? bitmap = null;

		if (!fileName.IsEmpty)
		{
			var hBitmap = GetHBitmap(fileName, width, height, options);

			if (hBitmap != IntPtr.Zero)
			{
				try
				{
					var bmp = new NativeMethods.BITMAP();
					NativeMethods.GetObjectBitmap(hBitmap, bitmapSize, ref bmp);

					if (shouldRotate())
					{
						RotateHorizontal(bmp);
					}

					bitmap = new Bitmap(PixelFormat.Bgra8888, AlphaFormat.Unpremul, bmp.bmBits, new Avalonia.PixelSize(bmp.bmWidth, bmp.bmHeight), new Avalonia.Vector(96, 96), bmp.bmWidthBytes);

				}
				finally
				{
					DeleteObject(hBitmap);
				}
			}
		}

		return bitmap;
	}

	private static void RotateHorizontal(NativeMethods.BITMAP bitmap)
	{
		var w = bitmap.bmWidth;
		var h = bitmap.bmHeight;

		Span<int> p = new(bitmap.bmBits.ToPointer(), bitmap.bmWidthBytes * bitmap.bmHeight / sizeof(int));
		Span<int> pDest = stackalloc int[p.Length];

		for (var i = 0; i < p.Length; i++)
		{
			var x = i % w;
			var y = i / w;
			var yOffset = h - (y + 1);

			if (yOffset < 0 && yOffset >= h)
				yOffset = 0;

			var val = yOffset * w + x;

			pDest[i] = p[val];
		}

		pDest.CopyTo(p);
	}

	private static IntPtr GetHBitmap(ReadOnlySpan<char> fileName, int width, int height, ThumbnailOptions options)
	{
		fixed (char* data = &fileName[0])
		{
			var shellItem2Guid = new Guid(IShellItem2Guid);
			var retCode = SHCreateItemFromParsingName(data, IntPtr.Zero, ref shellItem2Guid, out var nativeShellItem);

			if (retCode != 0)
				return IntPtr.Zero;

			NativeSize nativeSize = new()
			{
				Width = width,
				Height = height
			};

			//if (Dispatcher.UIThread.CheckAccess())
			//{
			var hr = ((IShellItemImageFactory)nativeShellItem).GetImage(nativeSize, options, out var hBitmap);

			Marshal.ReleaseComObject(nativeShellItem);

			return hr == HResult.Ok
				? hBitmap
				: IntPtr.Zero;
			//}

			//return Dispatcher.UIThread.InvokeAsync(() =>
			//{
			//	var hr = ((IShellItemImageFactory)nativeShellItem).GetImage(nativeSize, options, out var hBitmap);

			//	Marshal.ReleaseComObject(nativeShellItem);

			//	return hr == HResult.Ok
			//		? hBitmap
			//		: IntPtr.Zero;
			//}, DispatcherPriority.MinValue).Result;
		}
	}

	[ComImport]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
	internal interface IShellItem
	{
		void BindToHandler(IntPtr pbc,
			[MarshalAs(UnmanagedType.LPStruct)] Guid bhid,
			[MarshalAs(UnmanagedType.LPStruct)] Guid riid,
			out IntPtr ppv);

		void GetParent(out IShellItem ppsi);
		void GetDisplayName(SIGDN sigdnName, out IntPtr ppszName);
		void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
		void Compare(IShellItem psi, uint hint, out int piOrder);
	}

	internal enum SIGDN : uint
	{
		NORMALDISPLAY = 0,
		PARENTRELATIVEPARSING = 0x80018001,
		PARENTRELATIVEFORADDRESSBAR = 0x8001c001,
		DESKTOPABSOLUTEPARSING = 0x80028000,
		PARENTRELATIVEEDITING = 0x80031001,
		DESKTOPABSOLUTEEDITING = 0x8004c000,
		FILESYSPATH = 0x80058000,
		URL = 0x80068000
	}

	internal enum HResult
	{
		Ok = 0x0000,
		False = 0x0001,
		InvalidArguments = unchecked((int)0x80070057),
		OutOfMemory = unchecked((int)0x8007000E),
		NoInterface = unchecked((int)0x80004002),
		Fail = unchecked((int)0x80004005),
		ElementNotFound = unchecked((int)0x80070490),
		TypeElementNotFound = unchecked((int)0x8002802B),
		NoObject = unchecked((int)0x800401E5),
		Win32ErrorCanceled = 1223,
		Canceled = unchecked((int)0x800704C7),
		ResourceInUse = unchecked((int)0x800700AA),
		AccessDenied = unchecked((int)0x80030005)
	}

	[ComImport]
	[Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IShellItemImageFactory
	{
		[PreserveSig]
		HResult GetImage(
			[In, MarshalAs(UnmanagedType.Struct)] NativeSize size,
			[In] ThumbnailOptions flags,
			[Out] out IntPtr phbm);
	}

	[StructLayout(LayoutKind.Sequential)]
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
	internal struct NativeSize
	{
		private int width;
		private int height;

		public int Width
		{
			set => width = value;
		}

		public int Height
		{
			set => height = value;
		}
	}
}

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
static partial class NativeMethods
{
	[StructLayout(LayoutKind.Sequential)]
	public struct BITMAP
	{
		public int bmType;
		public int bmWidth;
		public int bmHeight;
		public int bmWidthBytes;
		public ushort bmPlanes;
		public ushort bmBitsPixel;
		public IntPtr bmBits;
	}

	[DllImport("gdi32", EntryPoint = "GetObject")]
	public static extern int GetObjectBitmap(IntPtr hObject, int nCount, ref BITMAP lpObject);
}