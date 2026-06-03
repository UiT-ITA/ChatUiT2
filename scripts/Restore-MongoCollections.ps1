<#
.SYNOPSIS
    Restores MongoDB collections from a mongodump backup, recreating them with the
    correct Cosmos DB shard key.

.DESCRIPTION
    For each collection this script (when -Drop is set):
      1. Drops the existing collection.
      2. Recreates it SHARDED on -ShardKey (hashed) via shardCollection.
      3. Restores the documents into the freshly created sharded collection.

    Step 2 is essential on Azure Cosmos DB for MongoDB: mongodump/mongorestore do
    not capture or recreate the shard key, so a plain restore leaves the collection
    UNSHARDED and every write then fails with
    "PartitionKey extracted from document doesn't match the one specified in the header".

    DESTRUCTIVE when -Drop is used. Always run Backup-MongoCollections.ps1 first.

.EXAMPLE
    .\Restore-MongoCollections.ps1 -DumpDir ..\backups\mongo-20260602-141716 -Drop
#>
[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
param(
    [Parameter(Mandatory)][string]$DumpDir,
    [string]$ConnectionString,
    [string]$Database = 'Users',
    [string[]]$Collections = @('ChatMessages', 'Files'),
    [string]$ShardKey = 'Username',
    # Throttle the restore to avoid Cosmos RU rate-limiting (error 16500) on large
    # documents: insert as a single steady stream in small batches rather than
    # parallel bursts. Raise these if you also raise the collection's RU/s.
    [int]$InsertionWorkers = 1,
    # 1 = write a single document per insert request (slowest, gentlest on RU; best
    # for large image docs against limited throughput). Raise it if you raise RU/s.
    [int]$BatchSize = 1,
    # Max resilient re-restore passes per collection (each pass retries throttled docs).
    [int]$MaxPasses = 12,
    [switch]$Drop
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')

if (-not (Test-Path $DumpDir)) { throw "Dump folder not found: $DumpDir" }

$cs = Get-MongoConnectionString -ConnectionString $ConnectionString
$mongorestore = Resolve-MongoTool -Name 'mongorestore.exe'
$mongosh = Resolve-MongoTool -Name 'mongosh.exe'
$target = Get-MongoHostForDisplay $cs

foreach ($col in $Collections) {
    $bson = Join-Path $DumpDir "$Database\$col.bson"
    if (-not (Test-Path $bson)) { throw "Backup file not found for '$col': $bson" }

    if (-not $PSCmdlet.ShouldProcess("$target $Database.$col", "DROP, shard on '$ShardKey', restore")) { continue }

    if ($Drop) {
        Write-Host "Dropping $Database.$col ..." -ForegroundColor Yellow
        $dropJs = "print('dropped=' + db.getSiblingDB('$Database').getCollection('$col').drop());"
        & $mongosh "$cs" --quiet --eval $dropJs
        if ($LASTEXITCODE -ne 0) { throw "drop failed for $col (exit $LASTEXITCODE)." }
    }

    # Recreate sharded BEFORE loading data so it inherits the shard key (and the
    # 16 MB capability, since it's created now).
    Write-Host "Sharding $Database.$col on '$ShardKey' ..." -ForegroundColor Cyan
    Set-MongoShardKey -ConnectionString $cs -Database $Database -Collection $col -ShardKey $ShardKey

    # Load documents into the existing sharded collection (no --drop: don't recreate it).
    # Large image-bearing docs (~1 MB) can exhaust the collection's RU/s and trip
    # throttling (error 16500). mongorestore does not retry those, so we re-run it
    # (idempotent: already-present docs just hit duplicate-key and are skipped) until
    # the document count stops growing == everything is in. mongorestore continues
    # past errors by default; we deliberately do NOT pass --stopOnError (that would halt).
    # Throughput is the real lever: raising the database/collection RU/s finishes it faster.
    $prevCount = -1
    $finalCount = 0
    for ($pass = 1; $pass -le $MaxPasses; $pass++) {
        Write-Host "Restoring $Database.$col (pass $pass/$MaxPasses, workers=$InsertionWorkers, batchSize=$BatchSize) ..." -ForegroundColor Cyan
        # mongorestore logs progress to stderr and exits non-zero when some docs are
        # throttled. Neither is fatal here, so relax ErrorActionPreference for this call
        # only (otherwise PS turns the first stderr line / the non-zero exit into a throw
        # and aborts before anything is inserted). Output streams straight to the console.
        $eapSaved = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            & $mongorestore --uri $cs --db $Database --collection $col `
                --numInsertionWorkersPerCollection $InsertionWorkers --batchSize $BatchSize $bson
        }
        finally {
            $ErrorActionPreference = $eapSaved
        }

        $finalCount = Get-MongoCount -ConnectionString $cs -Database $Database -Collection $col
        Write-Host "  -> $Database.$col now holds $finalCount documents" -ForegroundColor DarkCyan

        if ($finalCount -eq $prevCount) {
            Write-Host "  Count stopped increasing at pass $pass (no more documents being added)." -ForegroundColor Yellow
            break
        }
        $prevCount = $finalCount
    }

    Write-Host "Done: $Database.$col holds $finalCount documents.`n" -ForegroundColor Green
}

Write-Host "Restore complete." -ForegroundColor Green
