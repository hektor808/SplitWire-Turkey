@echo off
setlocal EnableExtensions

echo Building SplitWire-Turkey C# WPF Application (CI/CD compatible)...

REM ---- Configurable parameters (CLI arg > ENV var > default) ----
set "PROJECT_DIR=SplitWireTurkey"
set "CONFIGURATION=Release"
set "TFM=net8.0-windows"
set "ARTIFACTS_ROOT=..\artifacts"
set "RES_DIR=Resources"
set "ADD_TO_ROOT_DIR=..\AddToRoot"

if not "%~1"=="" set "ARTIFACTS_ROOT=%~1"
if not "%BUILD_ARTIFACTS_DIR%"=="" set "ARTIFACTS_ROOT=%BUILD_ARTIFACTS_DIR%"
if not "%BUILD_CONFIGURATION%"=="" set "CONFIGURATION=%BUILD_CONFIGURATION%"
if not "%BUILD_TFM%"=="" set "TFM=%BUILD_TFM%"
if not "%BUILD_PROJECT_DIR%"=="" set "PROJECT_DIR=%BUILD_PROJECT_DIR%"

set "APP_ARTIFACT_DIR=%ARTIFACTS_ROOT%\app"
set "PACKAGE_INPUT_DIR=%ARTIFACTS_ROOT%\package-input"
if not "%BUILD_PACKAGE_INPUT_DIR%"=="" set "PACKAGE_INPUT_DIR=%BUILD_PACKAGE_INPUT_DIR%"
if not "%BUILD_APP_ARTIFACT_DIR%"=="" set "APP_ARTIFACT_DIR=%BUILD_APP_ARTIFACT_DIR%"

set "OUTPUT_DIR=%PROJECT_DIR%\bin\%CONFIGURATION%\%TFM%"

REM Check if .NET SDK is installed
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo Error: .NET SDK not found. Please install .NET 8.0 SDK or later.
    exit /b 1
)

REM Clean previous builds
if exist "%PROJECT_DIR%\bin" rmdir /s /q "%PROJECT_DIR%\bin"
if exist "%PROJECT_DIR%\obj" rmdir /s /q "%PROJECT_DIR%\obj"

REM Ensure folders exist
if not exist "%PROJECT_DIR%\%RES_DIR%" mkdir "%PROJECT_DIR%\%RES_DIR%"
if not exist "%ARTIFACTS_ROOT%" mkdir "%ARTIFACTS_ROOT%"
if not exist "%APP_ARTIFACT_DIR%" mkdir "%APP_ARTIFACT_DIR%"
if not exist "%PACKAGE_INPUT_DIR%" mkdir "%PACKAGE_INPUT_DIR%"

REM Build the application
dotnet restore "%PROJECT_DIR%\SplitWireTurkey.csproj"
if errorlevel 1 (
    echo Error: dotnet restore failed!
    exit /b 1
)

dotnet build "%PROJECT_DIR%\SplitWireTurkey.csproj" -c "%CONFIGURATION%" -f "%TFM%"
if errorlevel 1 (
    echo Error: dotnet build failed!
    exit /b 1
)

REM Check if critical files exist
if not exist "%OUTPUT_DIR%\SplitWire-Turkey.exe" (
    echo Error: Main executable not found! Build may have failed.
    exit /b 1
)

REM Remove unnecessary files
if exist "%OUTPUT_DIR%\SplitWire-Turkey.deps.json" del "%OUTPUT_DIR%\SplitWire-Turkey.deps.json"
if exist "%OUTPUT_DIR%\SplitWire-Turkey.pdb" del "%OUTPUT_DIR%\SplitWire-Turkey.pdb"
if exist "%OUTPUT_DIR%\SplitWire-Turkey.xml" del "%OUTPUT_DIR%\SplitWire-Turkey.xml"

REM Copy AddToRoot contents (if present)
if exist "%ADD_TO_ROOT_DIR%" (
    xcopy "%ADD_TO_ROOT_DIR%\*" "%OUTPUT_DIR%\" /E /I /Y >nul
    if errorlevel 1 (
        echo Error: AddToRoot content copy failed.
        exit /b 1
    )
) else (
    echo Warning: AddToRoot folder not found, skipping.
)

REM Standardize release artifacts
xcopy "%OUTPUT_DIR%\*" "%APP_ARTIFACT_DIR%\" /E /I /Y >nul
if errorlevel 1 (
    echo Error: Failed to copy application files to %APP_ARTIFACT_DIR%.
    exit /b 1
)

xcopy "%OUTPUT_DIR%\*" "%PACKAGE_INPUT_DIR%\" /E /I /Y >nul
if errorlevel 1 (
    echo Error: Failed to prepare package input files in %PACKAGE_INPUT_DIR%.
    exit /b 1
)

echo.
echo Build complete!
echo Application artifact: %APP_ARTIFACT_DIR%
echo Installer package input: %PACKAGE_INPUT_DIR%
echo.

endlocal
exit /b 0
