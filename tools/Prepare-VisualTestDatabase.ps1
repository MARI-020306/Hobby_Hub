[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$instance='(localdb)\HobbyHubPhase1C_20261008'
$database='HobbyHub_Development_Visual_20261008_'+[guid]::NewGuid().ToString('N').Substring(0,8)
$root=Split-Path $PSScriptRoot -Parent
$folder=Join-Path $root ('.local\visual-sql\'+$database)
New-Item -ItemType Directory -Path $folder -Force | Out-Null
# No access to application tables of the existing development database. No reuse or drop.
& sqlcmd -S $instance -E -b -d master -Q "IF DB_ID(N'$database') IS NOT NULL THROW 51000, 'Visual test database already exists. Nothing will be overwritten.', 1; CREATE DATABASE [$database];"
if($LASTEXITCODE -ne 0){throw 'Creation failed; no scripts will run.'}
$order=@('001_EmptyDevelopmentBaseline.sql','002_ExistingFeatureExtensions.sql','003_DevelopmentRoles.sql','005_AccountSessionsSecurity.sql','006_RecoveryAndAudit.sql','007_SocialModules.sql','008_ModerationConcurrency.sql','009_PersonalBlocksAndMessageReports.sql','010_AuditCorrelation.sql')
foreach($script in $order){
 $source=Get-Content -Raw -Encoding utf8 (Join-Path $root ('Proyecto_HobbyHub\Database\Scripts\'+$script))
 # Keep each script's exact-name guard, including 010, scoped to this new visual database.
 $source=$source.Replace('HobbyHub_Development_Phase1C_20261008',$database)
 $destination=Join-Path $folder $script
 [IO.File]::WriteAllText($destination,$source,[Text.UTF8Encoding]::new($false))
 & sqlcmd -S $instance -E -b -d $database -i $destination
 if($LASTEXITCODE -ne 0){throw ('Stopped at '+$script+'. Prior scripts remain applied only to the new test database.')}
}
$connection="Server=$instance;Database=$database;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=5"
@{Database=$database;Connection=$connection} | ConvertTo-Json | Set-Content -Encoding utf8 (Join-Path $root '.local\visual-test-database.json')
Write-Output ('New independent visual test database prepared: '+$database)
