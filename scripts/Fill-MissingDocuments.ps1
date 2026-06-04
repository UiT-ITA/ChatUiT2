<#
.SYNOPSIS
    Inserts only the MISSING documents of a collection from a mongodump backup.
    Does NOT drop, does NOT shard. Safe to run against a populated collection.

.DESCRIPTION
    Wraps FillMissingDocuments.cs (a .NET file-based app using MongoDB.Driver). It
    streams the dump once and inserts each document; already-present docs are skipped
    (duplicate _id), and throttled inserts (Cosmos 16500 / 429) are retried per-document
    with backoff instead of aborting. Ideal for finishing a restore on a serverless
    account where you cannot raise RU/s.

    Connection string resolution (same as the other scripts):
      -ConnectionString  >  scripts\.env  >  .NET user secrets

.EXAMPLE
    .\Fill-MissingDocuments.ps1 -DumpDir ..\backups\mongo-20260603-074357 -Collection Files

.EXAMPLE
    # validate the dump can be read, no DB connection:
    .\Fill-MissingDocuments.ps1 -DumpDir ..\backups\mongo-20260603-074357 -Collection Files -DryRun -Limit 5
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$DumpDir,
    [Parameter(Mandatory)][string]$Collection,
    [string]$ConnectionString,
    [string]$Database = 'Users',
    [int]$Progress = 200,
    [int]$MaxDelaySeconds = 60,
    [int]$Limit = 0,
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')

$bson = Join-Path $DumpDir "$Database\$Collection.bson"
if (-not (Test-Path $bson)) { throw "Backup file not found: $bson" }

$program = Join-Path $PSScriptRoot 'FillMissingDocuments.cs'
if (-not (Test-Path $program)) { throw "Missing $program" }

$dotnetArgs = @('run', $program, '--',
    '--bson', $bson,
    '--db', $Database,
    '--collection', $Collection,
    '--progress', $Progress,
    '--max-delay-seconds', $MaxDelaySeconds)
if ($Limit -gt 0) { $dotnetArgs += @('--limit', $Limit) }

try {
    if ($DryRun) {
        $dotnetArgs += '--dry-run'
        Write-Host "Dry run: reading $bson (no database connection)." -ForegroundColor Cyan
    }
    else {
        $cs = Get-MongoConnectionString -ConnectionString $ConnectionString
        $env:MONGO_FILL_URI = $cs   # passed to the child via env, not the command line
        Write-Host "Filling missing documents into $Database.$Collection on $(Get-MongoHostForDisplay $cs)" -ForegroundColor Cyan
        Write-Host "Source: $bson  (insert-missing only; no drop, no shard)`n" -ForegroundColor DarkCyan
    }

    & dotnet @dotnetArgs
    $code = $LASTEXITCODE
    if ($code -ne 0) { Write-Host "`nFinished with exit code $code (some documents failed - see messages above)." -ForegroundColor Yellow }
    else { Write-Host "`nFinished cleanly." -ForegroundColor Green }
}
finally {
    Remove-Item Env:\MONGO_FILL_URI -ErrorAction SilentlyContinue
}
