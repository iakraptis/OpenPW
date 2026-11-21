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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Pw.AssetInstaller;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Pw.Widgets.Logic
{
	// The pw-content screen (as OpenSYW's): shown when the converted game content is missing (first launch, or after a
	// change of PwContent.Version). Finds the player's Primitive Wars folder (detected, or picked with Browse), converts
	// it into the support folder on a background thread (every stage, without our test maps), then starts the game.
	public class PwContentLogic : ChromeLogic
	{
		const string GameMod = "pw";

		enum State { NotFound, Found, Installed, Installing, Failed }

		readonly string modFolder;
		State state;
		string gamePath;
		string error;
		FolderPicker picker;

		// Written by the install thread, read by the UI.
		volatile int progress;
		volatile string currentStage;
		Task install;

		[ObjectCreator.UseCtor]
		public PwContentLogic(Widget widget)
		{
			modFolder = Game.Mods[GameMod].Package.Name;
			var panel = widget.Get("PANEL");

			panel.Get<LabelWidget>("MESSAGE").GetText = Message;
			panel.Get<LabelWidget>("PATH").GetText = () => gamePath ?? "";

			var bar = panel.Get<ProgressBarWidget>("PROGRESS");
			bar.IsVisible = () => state == State.Installing;
			bar.GetPercentage = () => progress;

			var status = panel.Get<LabelWidget>("STATUS");
			status.GetText = () => state == State.Installing
				? FluentProvider.GetMessage("pw-content-converting", "stage", currentStage ?? "")
				: state == State.Failed ? error : "";
			status.GetColor = () => state == State.Failed ? Color.FromArgb(0xFF, 0x60, 0x60) : status.TextColor;

			var browse = panel.Get<ButtonWidget>("BROWSE_BUTTON");
			browse.IsVisible = OperatingSystem.IsWindows;
			browse.IsDisabled = () => state == State.Installing || picker != null;
			browse.OnClick = () =>
			{
				if (OperatingSystem.IsWindows())
					picker = new FolderPicker(FluentProvider.GetMessage("pw-content-browse-title"));
			};

			var installButton = panel.Get<ButtonWidget>("INSTALL_BUTTON");
			installButton.GetText = () => FluentProvider.GetMessage(state == State.Installed ? "button-pw-content-reinstall" : "button-pw-content-install");
			installButton.IsDisabled = () => gamePath == null || state == State.Installing || picker != null;
			installButton.OnClick = StartInstall;

			// Back to the game when the content is there, otherwise the only way out is to quit.
			var back = panel.Get<ButtonWidget>("BACK_BUTTON");
			back.GetText = () => FluentProvider.GetMessage(IsInstalled() ? "button-pw-content-play" : "button-pw-content-quit");
			back.IsDisabled = () => state == State.Installing;
			back.OnClick = () =>
			{
				if (IsInstalled())
					Game.RunAfterTick(() => Game.InitializeMod(Game.Mods[GameMod], new Arguments()));
				else
					Game.Exit();
			};

			gamePath = PwContent.Detect();
			state = IsInstalled() ? State.Installed : gamePath != null ? State.Found : State.NotFound;
		}

		static bool IsInstalled() => File.Exists(Path.Combine(PwContent.Folder, PwContent.ManifestFile));

		string Message()
		{
			switch (state)
			{
				case State.Installed: return FluentProvider.GetMessage("pw-content-installed");
				case State.Found: return FluentProvider.GetMessage("pw-content-found");
				case State.Installing: return FluentProvider.GetMessage("pw-content-installing");
				case State.Failed: return FluentProvider.GetMessage("pw-content-failed");
				default: return FluentProvider.GetMessage("pw-content-not-found");
			}
		}

		void StartInstall()
		{
			var source = gamePath;
			var stages = Stages.All.ToList();
			state = State.Installing;
			progress = 0;
			currentStage = null;

			install = Task.Run(() =>
			{
				// Start from an empty folder: it is ours alone, and files an older converter wrote must not linger.
				if (Directory.Exists(PwContent.Folder))
					Directory.Delete(PwContent.Folder, true);

				var index = 0;
				PwContent.Run(source, PwContent.Folder, modFolder, stages, writeYaml: false, mergeManifest: false, stage =>
				{
					currentStage = stage.Description;
					progress = 100 * index++ / stages.Count;
				}, testMaps: false);

				progress = 100;
			});
		}

		public override void Tick()
		{
			if (picker != null && picker.IsDone)
			{
				var chosen = picker.Result;
				picker = null;
				if (chosen != null)
				{
					if (GameFolder.IsGameFolder(chosen))
					{
						gamePath = chosen;
						state = IsInstalled() ? State.Installed : State.Found;
					}
					else
					{
						error = FluentProvider.GetMessage("pw-content-not-game-folder", "folder", chosen);
						state = State.Failed;
					}
				}
			}

			if (install != null && install.IsCompleted)
			{
				var task = install;
				install = null;
				if (task.IsFaulted)
				{
					var e = task.Exception?.GetBaseException();
					Log.Write("debug", $"Content install failed: {e}");
					error = e?.Message ?? "";
					state = State.Failed;
				}
				else
					Game.RunAfterTick(() => Game.InitializeMod(Game.Mods[GameMod], new Arguments()));
			}
		}
	}
}
