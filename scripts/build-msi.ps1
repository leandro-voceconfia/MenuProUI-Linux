param(
    [string]$Version
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectRoot "MenuProUI.csproj"
[xml]$project = Get-Content $projectFile
$projectVersion = $project.Project.PropertyGroup |
    ForEach-Object { $_.Version } |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    Select-Object -First 1

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = $projectVersion
}

if ($Version -match "^v") {
    $Version = $Version.Substring(1)
}

if ($Version -ne $projectVersion) {
    throw "Release version $Version does not match project version $projectVersion."
}

if ($Version -notmatch "^\d+\.\d+\.\d+(\.\d+)?$") {
    throw "Invalid MSI version: $Version"
}

if (-not (Get-Command wix -ErrorAction SilentlyContinue)) {
    throw "WiX Toolset 5.0.2 is required. Install it with: dotnet tool install --global wix --version 5.0.2"
}

$publishDir = Join-Path $projectRoot "publish/win-x64"
$sourceFile = Join-Path $projectRoot "installer/MenuProUI.wxs"
$outputFile = Join-Path $projectRoot "menupro-ui_${Version}_x64.msi"

Push-Location $projectRoot
try {
    dotnet publish $projectFile -c Release -r win-x64 --self-contained true `
        -o $publishDir -p:PublishSingleFile=false -p:PublishTrimmed=false
    if ($LASTEXITCODE -ne 0) {
        throw "Windows publish failed with exit code $LASTEXITCODE."
    }

    wix build $sourceFile -arch x64 `
        -d "Version=$Version" `
        -d "PublishDir=$publishDir" `
        -d "ProjectRoot=$projectRoot" `
        -out $outputFile `
        -pdbtype none
    if ($LASTEXITCODE -ne 0) {
        throw "WiX MSI build failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

if (-not (Test-Path $outputFile -PathType Leaf)) {
    throw "Expected MSI was not created: $outputFile"
}

Write-Output "MSI created: $outputFile"
