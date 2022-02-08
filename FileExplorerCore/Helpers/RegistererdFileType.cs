using System;
using System.Collections.Generic;
using Microsoft.Win32;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace FileTypeAndIcon
{
	/// <summary>
	/// Structure that encapsulates basic information of icon embedded in a file.
	/// </summary>
	public struct EmbeddedIconInfo
	{
		public string FileName;
		public int IconIndex;
	}

	public class RegisteredFileType
	{
		#region APIs

		[DllImport("shell32.dll", EntryPoint = "ExtractIconA", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
		private static extern IntPtr ExtractIcon(int hInst, string lpszExeFileName, int nIconIndex);

		[DllImport("shell32.dll", CharSet = CharSet.Auto)]
		private static extern uint ExtractIconEx(string szFileName, int nIconIndex, IntPtr[] phiconLarge, IntPtr[] phiconSmall, uint nIcons);

		[DllImport("user32.dll", EntryPoint = "DestroyIcon", SetLastError = true)]
		private static unsafe extern int DestroyIcon(IntPtr hIcon);

		#endregion

		#region CORE METHODS

		/// <summary>
		/// Gets registered file types and their associated icon in the system.
		/// </summary>
		/// <returns>Returns a hash table which contains the file extension as keys, the icon file and param as values.</returns>
		[SupportedOSPlatform("Windows")]
		public static Dictionary<string, string> GetFileTypeAndIcon()
		{
			try
			{
				// Create a registry key object to represent the HKEY_CLASSES_ROOT registry section
				var rkRoot = Registry.ClassesRoot;

				//Gets all sub keys' names.
				var keyNames = rkRoot.GetSubKeyNames();
				var iconsInfo = new Dictionary<string, string>();

				//Find the file icon.
				foreach (var keyName in keyNames)
				{
					if (String.IsNullOrEmpty(keyName))
						continue;
					var indexOfPoint = keyName.IndexOf(".");

					//If this key is not a file exttension(eg, .zip), skip it.
					if (indexOfPoint != 0)
						continue;

					var rkFileType = rkRoot.OpenSubKey(keyName);
					if (rkFileType == null)
						continue;

					//Gets the default value of this key that contains the information of file type.
					var defaultValue = rkFileType.GetValue("");
					if (defaultValue == null)
						continue;

					//Go to the key that specifies the default icon associates with this file type.
					var defaultIcon = defaultValue.ToString() + "\\DefaultIcon";
					var rkFileIcon = rkRoot.OpenSubKey(defaultIcon);
					if (rkFileIcon != null)
					{
						//Get the file contains the icon and the index of the icon in that file.
						var value = rkFileIcon.GetValue("");
						if (value != null)
						{
							//Clear all unecessary " sign in the string to avoid error.
							var fileParam = value.ToString().Replace("\"", "");
							iconsInfo.Add(keyName, fileParam);
						}
						rkFileIcon.Close();
					}
					rkFileType.Close();
				}
				rkRoot.Close();
				return iconsInfo;
			}
			catch (Exception exc)
			{
				throw exc;
			}
		}

		/// <summary>
		/// Extract the icon from file.
		/// </summary>
		/// <param name="fileAndParam">The params string, 
		/// such as ex: "C:\\Program Files\\NetMeeting\\conf.exe,1".</param>
		/// <returns>This method always returns the large size of the icon (may be 32x32 px).</returns>
		public static Icon ExtractIconFromFile(string fileAndParam)
		{
			try
			{
				var embeddedIcon = getEmbeddedIconInfo(fileAndParam);

				//Gets the handle of the icon.
				var lIcon = ExtractIcon(0, embeddedIcon.FileName, embeddedIcon.IconIndex);

				//Gets the real icon.
				return Icon.FromHandle(lIcon);
			}
			catch (Exception exc)
			{
				throw exc;
			}
		}

		/// <summary>
		/// Extract the icon from file.
		/// </summary>
		/// <param name="fileAndParam">The params string, 
		/// such as ex: "C:\\Program Files\\NetMeeting\\conf.exe,1".</param>
		/// <returns>This method always returns the large size of the icon (may be 32x32 px).</returns>
		public static IntPtr ExtractIconFromFileHIcon(string fileAndParam)
		{
			try
			{
				var embeddedIcon = getEmbeddedIconInfo(fileAndParam);

				//Gets the handle of the icon.
				return ExtractIcon(0, embeddedIcon.FileName, embeddedIcon.IconIndex);
			}
			catch (Exception exc)
			{
				throw exc;
			}
		}

		/// <summary>
		/// Extract the icon from file.
		/// </summary>
		/// <param name="fileAndParam">The params string, 
		/// such as ex: "C:\\Program Files\\NetMeeting\\conf.exe,1".</param>
		/// <param name="isLarge">
		/// Determines the returned icon is a large (may be 32x32 px) 
		/// or small icon (16x16 px).</param>
		public static Icon ExtractIconFromFile(string fileAndParam, bool isLarge)
		{
			unsafe
			{
				uint readIconCount = 0;
				var hDummy = new IntPtr[1] { IntPtr.Zero };
				var hIconEx = new IntPtr[1] { IntPtr.Zero };

				try
				{
					var embeddedIcon = getEmbeddedIconInfo(fileAndParam);

					if (isLarge)
						readIconCount = ExtractIconEx(embeddedIcon.FileName, 0, hIconEx, hDummy, 1);
					else
						readIconCount = ExtractIconEx(embeddedIcon.FileName, 0, hDummy, hIconEx, 1);

					if (readIconCount > 0 && hIconEx[0] != IntPtr.Zero)
					{
						// Get first icon.
						var extractedIcon = (Icon)Icon.FromHandle(hIconEx[0]).Clone();

						return extractedIcon;
					}
					else // No icon read
						return null;
				}
				catch (Exception exc)
				{
					// Extract icon error.
					throw new ApplicationException("Could not extract icon", exc);
				}
				finally
				{
					// Release resources.
					foreach (var ptr in hIconEx)
						if (ptr != IntPtr.Zero)
							DestroyIcon(ptr);

					foreach (var ptr in hDummy)
						if (ptr != IntPtr.Zero)
							DestroyIcon(ptr);
				}
			}
		}

		#endregion

		#region UTILITY METHODS

		/// <summary>
		/// Parses the parameters string to the structure of EmbeddedIconInfo.
		/// </summary>
		/// <param name="fileAndParam">The params string, 
		/// such as ex: "C:\\Program Files\\NetMeeting\\conf.exe,1".</param>
		/// <returns></returns>
		protected static EmbeddedIconInfo getEmbeddedIconInfo(string fileAndParam)
		{
			var embeddedIcon = new EmbeddedIconInfo();

			if (String.IsNullOrEmpty(fileAndParam))
				return embeddedIcon;

			//Use to store the file contains icon.
			var fileName = String.Empty;

			//The index of the icon in the file.
			var iconIndex = 0;
			var iconIndexString = String.Empty;

			var commaIndex = fileAndParam.IndexOf(",");
			//if fileAndParam is some thing likes that: "C:\\Program Files\\NetMeeting\\conf.exe,1".
			if (commaIndex > 0)
			{
				fileName = fileAndParam.Substring(0, commaIndex);
				iconIndexString = fileAndParam.Substring(commaIndex + 1);
			}
			else
				fileName = fileAndParam;

			if (!String.IsNullOrEmpty(iconIndexString))
			{
				//Get the index of icon.
				iconIndex = int.Parse(iconIndexString);
				if (iconIndex < 0)
					iconIndex = 0;  //To avoid the invalid index.
			}

			embeddedIcon.FileName = fileName;
			embeddedIcon.IconIndex = iconIndex;

			return embeddedIcon;
		}

		#endregion
	}
}
