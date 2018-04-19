::::::::::::::::::::::::::::::
:::::::ToLuaPkgGenerator::::::
:::::::::::::2017:::::::::::::
::Copyright © Mathew Aloisio::
::::::::::::::::::::::::::::::

@echo off

:: Configuration (Edit this if neccesary.)
set SEARCH_DIR[1]="%~dp0Source"
set SEARCH_DIR[2]="%~dp0..\Basecode\Source"
::set SEARCH_DIR[3]="C:\Program Files (x86)\Steam\steamapps\common\Leadwerks\Include"
set SEARCH_DIR.length=2

::::::::::::::DO NOT EDIT CODE UNDER THIS LINE::::::::::::::

:: Generate argument string.
setlocal EnableExtensions EnableDelayedExpansion
set ARGS=
for /L %%i in (1,1,%SEARCH_DIR.length%) do set ARGS=!ARGS! !SEARCH_DIR[%%i]!

:: ToLua package generation.
cd .\ToLua\
ToLuaPkgGenerator2.exe!ARGS!

:: ToLua source generation & movement.
echo Generating ToLua bindings...

md %~dp0Source\ToLua 2> nul
for %%f in (*.pkg) do (
	toluapp.exe -o %%~nf.cpp %%f
	move /Y "%%~nf.cpp" "%~dp0Source\ToLua\%%~nf.cpp"
	echo No|move /-y "%%~nf_includes.h" "%~dp0Source\ToLua\%%~nf_includes.h"
)
::FixToLuaNamespaces "%~dp0Source\ToLua\%%~nf.cpp" :: THIS BREAKS EVERYTHING WHEN COMMENTED IN LOOPS ), move above ) and uncomment to enable.
move /Y "tolua_export.h" "%~dp0Source\ToLua\tolua_export.h"

pause
