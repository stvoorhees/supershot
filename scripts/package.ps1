param(
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version = '0.2.1',
    [string]$OutputDirectory = 'artifacts'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Push-Location (Join-Path $PSScriptRoot '..')
try {
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'Tool restore failed.' }
    $publish = Join-Path $OutputDirectory 'publish'
    # Never package files left over from an older publish.
    if (Test-Path $publish) { Remove-Item -Recurse -Force $publish }
    dotnet publish src/Supershot -c Release -r win-x64 --self-contained true "-p:Version=$Version" -o $publish
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    dotnet tool run vpk -- pack --packId Supershot --packVersion $Version --packDir $publish --mainExe Supershot.exe --packTitle Supershot --packAuthors 'Spencer Voorhees' --icon assets/Supershot.ico --framework webview2 --runtime win-x64 --channel win --outputDir (Join-Path $OutputDirectory 'releases') --skip-updates
    if ($LASTEXITCODE -ne 0) { throw 'Packaging failed.' }
} finally { Pop-Location }
