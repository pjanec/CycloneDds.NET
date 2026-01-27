$ErrorActionPreference = "Stop"

Write-Host "1. Building CodeGen (Debug) - Crucial for applying SerializerEmitter changes..." -ForegroundColor Cyan
dotnet build tools\CycloneDDS.CodeGen\CycloneDDS.CodeGen.csproj -c Debug
if ($LASTEXITCODE -ne 0) { Write-Error "CodeGen build failed"; exit 1 }

Write-Host "2. Cleaning Test Project..." -ForegroundColor Cyan
dotnet clean tests\CsharpToC.Roundtrip.Tests\CsharpToC.Roundtrip.Tests.csproj

Write-Host "3. Building Test Project Debug)..." -ForegroundColor Cyan
# This will trigger the CodeGen target using the updated Debug CodeGen.exe
dotnet build tests\CsharpToC.Roundtrip.Tests\CsharpToC.Roundtrip.Tests.csproj -c Debug
if ($LASTEXITCODE -ne 0) { Write-Error "Test Project build failed"; exit 1 }

Write-Host "4. Rebuilding Native Lib (via bat script but only the native part if possible, or just run the bat)..." -ForegroundColor Cyan
# To be safe, run the bat, but we already built C# so it's redundant but safe.
.\rebuild_all_csharp_to_c.bat
if ($LASTEXITCODE -ne 0) { Write-Error "Bat script failed"; exit 1 }

Write-Host "5. Running CsharpToC Tests..." -ForegroundColor Cyan
dotnet test tests\CsharpToC.Roundtrip.Tests\CsharpToC.Roundtrip.Tests.csproj -c Debug --logger "console;verbosity=normal"

