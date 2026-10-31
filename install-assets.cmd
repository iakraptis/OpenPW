@echo off
rem Converts an original Primitive Wars installation (tilesets, maps, and later art and sounds) into the mod's content.
rem   install-assets.cmd [GAME_FOLDER] [--only STAGE,...] [--write-yaml] [--list]
rem GAME_FOLDER defaults to a "Primitive Wars" folder next to this repository. Run "make" first so the engine is built.
setlocal EnableDelayedExpansion
set ROOT=%~dp0

rem The options are passed on as typed (%*): taking them one by one (%1, %2, ...) would split "--only tilesets,maps"
rem at the comma, as cmd treats commas like spaces. Only a leading GAME_FOLDER is cut off and made absolute.
set GAME=
set "REST=%*"
set FIRST=%~1
if not "!FIRST!" == "" if not "!FIRST:~0,1!" == "-" (
	set GAME="%~f1"
	set "REST=!REST:*%1=!"
)

cd /d "%ROOT%"
call "%ROOT%utility.cmd" pw --install-assets !GAME! !REST!
