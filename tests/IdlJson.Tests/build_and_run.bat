:: usage:
::   build_and_run.bat [Debug|Release]

@echo off
setlocal

set "REPO_ROOT=%~dp0..\.."
set "TEST_DIR=%REPO_ROOT%\tests\IdlJson.Tests"

:: Default to Release
set "BUILD_CONFIG=Release"
if not "%1"=="" set "BUILD_CONFIG=%1"

if /I "%BUILD_CONFIG%"=="Debug" (
    set "CYCLONE_INSTALL_DIR=%REPO_ROOT%\artifacts\native-install"
) else (
    set "CYCLONE_INSTALL_DIR=%REPO_ROOT%\artifacts\native-install"
)

set "CYCLONE_INSTALL_BIN=%CYCLONE_INSTALL_DIR%\bin"

echo ==========================================
echo Configuration: %BUILD_CONFIG%
echo Cyclone Install: %CYCLONE_INSTALL_DIR%
echo ==========================================

if not exist "%CYCLONE_INSTALL_BIN%\idlc.exe" (
    echo Error: Cyclone DDS build not found at %CYCLONE_INSTALL_BIN%
    echo Please run build_cyclone.bat or build_cyclone_debug.bat first.
    exit /b 1
)

echo.
echo Configuring and Building IdlJson.Tests...
if not exist "%TEST_DIR%\build" mkdir "%TEST_DIR%\build"
pushd "%TEST_DIR%\build"

cmake .. -DCMAKE_PREFIX_PATH="%CYCLONE_INSTALL_DIR%" -DCYCLONE_INSTALL_DIR="%CYCLONE_INSTALL_DIR%"
if %ERRORLEVEL% NEQ 0 (
    echo CMake configuration failed
    popd
    exit /b %ERRORLEVEL%
)

cmake --build . --config %BUILD_CONFIG%
if %ERRORLEVEL% NEQ 0 (
    echo Build failed
    popd
    exit /b %ERRORLEVEL%
)

echo.
echo Running verify_layout...
set "PATH=%CYCLONE_INSTALL_BIN%;%PATH%"
%BUILD_CONFIG%\verify_layout.exe %BUILD_CONFIG%\verification.idl.json

popd
endlocal
