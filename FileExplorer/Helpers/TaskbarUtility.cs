﻿// using Avalonia.Controls.ApplicationLifetimes;
// using Avalonia.Threading;
// using System.Runtime.InteropServices;
// using System.Runtime.Versioning;
// using Avalonia;
//
// namespace FileExplorer.Helpers;
//
// [SupportedOSPlatform("Windows")]
// public static class TaskbarUtility
// {
// 	private static readonly ITaskbarList4 _taskbarList;
//
// 	static TaskbarUtility()
// 	{
// 		if (!IsSupported())
// 			throw new Exception("Taskbar functions not available");
//
// 		_taskbarList = (ITaskbarList4)new CTaskbarList();
// 		_taskbarList.HrInit();
// 	}
//
// 	private static bool IsSupported()
// 	{
// 		return Environment.OSVersion.Platform == PlatformID.Win32NT &&
// 		       Environment.OSVersion.Version.CompareTo(new Version(6, 1)) >= 0;
// 	}
//
// 	public static void SetProgressState(TaskbarProgressBarStatus state)
// 	{
// 		Dispatcher.UIThread.InvokeAsync(new Action(() =>
// 		{
// 			if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
// 			{
// 				_taskbarList.SetProgressState(desktop.MainWindow.PlatformImpl.Handle.Handle, state);
// 			}
// 		}), DispatcherPriority.Background);
// 	}
//
// 	public static void SetProgressValue(int currentValue, int maximumValue)
// 	{
// 		// using System.Windows.Interop
// 		Dispatcher.UIThread.InvokeAsync(new Action(() =>
// 		{
// 			if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
// 			{
// 				if (currentValue >= (int)UInt64.MinValue)
// 				{
// 					_taskbarList.SetProgressValue(desktop.MainWindow.PlatformImpl.Handle.Handle, Convert.ToUInt64(currentValue), Convert.ToUInt64(maximumValue));
//
// 				}
// 			}
// 		}), DispatcherPriority.Background);
// 	}
// }
//
// internal enum HResult
// {
// 	Ok = 0x0000
//
// 	// Add more constants here, if necessary
// }
//
// public enum TaskbarProgressBarStatus
// {
// 	NoProgress = 0,
// 	Indeterminate = 0x1,
// 	Normal = 0x2,
// 	Error = 0x4,
// 	Paused = 0x8
// }
//
// internal enum ThumbButtonMask
// {
// 	Bitmap = 0x1,
// 	Icon = 0x2,
// 	Tooltip = 0x4,
// 	THB_FLAGS = 0x8
// }
//
// [Flags]
// internal enum ThumbButtonOptions
// {
// 	Enabled = 0x00000000,
// 	Disabled = 0x00000001,
// 	DismissOnClick = 0x00000002,
// 	NoBackground = 0x00000004,
// 	Hidden = 0x00000008,
// 	NonInteractive = 0x00000010
// }
//
// [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
// internal struct ThumbButton
// {
// 	///
// 	/// WPARAM value for a THUMBBUTTON being clicked.
// 	///
// 	internal const int Clicked = 0x1800;
//
// 	[MarshalAs(UnmanagedType.U4)]
// 	internal ThumbButtonMask Mask;
// 	internal uint Id;
// 	internal uint Bitmap;
// 	internal IntPtr Icon;
// 	[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
// 	internal string Tip;
// 	[MarshalAs(UnmanagedType.U4)]
// 	internal ThumbButtonOptions Flags;
// }
//
// internal enum SetTabPropertiesOption
// {
// 	None = 0x0,
// 	UseAppThumbnailAlways = 0x1,
// 	UseAppThumbnailWhenActive = 0x2,
// 	UseAppPeekAlways = 0x4,
// 	UseAppPeekWhenActive = 0x8
// }
//
// // using System.Runtime.InteropServices
// [ComImport()]
// [Guid("c43dc798-95d1-4bea-9030-bb99e2983a1a")]
// [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
// internal interface ITaskbarList4
// {
// 	// ITaskbarList
// 	[PreserveSig]
// 	void HrInit();
// 	[PreserveSig]
// 	void AddTab(IntPtr hwnd);
// 	[PreserveSig]
// 	void DeleteTab(IntPtr hwnd);
// 	[PreserveSig]
// 	void ActivateTab(IntPtr hwnd);
// 	[PreserveSig]
// 	void SetActiveAlt(IntPtr hwnd);
//
// 	// ITaskbarList2
// 	[PreserveSig]
// 	void MarkFullscreenWindow(
// 		IntPtr hwnd,
// 		[MarshalAs(UnmanagedType.Bool)] bool fFullscreen);
//
// 	// ITaskbarList3
// 	[PreserveSig]
// 	void SetProgressValue(IntPtr hwnd, UInt64 ullCompleted, UInt64 ullTotal);
// 	[PreserveSig]
// 	void SetProgressState(IntPtr hwnd, TaskbarProgressBarStatus tbpFlags);
// 	[PreserveSig]
// 	void RegisterTab(IntPtr hwndTab, IntPtr hwndMDI);
// 	[PreserveSig]
// 	void UnregisterTab(IntPtr hwndTab);
// 	[PreserveSig]
// 	void SetTabOrder(IntPtr hwndTab, IntPtr hwndInsertBefore);
// 	[PreserveSig]
// 	void SetTabActive(IntPtr hwndTab, IntPtr hwndInsertBefore, uint dwReserved);
// 	[PreserveSig]
// 	HResult ThumbBarAddButtons(
// 		IntPtr hwnd,
// 		uint cButtons,
// 		[MarshalAs(UnmanagedType.LPArray)] ThumbButton[] pButtons);
// 	[PreserveSig]
// 	HResult ThumbBarUpdateButtons(
// 		IntPtr hwnd,
// 		uint cButtons,
// 		[MarshalAs(UnmanagedType.LPArray)] ThumbButton[] pButtons);
// 	[PreserveSig]
// 	void ThumbBarSetImageList(IntPtr hwnd, IntPtr himl);
// 	[PreserveSig]
// 	void SetOverlayIcon(
// 		IntPtr hwnd,
// 		IntPtr hIcon,
// 		[MarshalAs(UnmanagedType.LPWStr)] string pszDescription);
// 	[PreserveSig]
// 	void SetThumbnailTooltip(
// 		IntPtr hwnd,
// 		[MarshalAs(UnmanagedType.LPWStr)] string pszTip);
// 	[PreserveSig]
// 	void SetThumbnailClip(
// 		IntPtr hwnd,
// 		IntPtr prcClip);
//
// 	// ITaskbarList4
// 	void SetTabProperties(IntPtr hwndTab, SetTabPropertiesOption stpFlags);
// }
//
// [Guid("56FDF344-FD6D-11d0-958A-006097C9A090")]
// [ClassInterface(ClassInterfaceType.None)]
// [ComImport()]
// internal class CTaskbarList { }