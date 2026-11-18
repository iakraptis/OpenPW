#region Copyright & License Information
/*
 * Copyright (c) The OpenPW Developers and Contributors
 * This file is part of OpenPW, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace OpenRA.Mods.Pw.Widgets.Logic
{
	// Windows only. The "Browse For Folder" dialog (SHBrowseForFolder), for the content installer's Browse button. It
	// runs on its own STA thread so the game keeps drawing; poll IsDone from the UI thread and read Result (null if
	// cancelled).
	public sealed class FolderPicker
	{
		const uint BifReturnOnlyFsDirs = 0x0001;
		const uint BifNewDialogStyle = 0x0040;
		const uint BifNoNewFolderButton = 0x0200;
		const int MaxPath = 32768;

		volatile bool done;

		public bool IsDone => done;
		public string Result { get; private set; }

		public FolderPicker(string title)
		{
			// Own the dialog by the game window (in front of it, and modal to it); the click that opened us left it
			// in the foreground.
			var owner = GetForegroundWindow();
			var thread = new Thread(() =>
			{
				try
				{
					Result = Show(owner, title);
				}
				finally
				{
					done = true;
				}
			});

			if (OperatingSystem.IsWindows())
				thread.SetApartmentState(ApartmentState.STA);
			thread.IsBackground = true;
			thread.Start();
		}

		static string Show(IntPtr owner, string title)
		{
			CoInitializeEx(IntPtr.Zero, 2); // COINIT_APARTMENTTHREADED, needed by the new dialog style.
			try
			{
				var info = new BrowseInfo
				{
					Owner = owner,
					Title = title,
					Flags = BifReturnOnlyFsDirs | BifNewDialogStyle | BifNoNewFolderButton
				};

				var list = SHBrowseForFolderW(ref info);
				if (list == IntPtr.Zero)
					return null;

				try
				{
					var path = new char[MaxPath];
					if (!SHGetPathFromIDListEx(list, path, MaxPath, 0))
						return null;

					var length = Array.IndexOf(path, (char)0);
					return new string(path, 0, length < 0 ? path.Length : length);
				}
				finally
				{
					Marshal.FreeCoTaskMem(list);
				}
			}
			finally
			{
				CoUninitialize();
			}
		}

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		struct BrowseInfo
		{
			public IntPtr Owner;
			public IntPtr Root;
			public IntPtr DisplayName;
			[MarshalAs(UnmanagedType.LPWStr)]
			public string Title;
			public uint Flags;
			public IntPtr Callback;
			public IntPtr Param;
			public int Image;
		}

		[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
		static extern IntPtr SHBrowseForFolderW(ref BrowseInfo info);

		[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
		[return: MarshalAs(UnmanagedType.Bool)]
		static extern bool SHGetPathFromIDListEx(IntPtr list, [Out] char[] path, int length, int options);

		[DllImport("ole32.dll")]
		static extern int CoInitializeEx(IntPtr reserved, uint coInit);

		[DllImport("ole32.dll")]
		static extern void CoUninitialize();

		[DllImport("user32.dll")]
		static extern IntPtr GetForegroundWindow();
	}
}
