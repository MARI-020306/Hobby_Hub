[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$config=Get-Content (Join-Path $root '.local/visual-test-database.json') -Raw | ConvertFrom-Json
if($config.Database -notmatch '^HobbyHub_Development_Visual_20261008_[a-f0-9]{8}$'){throw 'Invalid visual test database.'}
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:DOTNET_ENVIRONMENT='Development'
$env:DevelopmentDatabase__ConnectionString=$config.Connection
$env:VisualTesting__Enabled='true'
$env:Security__DataProtection__KeyPath=Join-Path $root '.local/visual-keys'
# Build separately first, while this review server is stopped. Never writes normal bin files.
$assembly=Join-Path $root '.local/visual-build/bin/Proyecto_HobbyHub/debug/Proyecto_HobbyHub.dll'
if(!(Test-Path -LiteralPath $assembly)){throw 'Run dotnet build Proyecto_HobbyHub.sln --artifacts-path .local/visual-build first.'}
& dotnet $assembly --contentRoot (Join-Path $root 'Proyecto_HobbyHub') --urls http://127.0.0.1:5088 --Logging:LogLevel:Default Warning
