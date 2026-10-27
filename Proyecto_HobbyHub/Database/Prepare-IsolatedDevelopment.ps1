# Executed with authorization on 2026-10-08. Do not rerun against an existing instance.
# Creates a NEW dedicated instance; refuses to reuse any instance visible to the current user.
[CmdletBinding()]
param([switch]$Approved)
$ErrorActionPreference = 'Stop'
if (-not $Approved) { throw 'Explicit approval is required. This script has not been authorized for execution.' }
$instanceName = 'HobbyHubPhase1C_20261008'
$databaseName = 'HobbyHub_Development_Phase1C_20261008'
$null = Get-Command SqlLocalDB -ErrorAction Stop
$null = Get-Command sqlcmd -ErrorAction Stop
$instances = @(& SqlLocalDB info)
if ($LASTEXITCODE -ne 0) { throw 'Cannot inventory LocalDB instances. No instance will be created.' }
if ($instances | Where-Object { $_.Trim() -eq $instanceName }) { throw 'The reserved instance already exists. Stop and choose a newly reviewed name; do not reuse it.' }
& SqlLocalDB create $instanceName 15.0
if ($LASTEXITCODE -ne 0) { throw 'LocalDB instance creation failed. Stop without changing another instance.' }
$createdInstances = @(& SqlLocalDB info)
if ($LASTEXITCODE -ne 0 -or -not ($createdInstances | Where-Object { $_.Trim() -eq $instanceName })) {
    throw 'LocalDB did not confirm the newly created instance. No SQL will be executed.'
}
& SqlLocalDB start $instanceName
if ($LASTEXITCODE -ne 0) { throw 'Dedicated instance startup failed.' }
$serverName = '(localdb)\' + $instanceName
# CREATE DATABASE fails if a database of this name already exists; no replacement/drop.
& sqlcmd -S $serverName -E -b -Q 'CREATE DATABASE [HobbyHub_Development_Phase1C_20261008];'
if ($LASTEXITCODE -ne 0) { throw 'Database creation failed. No scripts will be applied.' }
$scriptOrder = @('001_EmptyDevelopmentBaseline.sql', '002_ExistingFeatureExtensions.sql', '003_DevelopmentRoles.sql',
    '005_AccountSessionsSecurity.sql', '006_RecoveryAndAudit.sql', '007_SocialModules.sql',
    '008_ModerationConcurrency.sql', '009_PersonalBlocksAndMessageReports.sql', '004_ReadOnlySchemaInventory.sql')
foreach ($scriptName in $scriptOrder) {
    $scriptPath = Join-Path $PSScriptRoot ('Scripts\' + $scriptName)
    & sqlcmd -S $serverName -E -b -d $databaseName -i $scriptPath
    if ($LASTEXITCODE -ne 0) { throw ('Schema preparation stopped at ' + $scriptName + '. Earlier successful scripts remain committed; review manually.') }
}
# No automatic cleanup, deletion, restart of existing services or seed users/passwords.
