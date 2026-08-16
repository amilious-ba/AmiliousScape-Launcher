@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "ROOT_DIR=%~dp0"
set "ROOT_DIR=%ROOT_DIR:~0,-1%"
set "PROJECT_DIR=%ROOT_DIR%\Saradomin"
set "OUT_DIR=%ROOT_DIR%\dist"

echo ==> Building AmiliousScape Launcher
echo Root: %ROOT_DIR%

if not exist "%OUT_DIR%" mkdir "%OUT_DIR%"
cd /d "%PROJECT_DIR%" || exit /b 1

echo ==> Stopping any running launcher
taskkill /F /IM Saradomin.exe >nul 2>&1
taskkill /F /IM AmiliousScape-Launcher-win-x64.exe >nul 2>&1

echo ==> Restoring packages
dotnet restore
if errorlevel 1 exit /b 1

set "COMMON_FLAGS=-c Release --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false"

echo ==> Publishing Windows x64
if exist "bin\Release\net6.0\win-x64\publish" rmdir /s /q "bin\Release\net6.0\win-x64\publish"
dotnet publish -r win-x64 %COMMON_FLAGS%
if errorlevel 1 exit /b 1
copy /Y "bin\Release\net6.0\win-x64\publish\Saradomin.exe" "%OUT_DIR%\AmiliousScape-Launcher-win-x64.exe" >nul

echo ==> Publishing Linux x64
if exist "bin\Release\net6.0\linux-x64\publish" rmdir /s /q "bin\Release\net6.0\linux-x64\publish"
dotnet publish -r linux-x64 %COMMON_FLAGS%
if errorlevel 1 exit /b 1
copy /Y "bin\Release\net6.0\linux-x64\publish\Saradomin" "%OUT_DIR%\AmiliousScape-Launcher-linux-x64" >nul

echo.
echo ==> Done
echo Windows: %OUT_DIR%\AmiliousScape-Launcher-win-x64.exe
echo Linux:   %OUT_DIR%\AmiliousScape-Launcher-linux-x64
dir "%OUT_DIR%"

endlocal