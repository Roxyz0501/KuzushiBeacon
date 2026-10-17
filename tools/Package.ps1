$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output = Join-Path $root 'KuzushiBeacon/bin/Release'
$dist = Join-Path $root 'dist'
[IO.Directory]::CreateDirectory($dist) | Out-Null
$manifest = Get-Content -Raw (Join-Path $output 'KuzushiBeacon.json') | ConvertFrom-Json
if ($manifest.InternalName -ne 'KuzushiBeacon' -or $manifest.Author -ne 'Roxyz0501' -or
    $manifest.RepoUrl -ne 'https://github.com/Roxyz0501/KuzushiBeacon' -or
    $manifest.IconUrl -ne 'https://raw.githubusercontent.com/Roxyz0501/KuzushiBeacon/main/KuzushiBeacon/images/icon.png') {
    throw 'Invalid package identity or URLs'
}
$zipPath = Join-Path $dist ('KuzushiBeacon-' + $manifest.AssemblyVersion + '.zip')
$stream = [IO.File]::Open($zipPath, [IO.FileMode]::Create)
$zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($entry in @('KuzushiBeacon.dll', 'KuzushiBeacon.deps.json', 'KuzushiBeacon.json', 'LICENSE', 'NOTICE', 'images/icon.png')) {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, (Join-Path $output $entry), $entry, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $zip.Dispose(); $stream.Dispose() }
Get-FileHash -LiteralPath $zipPath -Algorithm SHA256 | Select-Object Hash, Path
