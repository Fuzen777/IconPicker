# Builds the built-in icons asset from Microsoft's Fluent UI System Icons (MIT):
#   src\IconPicker.App\Assets\FluentIcons.zip          index.json (ids, names, descriptions, keywords, styles) and icons\<id>_<style>.svg
#   src\IconPicker.App\Assets\FluentIcons-LICENSE.txt  the icons' license
# The SVGs come from the npm package @fluentui/svg-icons; the names, descriptions and keywords from the metadata.json files of
# github.com/microsoft/fluentui-system-icons, fetched with a shallow, blobless, sparse clone so only those small files and the license are downloaded.
# Each icon keeps its largest size, in its regular and filled styles. Run it again to update; the output is committed.
#   -Version   the npm package version (default 1.1.344)
param(
    [string]$Version = "1.1.344"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem

$assetsFolder = Join-Path $PSScriptRoot "..\src\IconPicker.App\Assets"
$workFolder = Join-Path ([IO.Path]::GetTempPath()) ("fluent-icons-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $workFolder | Out-Null
try {
    # 1. The SVGs.
    $tarball = Join-Path $workFolder "svg-icons.tgz"
    Write-Host "Downloading @fluentui/svg-icons $Version..."
    Invoke-WebRequest -UseBasicParsing -Uri "https://registry.npmjs.org/@fluentui/svg-icons/-/svg-icons-$Version.tgz" -OutFile $tarball
    $packageFolder = Join-Path $workFolder "npm"
    New-Item -ItemType Directory -Path $packageFolder | Out-Null
    # Windows' own tar: Git's, when it comes first on the path, reads "C:" as a remote host.
    & (Join-Path $env:SystemRoot "System32\tar.exe") -xzf $tarball -C $packageFolder
    if ($LASTEXITCODE -ne 0) { throw "tar failed to extract the package." }
    $iconsFolder = Join-Path $packageFolder "package\icons"

    # 2. The names, descriptions and keywords.
    $repositoryFolder = Join-Path $workFolder "repository"
    Write-Host "Fetching the icons' metadata..."
    git clone --quiet --depth 1 --filter=blob:none --sparse https://github.com/microsoft/fluentui-system-icons.git $repositoryFolder
    if ($LASTEXITCODE -ne 0) { throw "git clone failed." }
    git -C $repositoryFolder sparse-checkout set --no-cone "/LICENSE" "/assets/*/metadata.json"
    if ($LASTEXITCODE -ne 0) { throw "git sparse-checkout failed." }
    $metadataById = @{}
    foreach ($metadataFile in Get-ChildItem -LiteralPath (Join-Path $repositoryFolder "assets") -Filter "metadata.json" -Recurse -File) {
        $metadata = Get-Content -LiteralPath $metadataFile.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
        if (-not $metadata.name) { continue }
        $id = ($metadata.name.ToLowerInvariant() -replace "[^a-z0-9]+", "_").Trim("_")
        $metadataById[$id] = $metadata
    }

    # 3. The largest size of each icon's regular and filled styles.
    $largest = @{}
    foreach ($svgFile in Get-ChildItem -LiteralPath $iconsFolder -Filter "*.svg" -File) {
        if ($svgFile.Name -notmatch "^(?<id>.+)_(?<size>\d+)_(?<style>regular|filled)\.svg$") { continue }
        $key = "$($Matches.id)|$($Matches.style)"
        $size = [int]$Matches.size
        if (-not $largest.ContainsKey($key) -or $largest[$key].Size -lt $size) {
            $largest[$key] = [pscustomobject]@{ Id = $Matches.id; Style = $Matches.style; Size = $size; File = $svgFile.FullName }
        }
    }

    $stagingFolder = Join-Path $workFolder "staging"
    New-Item -ItemType Directory -Path (Join-Path $stagingFolder "icons") | Out-Null
    $icons = foreach ($group in $largest.Values | Group-Object Id | Sort-Object Name) {
        $id = $group.Name
        # Typed lists stay JSON arrays: PowerShell flattens a one-item array to its item and writes an empty one as {}.
        $styles = [System.Collections.Generic.List[string]]::new()
        foreach ($styleFile in $group.Group | Sort-Object { if ($_.Style -eq "regular") { 0 } else { 1 } }) {
            Copy-Item -LiteralPath $styleFile.File -Destination (Join-Path $stagingFolder "icons\$($id)_$($styleFile.Style).svg")
            $styles.Add($styleFile.Style)
        }
        $metadata = $metadataById[$id]
        $name = if ($metadata) { [string]$metadata.name } else { (Get-Culture).TextInfo.ToTitleCase(($id -replace "_", " ")) }
        $description = if ($metadata -and $metadata.description) { [string]$metadata.description } else { "" }
        $keywords = [System.Collections.Generic.List[string]]::new()
        if ($metadata -and $metadata.metaphor) {
            foreach ($keyword in @($metadata.metaphor)) {
                if ($keyword) { $keywords.Add([string]$keyword) }
            }
        }
        [ordered]@{ id = $id; name = $name; description = $description; keywords = $keywords; styles = $styles }
    }
    $withMetadata = @($icons | Where-Object { $metadataById.ContainsKey($_.id) }).Count
    $indexJson = ConvertTo-Json -InputObject ([ordered]@{ icons = @($icons) }) -Depth 4 -Compress
    [IO.File]::WriteAllText((Join-Path $stagingFolder "index.json"), $indexJson, (New-Object Text.UTF8Encoding $false))

    # 4. The asset and its license.
    $zipPath = Join-Path $assetsFolder "FluentIcons.zip"
    if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath }
    # Entries are added one by one: Windows PowerShell's CreateFromDirectory names them with backslashes, which zip readers don't treat as folders.
    $archive = [IO.Compression.ZipFile]::Open($zipPath, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($stagedFile in Get-ChildItem -LiteralPath $stagingFolder -Recurse -File | Sort-Object FullName) {
            $entryName = $stagedFile.FullName.Substring($stagingFolder.Length + 1).Replace("\", "/")
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $stagedFile.FullName, $entryName, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    }
    finally {
        $archive.Dispose()
    }
    # The npm package carries no license file; the repository's is the same MIT license.
    Copy-Item -LiteralPath (Join-Path $repositoryFolder "LICENSE") -Destination (Join-Path $assetsFolder "FluentIcons-LICENSE.txt")
    $zipSize = [math]::Round((Get-Item -LiteralPath $zipPath).Length / 1MB, 1)
    Write-Host "Built $($zipPath): $(@($icons).Count) icons ($withMetadata with Microsoft's names and keywords), $zipSize MB."
}
finally {
    # The clone holds read-only pack files.
    Get-ChildItem -LiteralPath $workFolder -Recurse -Force -File | ForEach-Object { $_.Attributes = "Normal" }
    Remove-Item -LiteralPath $workFolder -Recurse -Force
}
