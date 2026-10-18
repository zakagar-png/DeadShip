@echo off
setlocal
rem Builds Assemblies\DeadShip.dll from the .cs files in this folder using the C# compiler built into Windows.
rem Double-click this file. The result is written to build.log in this folder.

set "MODDIR=%~dp0.."
set "MANAGED=%~dp0..\..\..\RimWorldWin64_Data\Managed"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"

if not exist "%CSC%" (
  echo Could not find the Windows C# compiler.> "%~dp0build.log"
  echo Could not find the Windows C# compiler. See build.log
  pause
  exit /b 1
)

if not exist "%MODDIR%\Assemblies" mkdir "%MODDIR%\Assemblies"

rem If Combat Extended is installed from the Steam Workshop, build against it so the lurker can fire real CE projectiles.
set "CEDLL=%~dp0..\..\..\..\..\workshop\content\294100\2890901044\Assemblies\CombatExtended.dll"
set "CEREF="
set "CEDEF="
if exist "%CEDLL%" (
  set "CEREF=/reference:%CEDLL%"
  set "CEDEF=/define:CE"
  echo Building with Combat Extended support.
) else (
  echo Combat Extended not found at the Workshop path: building without CE support.
)

set "MATHREF="
if exist "%MANAGED%\Unity.Mathematics.dll" set "MATHREF=/reference:%MANAGED%\Unity.Mathematics.dll"

set "LUDEONREF="
if exist "%MANAGED%\LudeonTK.dll" set "LUDEONREF=/reference:%MANAGED%\LudeonTK.dll"

"%CSC%" /nologo /target:library /optimize+ /out:"%MODDIR%\Assemblies\DeadShip.dll" ^
  /reference:"%MANAGED%\Assembly-CSharp.dll" ^
  /reference:"%MANAGED%\UnityEngine.dll" ^
  /reference:"%MANAGED%\UnityEngine.CoreModule.dll" ^
  /reference:"%MANAGED%\netstandard.dll" ^
  %MATHREF% %LUDEONREF% %CEREF% %CEDEF% ^
  "%~dp0*.cs" > "%~dp0build.log" 2>&1

if errorlevel 1 (
  echo.
  echo BUILD FAILED. The details are in build.log in this folder.
  type "%~dp0build.log"
) else (
  echo.
  echo BUILD OK. DeadShip.dll was written to the Assemblies folder.
  echo Restart RimWorld to load it.
)
echo.
pause
