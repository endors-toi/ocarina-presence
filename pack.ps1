$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$project = "$root\src\OcarinaPresence\OcarinaPresence.csproj"
$version = ([xml](Get-Content $project)).Project.PropertyGroup.Version
$stage = "$root\release\stage\OcarinaPresence"
$zip = "$root\release\OcarinaPresence-v$version.zip"

if (Test-Path "$root\release\stage") { Remove-Item "$root\release\stage" -Recurse -Force }
dotnet publish $project -c Release -o $stage
if ($LASTEXITCODE -ne 0) { throw "publish failed" }
Copy-Item "$root\README.md" $stage
Compress-Archive -Path $stage -DestinationPath $zip -Force
Get-Item $zip
