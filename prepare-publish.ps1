$workspaceRoot = [System.IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\')
$releaseRoot = [System.IO.Path]::GetFullPath((Join-Path $workspaceRoot 'release')).TrimEnd('\')

if (-not [string]::Equals($releaseRoot, (Join-Path $workspaceRoot 'release'), [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'The release output path is outside the project folder.'
}

foreach ($name in @('backend', 'FreshCare')) {
    $target = [System.IO.Path]::GetFullPath((Join-Path $releaseRoot $name))
    if (-not $target.StartsWith($releaseRoot + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean an output outside the release folder: $name"
    }

    if (Test-Path -LiteralPath $target) {
        $item = Get-Item -LiteralPath $target -Force
        if ($item.PSIsContainer -ne $true -or ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Refusing to clean a non-directory or reparse-point output: $name"
        }

        [System.IO.Directory]::Delete($target, $true)
    }
}
