$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output = Join-Path $root 'KuzushiBeacon/bin/Release'
$dist = Join-Path $root 'dist'
[IO.Directory]::CreateDirectory($dist) | Out-Null
$manifest = [IO.File]::ReadAllText((Join-Path $output 'KuzushiBeacon.json'), [Text.Encoding]::UTF8) | ConvertFrom-Json
if ($manifest.InternalName -ne 'KuzushiBeacon' -or $manifest.Author -ne 'Roxyz0501' -or
    $manifest.RepoUrl -ne 'https://github.com/Roxyz0501/KuzushiBeacon' -or
    $manifest.IconUrl -ne 'https://raw.githubusercontent.com/Roxyz0501/KuzushiBeacon/main/KuzushiBeacon/images/icon.png') {
    throw 'Invalid package identity or URLs'
}
$zipPath = Join-Path $dist ('KuzushiBeacon-' + $manifest.AssemblyVersion + '.zip')
$allowlist = @('KuzushiBeacon.dll', 'KuzushiBeacon.deps.json', 'KuzushiBeacon.json', 'LICENSE', 'NOTICE', 'Ledger-LICENSE.txt', 'PvpStats-LICENSE.txt', 'images/icon.png')
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
$stream = [IO.File]::Open($zipPath, [IO.FileMode]::CreateNew)
$archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($entry in $allowlist) {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, (Join-Path $output $entry), $entry, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $archive.Dispose(); $stream.Dispose() }
$archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $actual = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\\', '/') } | Sort-Object)
    $expected = @($allowlist | Sort-Object)
    if (Compare-Object $expected $actual) { throw 'Package entries do not match allowlist' }
} finally { $archive.Dispose() }
Get-FileHash -LiteralPath $zipPath -Algorithm SHA256 | Select-Object Hash, Path
