// The OpenPW.exe at the top of a portable folder (update-portable.ps1): it starts the game in the folder's game\
// subfolder, which holds the engine, its .NET runtime, the mod and the Support folder. Built with the C# compiler of
// .NET Framework 4, which every Windows 10 and 11 PC has, so it needs nothing of its own.
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

static class PortableLauncher
{
	[STAThread]
	static int Main(string[] args)
	{
		var folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "game");
		var game = Path.Combine(folder, "OpenPW.exe");
		if (!File.Exists(game))
		{
			MessageBox.Show("The game folder is missing: " + game + "\n\nUnzip the whole OpenPW folder and try again.",
				"OpenPW", MessageBoxButtons.OK, MessageBoxIcon.Error);
			return 1;
		}

		var arguments = string.Join(" ", args.Select(a => "\"" + a + "\"").ToArray());
		Process.Start(new ProcessStartInfo(game, arguments) { WorkingDirectory = folder, UseShellExecute = false });
		return 0;
	}
}
