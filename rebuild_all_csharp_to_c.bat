@echo off
echo Building CsharpToC Roundtrip Tests...

echo [1/3] Building Native Library...
mkdir tests\CsharpToC.Roundtrip.Tests\Native\build 2>nul
pushd tests\CsharpToC.Roundtrip.Tests\Native\build
cmake .. -A x64 -DCYCLONE_INSTALL_DIR=D:/Work/FastCycloneDdsCsharpBindings/cyclone-compiled
if %errorlevel% neq 0 exit /b %errorlevel%
cmake --build . --config Release
if %errorlevel% neq 0 exit /b %errorlevel%
popd

echo [2/3] Building C# Project...
dotnet build tests\CsharpToC.Roundtrip.Tests\CsharpToC.Roundtrip.Tests.csproj -c Release
if %errorlevel% neq 0 exit /b %errorlevel%

echo [3/3] Deploying Native DLL...
copy /Y tests\CsharpToC.Roundtrip.Tests\Native\build\Release\CsharpToC_Roundtrip_Native.dll tests\CsharpToC.Roundtrip.Tests\bin\Release\net8.0\
copy /Y cyclone-compiled\bin\ddsc.dll tests\CsharpToC.Roundtrip.Tests\bin\Release\net8.0\

echo Build Complete.
