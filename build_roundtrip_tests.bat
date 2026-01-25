@echo off
SETLOCAL ENABLEDELAYEDEXPANSION

:: ============================================================================
:: Roundtrip Tests Build Script
:: ============================================================================
:: Builds the complete roundtrip verification framework including:
:: - Native C DLL (CMake)
:: - C# Test Application (.NET)
:: ============================================================================

:: Parse arguments
SET BUILD_TYPE=%1
IF "%BUILD_TYPE%"=="" SET BUILD_TYPE=Release

IF /I "%BUILD_TYPE%" NEQ "Debug" IF /I "%BUILD_TYPE%" NEQ "Release" (
    echo ERROR: Build type must be "Debug" or "Release"
    echo Usage: build_roundtrip_tests.bat [Debug^|Release]
    exit /b 1
)

:: Root paths
SET ROOT=%~dp0
SET NATIVE_DIR=%ROOT%tests\CycloneDDS.Roundtrip.Tests\Native
SET APP_DIR=%ROOT%tests\CycloneDDS.Roundtrip.Tests\App
SET BUILD_DIR=%NATIVE_DIR%\build

echo.
echo ========================================================
echo   CycloneDDS Roundtrip Tests Builder
echo ========================================================
echo   Build Type: %BUILD_TYPE%
echo ========================================================
echo.

:: ============================================================================
:: Step 1: Ensure Cyclone is Built
:: ============================================================================

echo [Step 1/4] Checking CycloneDDS...

IF NOT EXIST "%ROOT%cyclone-compiled\bin\ddsc.dll" (
    echo [Cyclone] Not found. Building CycloneDDS first...
    echo.
    
    IF /I "%BUILD_TYPE%"=="Debug" (
        call "%ROOT%build_cyclone_debug.bat"
    ) ELSE (
        call "%ROOT%build_cyclone.bat"
    )
    
    IF ERRORLEVEL 1 (
        echo.
        echo [ERROR] CycloneDDS build failed
        exit /b 1
    )
) ELSE (
    echo [Cyclone] Found: %ROOT%cyclone-compiled
)

echo.

:: ============================================================================
:: Step 2: Build Native DLL (CMake)
:: ============================================================================

echo [Step 2/4] Building Native DLL...
echo.

IF NOT EXIST "%BUILD_DIR%" (
    echo [CMake] Creating build directory...
    mkdir "%BUILD_DIR%"
)

pushd "%BUILD_DIR%"

echo [CMake] Configuring (Visual Studio 2022)...
cmake -G "Visual Studio 17 2022" -A x64 ^
    -DCMAKE_BUILD_TYPE=%BUILD_TYPE% ^
    -DCYCLONE_INSTALL_DIR="%ROOT%cyclone-compiled" ^
    ..

IF ERRORLEVEL 1 (
    echo.
    echo [ERROR] CMake configuration failed
    popd
    exit /b 1
)

echo.
echo [CMake] Compiling...
cmake --build . --config %BUILD_TYPE% --parallel

IF ERRORLEVEL 1 (
    echo.
    echo [ERROR] Native DLL build failed
    popd
    exit /b 1
)

SET NATIVE_DLL=%BUILD_DIR%\%BUILD_TYPE%\CycloneDDS.Roundtrip.Native.dll

IF NOT EXIST "%NATIVE_DLL%" (
    echo.
    echo [ERROR] DLL not found at: %NATIVE_DLL%
    popd
    exit /b 1
)

echo [Native] DLL built: %NATIVE_DLL%

popd
echo.

:: ============================================================================
:: Step 3: Build C# Application
:: ============================================================================

echo [Step 3/4] Building C# Application...
echo.


echo.

echo [C#] Restoring packages...
dotnet restore "%APP_DIR%\CycloneDDS.Roundtrip.App.csproj"


IF ERRORLEVEL 1 (
    echo.
    echo [ERROR] NuGet restore failed
    exit /b 1
)

echo.
echo [C#] Building project...
dotnet build "%APP_DIR%\CycloneDDS.Roundtrip.App.csproj" -c %BUILD_TYPE% --no-restore

IF ERRORLEVEL 1 (
    echo.
    echo [ERROR] C# build failed
    exit /b 1
)

echo.

:: ============================================================================
:: Step 4: Deploy DLLs
:: ============================================================================

:DEPLOY
echo [Step 4/4] Deploying DLLs...
echo.

IF EXIST "%APP_DIR%\bin\%BUILD_TYPE%\net8.0" (
    SET CSHARP_OUT=%APP_DIR%\bin\%BUILD_TYPE%\net8.0
    
    echo [Deploy] Copying Native DLL...
    copy /Y "%NATIVE_DLL%" "%CSHARP_OUT%\" >nul
    
    echo [Deploy] Copying CycloneDDS DLLs...
    copy /Y "%ROOT%cyclone-compiled\bin\ddsc.dll" "%CSHARP_OUT%\" >nul
    copy /Y "%ROOT%cyclone-compiled\bin\cycloneddsidl.dll" "%CSHARP_OUT%\" >nul
    
    echo [Deploy] Deployment complete
) ELSE (
    echo [Deploy] C# output directory not found (C# build skipped)
    echo [Deploy] Native DLL is ready at: %NATIVE_DLL%
)

echo.

:: ============================================================================
:: Summary
:: ============================================================================

echo ========================================================
echo   Build Complete!
echo ========================================================
echo   Build Type:   %BUILD_TYPE%
echo   Native DLL:   %NATIVE_DLL%

IF EXIST "%CSHARP_OUT%" (
    echo   C# Output:    %CSHARP_OUT%
    echo.
    echo   Run Tests:
    echo   %CSHARP_OUT%\CycloneDDS.Roundtrip.App.exe
) ELSE (
    echo.
    echo   Native DLL is ready for integration testing
)

echo ========================================================
echo.

ENDLOCAL
exit /b 0
