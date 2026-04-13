powershell -f .\build\pack.ps1
IF ERRORLEVEL 1 exit /b %ERRORLEVEL%

dotnet tool update --global CycloneDDS.NET.DdsMonitor --add-source artifacts\nuget --version "*-*"
