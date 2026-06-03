<#
.SYNOPSIS
    One-shot migration so collections pick up the 16 MB document limit.

.DESCRIPTION
    Azure Cosmos DB for MongoDB only applies the EnableMongo16MBDocumentSupport
    capability to collections created AFTER the feature is enabled. Existing
    collections keep the old 2 MB limit. This script backs the collections up,
    then drops and recreates them (via mongorestore --drop) so they inherit the
    16 MB limit, with the data restored.

    Prerequisite: the 16 MB feature must already be enabled on the Cosmos account.

.EXAMPLE
    .\Migrate-CollectionsTo16MB.ps1
    Prompts for confirmation before the destructive step.

.EXAMPLE
    .\Migrate-CollectionsTo16MB.ps1 -Force
    Skips the confirmation prompt.
#>
[CmdletBinding()]
param(
    [string]$ConnectionString,
    [string]$Database = 'Users',
    [string[]]$Collections = @('ChatMessages', 'Files'),
    [string]$ShardKey = 'Username',
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')

# 1. Back up (read-only)
$dumpDir = & (Join-Path $PSScriptRoot 'Backup-MongoCollections.ps1') `
    -ConnectionString $ConnectionString -Database $Database -Collections $Collections |
    Select-Object -Last 1

if (-not $dumpDir -or -not (Test-Path $dumpDir)) {
    throw "Backup did not produce a valid folder; aborting before the destructive step."
}

# 2. Confirm the destructive step
if (-not $Force) {
    Write-Host "`nAbout to DROP, re-shard on '$ShardKey', and restore [$($Collections -join ', ')] in '$Database'." -ForegroundColor Yellow
    Write-Host "Backup is at: $dumpDir" -ForegroundColor Yellow
    $answer = Read-Host "Type 'yes' to continue"
    if ($answer -ne 'yes') {
        Write-Host "Aborted. Nothing was changed. Backup kept at $dumpDir" -ForegroundColor Red
        return
    }
}

# 3. Drop + re-shard + restore so the collections are recreated, sharded correctly,
#    under the 16 MB capability.
& (Join-Path $PSScriptRoot 'Restore-MongoCollections.ps1') `
    -ConnectionString $ConnectionString -DumpDir $dumpDir `
    -Database $Database -Collections $Collections -ShardKey $ShardKey -Drop -Confirm:$false

Write-Host "`nMigration done. The collections were recreated and should now accept documents up to 16 MB." -ForegroundColor Green
Write-Host "Backup retained at: $dumpDir"
