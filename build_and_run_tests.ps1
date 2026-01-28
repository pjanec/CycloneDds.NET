param(
    [string]$Filter = ""
)

$ErrorActionPreference = "Stop"

Write-Host "1. Building CodeGen (Release) - Crucial for applying SerializerEmitter changes..." -ForegroundColor Cyan
dotnet build tools\CycloneDDS.CodeGen\CycloneDDS.CodeGen.csproj -c Release
if ($LASTEXITCODE -ne 0) { Write-Error "CodeGen build failed"; exit 1 }

Write-Host "2. Cleaning Test Project..." -ForegroundColor Cyan
dotnet clean tests\CsharpToC.Roundtrip.Tests\CsharpToC.Roundtrip.Tests.csproj

Write-Host "3. Building Test Project (Release)..." -ForegroundColor Cyan
# This will trigger the CodeGen target using the updated Debug CodeGen.exe
dotnet build tests\CsharpToC.Roundtrip.Tests\CsharpToC.Roundtrip.Tests.csproj -c Release
if ($LASTEXITCODE -ne 0) { Write-Error "Test Project build failed"; exit 1 }

Write-Host "4. Rebuilding Native Lib (via bat script but only the native part if possible, or just run the bat)..." -ForegroundColor Cyan
# To be safe, run the bat, but we already built C# so it's redundant but safe.
.\rebuild_all_csharp_to_c.bat
if ($LASTEXITCODE -ne 0) { Write-Error "Bat script failed"; exit 1 }

Write-Host "5. Running CsharpToC Tests..." -ForegroundColor Cyan
if ([string]::IsNullOrWhiteSpace($Filter)) {
    dotnet test tests\CsharpToC.Roundtrip.Tests\CsharpToC.Roundtrip.Tests.csproj -c Release --logger "console;verbosity=normal"
}
else {
    Write-Host "Applying Filter: $Filter" -ForegroundColor Yellow
    dotnet test tests\CsharpToC.Roundtrip.Tests\CsharpToC.Roundtrip.Tests.csproj -c Release --logger "console;verbosity=normal" --filter $Filter
}

