<#
.SYNOPSIS
    Builds the release files into dist\: VaporwaveToons.exe and VaporwaveToons-<version>-win.zip.

.DESCRIPTION
    Compiles the Release build, runs the theme self-test, and zips the program together with
    README.txt, LICENSE.txt and THIRD-PARTY-NOTICES.txt. Works in Windows PowerShell 5.1 and
    PowerShell 7. Run from anywhere:
        pwsh tools/package.ps1
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'VaporwaveToons.csproj'
$dist = Join-Path $root 'dist'

dotnet build $project -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

$exe = Join-Path $root 'bin\Release\net48\VaporwaveToons.exe'
$version = ([xml](Get-Content $project -Raw)).Project.PropertyGroup.Version |
    Where-Object { $_ } | Select-Object -First 1

if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
New-Item -ItemType Directory -Path $dist | Out-Null

# A GUI program: wait for it explicitly, then read what it logged.
$log = Join-Path $dist 'selftest.log'
$test = Start-Process -FilePath $exe -ArgumentList '--selftest', '--log', "`"$log`"" -Wait -PassThru
Get-Content $log | ForEach-Object { $_ -replace '^\[[^\]]+\] ', '' }
if ($test.ExitCode -ne 0) { throw "Self-test failed (exit code $($test.ExitCode))." }
Remove-Item $log

$name = "VaporwaveToons-$version"
$files = [ordered]@{
    'VaporwaveToons.exe'      = $exe
    'README.txt'              = Join-Path $root 'packaging\README.txt'
    'LICENSE.txt'             = Join-Path $root 'LICENSE'
    'THIRD-PARTY-NOTICES.txt' = Join-Path $root 'THIRD-PARTY-NOTICES.md'
}
# Entries are named explicitly: Compress-Archive on PowerShell 5.1 writes backslashes into them.
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::Open((Join-Path $dist "$name-win.zip"), 'Create')
try {
    foreach ($f in $files.GetEnumerator()) {
        [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $f.Value, "$name/$($f.Key)", 'Optimal')
    }
}
finally {
    $zip.Dispose()
}
Copy-Item $exe $dist

Get-ChildItem $dist | ForEach-Object {
    $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    '{0,-34} {1,10:N0} bytes   sha256 {2}' -f $_.Name, $_.Length, $hash
}
