@echo off
setlocal

set "ROOT_DIR=%~dp0"
set "ROOT_DIR=%ROOT_DIR:~0,-1%"
set "PROJECT_DIR=%ROOT_DIR%\Saradomin"
set "OUT_DIR=%ROOT_DIR%\dist"

if not "%~1"=="" (
  set "VERSION=%~1"
) else (
  set /p VERSION=Version (e.g. 1.7.0): 
)

if "%VERSION%"=="" (
  echo Version is required.
  exit /b 1
)

echo ==> Building AmiliousScape Launcher v%VERSION%
if not exist "%OUT_DIR%" mkdir "%OUT_DIR%"
cd /d "%PROJECT_DIR%" || exit /b 1

taskkill /F /IM Saradomin.exe >nul 2>&1

dotnet restore
if errorlevel 1 exit /b 1

set "COMMON_FLAGS=-c Release --self-contained true -p:Version=%VERSION% -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false"

echo ==> Publishing Windows x64
dotnet publish -r win-x64 %COMMON_FLAGS%
if errorlevel 1 exit /b 1
copy /Y "bin\Release\net6.0\win-x64\publish\Saradomin.exe" "%OUT_DIR%\AmiliousScape-Launcher-win-x64.exe" >nul

echo ==> Publishing Linux x64
dotnet publish -r linux-x64 %COMMON_FLAGS%
if errorlevel 1 exit /b 1
copy /Y "bin\Release\net6.0\linux-x64\publish\Saradomin" "%OUT_DIR%\AmiliousScape-Launcher-linux-x64" >nul

echo ==> Done v%VERSION%
dir "%OUT_DIR%"
endlocal