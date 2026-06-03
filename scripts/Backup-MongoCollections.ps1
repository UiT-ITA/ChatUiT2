<#
.SYNOPSIS
    Backs up MongoDB collections to a timestamped folder using mongodump.

.DESCRIPTION
    Read-only. Dumps the given collections (default: ChatMessages, Files in the
    Users database) so they can be restored later. Connection string is taken
    from the app's .NET user secrets unless -ConnectionString is provided.

.EXAMPLE
    .\Backup-MongoCollections.ps1
    Backs up ChatMessages and Files to ..\backups\mongo-<timestamp>.

.OUTPUTS
    The full path to the backup folder (last line on the pipeline).
#>
[CmdletBinding()]
param(
    [string]$ConnectionString,
    [string]$Database = 'Users',
    [string[]]$Collections = @('ChatMessages', 'Files'),
    [string]$OutDir
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')

$cs = Get-MongoConnectionString -ConnectionString $ConnectionString
$mongodump = Resolve-MongoTool -Name 'mongodump.exe'

if (-not $OutDir) {
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $OutDir = Join-Path (Split-Path $PSScriptRoot -Parent) "backups\mongo-$stamp"
}
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

Write-Host "Backing up [$($Collections -join ', ')] from '$Database' on $(Get-MongoHostForDisplay $cs)" -ForegroundColor Cyan
Write-Host "Destination: $OutDir`n"

foreach ($col in $Collections) {
    & $mongodump --uri $cs --db $Database --collection $col --out $OutDir
    if ($LASTEXITCODE -ne 0) { throw "mongodump failed for collection '$col' (exit $LASTEXITCODE)." }
}

Write-Host "`nBackup contents:" -ForegroundColor Green
Get-ChildItem -Recurse (Join-Path $OutDir $Database) |
    Select-Object Name, @{ N = 'SizeKB'; E = { [math]::Round($_.Length / 1KB, 1) } } |
    Format-Table -AutoSize | Out-Host

Write-Host "Backup complete." -ForegroundColor Green
Write-Output $OutDir
