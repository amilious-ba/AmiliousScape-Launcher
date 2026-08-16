@echo off
setlocal EnableExtensions

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
  pause
  exit /b 1
)

echo ==> Building AmiliousScape Launcher v%VERSION%
if not exist "%OUT_DIR%" mkdir "%OUT_DIR%"

cd /d "%PROJECT_DIR%"
if errorlevel 1 (
  echo Could not cd to "%PROJECT_DIR%"
  pause
  exit /b 1
)

taskkill /F /IM Saradomin.exe >nul 2>&1

echo ==> Restoring packages
dotnet restore
if errorlevel 1 (
  echo Restore failed.
  pause
  exit /b 1
)

set "COMMON_FLAGS=-c Release --self-contained true -p:Version=%VERSION% -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false"

echo ==> Publishing Windows x64
dotnet publish -r win-x64 %COMMON_FLAGS%
if errorlevel 1 (
  echo Windows publish failed.
  pause
  exit /b 1
)
copy /Y "bin\Release\net6.0\win-x64\publish\Saradomin.exe" "%OUT_DIR%\AmiliousScape-Launcher-win-x64.exe"
if errorlevel 1 (
  echo Copy Windows binary failed.
  pause
  exit /b 1
)

echo ==> Publishing Linux x64
dotnet publish -r linux-x64 %COMMON_FLAGS%
if errorlevel 1 (
  echo Linux publish failed.
  pause
  exit /b 1
)
copy /Y "bin\Release\net6.0\linux-x64\publish\Saradomin" "%OUT_DIR%\AmiliousScape-Launcher-linux-x64"
if errorlevel 1 (
  echo Copy Linux binary failed.
  pause
  exit /b 1
)

echo.
echo ==> Done v%VERSION%
dir "%OUT_DIR%"
echo.
pause
endlocal