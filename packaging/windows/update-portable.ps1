# Makes or updates a portable OpenPW folder outside the repository (by default ..\OpenPW-portable, next to the
# checkout) from the current working copy: builds the game with package-windows.ps1 (no installer), mirrors it into the
# folder and bundles this PC's converted Primitive Wars content in its Support\Content\pw, so the folder plays as it is
# on any Windows PC: no .NET, no original game, no install. The folder's own Support data (settings, replays, logs) is
# kept. With -Zip it installs an existing portable zip instead of building. With -Pack it also zips the finished folder
# (OpenPW-portable-<tag>.zip next to it) to hand over.
#
#   powershell -ExecutionPolicy Bypass -File packaging\windows\update-portable.ps1 [-Destination PATH]
#       [-Content PATH] [-Tag NAME] [-Zip OpenPW-...-winportable.zip] [-SkipContent] [-Pack]
#
# The bundled content is converted from the original game, so the folder (and its zip) is for private testing only,
# never a public release. It never enters the repository: the folder must be outside it. The content comes from this
# PC's OpenRA content folder; run install-assets.cmd first.
#
# The folder holds only OpenPW.exe (PortableLauncher.cs, which starts game\OpenPW.exe), the testers' readme and game\:
# the engine, its runtime and the mod, and game\Support, which the engine uses instead of %APPDATA%\OpenRA because it
# sits beside it (settings, the bundled content, logs).
param(
	[string]$Destination,
	[string]$Content = (Join-Path $env:APPDATA 'OpenRA\Content\pw'),
	[string]$Tag,
	[string]$Zip,
	[switch]$SkipContent,
	[switch]$Pack
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if (-not $Destination) { $Destination = Join-Path (Split-Path $root) 'OpenPW-portable' }

function Step([string]$text) { Write-Host "==> $text" -ForegroundColor Cyan }

# Robocopy: exit codes below 8 mean success (0 nothing to do, 1 files copied, 2-7 extra or mismatched files handled).
function Invoke-Mirror([string]$from, [string]$to, [string[]]$extra) {
	& robocopy $from $to /MIR /NFL /NDL /NJH /NP /R:2 /W:1 @extra | Out-Host
	if ($LASTEXITCODE -ge 8) { throw "robocopy $from -> $to failed with exit code $LASTEXITCODE" }
}

$Destination = [IO.Path]::GetFullPath($Destination)
if ($Destination.StartsWith($root.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
	throw "The portable folder must be outside the repository ($root), so its game content can never be committed."
}

if (Test-Path (Join-Path $Destination 'OpenRA.Game.dll')) {
	throw "$Destination has the game at its top (an older layout). Delete it, or keep its Support folder elsewhere."
}

# Files of a running game can't be replaced.
$running = Get-Process -Name OpenPW -ErrorAction SilentlyContinue |
	Where-Object { $_.Path -and $_.Path.StartsWith($Destination, [StringComparison]::OrdinalIgnoreCase) }
if ($running) { throw "OpenPW is running from $Destination. Close it first." }

if (-not $Tag) {
	Push-Location $root
	try {
		$commit = git rev-parse --short HEAD
		$dirty = if (git status --porcelain) { '-modified' } else { '' }
	}
	finally { Pop-Location }
	$Tag = "test-$commit$dirty"
}

$work = Join-Path ([IO.Path]::GetTempPath()) "openpw-portable-$PID"
try {
	if (-not $Zip) {
		Step "Building the game ($Tag)"
		& (Join-Path $PSScriptRoot 'package-windows.ps1') -Tag $Tag -NoInstaller -OutputDir $work
		$Zip = (Get-ChildItem (Join-Path $work '*-winportable.zip') | Select-Object -First 1).FullName
	}

	Step "Unpacking $Zip"
	$game = Join-Path $work 'game'
	Add-Type -AssemblyName System.IO.Compression.FileSystem
	[IO.Compression.ZipFile]::ExtractToDirectory((Resolve-Path $Zip).Path, $game)
	if (-not (Test-Path (Join-Path $game 'OpenPW.exe'))) { throw "$Zip is not an OpenPW portable zip (no OpenPW.exe)." }

	Step "Updating the game in $Destination"
	# Everything but Support mirrors the new game (files dropped from the game are removed here too); Support is the
	# player's data and the bundled content, never touched by this step.
	$gameFolder = Join-Path $Destination 'game'
	$support = Join-Path $gameFolder 'Support'
	Invoke-Mirror $game $gameFolder @('/XD', $support)
	New-Item -ItemType Directory -Force $support | Out-Null

	Step 'Building the launcher'
	$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
	& $csc /nologo /target:winexe /optimize+ "/win32icon:$(Join-Path $gameFolder 'pw.ico')" `
		"/out:$(Join-Path $Destination 'OpenPW.exe')" (Join-Path $PSScriptRoot 'PortableLauncher.cs') | Out-Host
	if ($LASTEXITCODE -ne 0) { throw "csc failed with exit code $LASTEXITCODE" }
}
finally {
	if (Test-Path $work) { Remove-Item -Recurse -Force $work }
}

# Desktop OpenGL, as launch-game.cmd asks for: the engine would otherwise pick ANGLE on Windows, whose texture readback
# can crash the game. Only a new folder gets these defaults; the testers' own settings are kept.
$settings = Join-Path $support 'settings.yaml'
if (-not (Test-Path $settings)) {
	[IO.File]::WriteAllText($settings, "Graphics:`n`tMode: PseudoFullscreen`n`tGLProfile: Modern`n")
}

if (-not $SkipContent) {
	$target = Join-Path $support 'Content\pw'
	if (Test-Path (Join-Path $Content 'install-manifest.txt')) {
		Step "Bundling the converted game content from $Content"
		# Without the developer maps install-assets adds (AssetInstaller/TestMaps.cs): they need the test harness.
		$devMaps = 'bot-test', 'battle-*'
		Invoke-Mirror $Content $target (@('/XD') + $devMaps)
		foreach ($map in $devMaps) {
			Get-ChildItem (Join-Path $target 'maps') -Directory -Filter $map | Remove-Item -Recurse -Force
		}
	}
	elseif (Test-Path (Join-Path $target 'install-manifest.txt')) {
		Write-Warning "No converted content in $Content; keeping the content already in $target."
	}
	else {
		Write-Warning "No converted content in $Content. Run install-assets.cmd, then update again."
	}
}

[IO.File]::WriteAllText((Join-Path $Destination 'README-testers.txt'), @"
OpenPW $Tag - private test build

Run OpenPW.exe. Nothing needs installing: the game, its .NET runtime and the game content are all in this folder.
Settings, replays and logs are kept in game\Support, so deleting the folder removes everything.

If Windows says "Windows protected your PC", click "More info", then "Run anyway".
If the game crashes, please send the newest files from game\Support\Logs.

This build contains content converted from Primitive Wars. Do not share it.
"@.Replace("`r`n", "`n").Replace("`n", "`r`n"))

if ($Pack) {
	$archive = Join-Path (Split-Path $Destination) "$(Split-Path -Leaf $Destination)-$Tag.zip"
	Step "Packing $archive"
	if (Test-Path $archive) { Remove-Item $archive }
	# Entries written one by one, with forward slashes (see package-windows.ps1); without the folder's logs and replays.
	Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
	$zipFile = [IO.Compression.ZipFile]::Open($archive, [IO.Compression.ZipArchiveMode]::Create)
	try {
		foreach ($file in Get-ChildItem -Recurse -File $Destination) {
			$name = $file.FullName.Substring($Destination.Length + 1).Replace('\', '/')
			if ($name -match '^game/Support/(Logs|Replays|ModMetadata)/') { continue }
			[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zipFile, $file.FullName, "OpenPW/$name",
				[IO.Compression.CompressionLevel]::Optimal) | Out-Null
		}
	}
	finally { $zipFile.Dispose() }
}

Step "Done: $Destination ($Tag)"
