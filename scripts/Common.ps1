# Shared helpers for the MongoDB maintenance scripts.
# Dot-source this file from the other scripts: . (Join-Path $PSScriptRoot 'Common.ps1')

Set-StrictMode -Version Latest

# Resolve a MongoDB CLI tool (mongodump.exe / mongorestore.exe / mongosh.exe).
# Falls back to the default winget install locations if it isn't on PATH yet
# (a freshly installed tool isn't on PATH until the shell is reopened).
function Resolve-MongoTool {
    param([Parameter(Mandatory)][string]$Name)

    $cmd = Get-Command $Name -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    $candidates = @(
        "C:\Program Files\MongoDB\Tools\*\bin\$Name",
        "C:\Program Files\mongosh\$Name",
        "C:\Program Files\MongoDB\mongosh\$Name",
        "$env:LOCALAPPDATA\Programs\mongosh\$Name",
        "$env:LOCALAPPDATA\Microsoft\WinGet\Links\$Name"
    )
    foreach ($pattern in $candidates) {
        $found = Get-ChildItem $pattern -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($found) { return $found.FullName }
    }

    throw "Could not find '$Name'. Install the tools with:`n" +
          "  winget install -e --id MongoDB.DatabaseTools`n" +
          "  winget install -e --id MongoDB.Shell"
}

# Parse a simple KEY=VALUE .env file into a hashtable (ignores blanks/# comments,
# strips matching surrounding quotes).
function Read-EnvFile {
    param([Parameter(Mandatory)][string]$Path)
    $map = @{}
    foreach ($line in Get-Content -Path $Path) {
        $trimmed = $line.Trim()
        if (-not $trimmed -or $trimmed.StartsWith('#')) { continue }
        $idx = $trimmed.IndexOf('=')
        if ($idx -lt 1) { continue }
        $key = $trimmed.Substring(0, $idx).Trim()
        $value = $trimmed.Substring($idx + 1).Trim()
        if ($value.Length -ge 2 -and
            (($value[0] -eq '"' -and $value[-1] -eq '"') -or ($value[0] -eq "'" -and $value[-1] -eq "'"))) {
            $value = $value.Substring(1, $value.Length - 2)
        }
        $map[$key] = $value
    }
    return $map
}

# Resolve the MongoDB connection string.
# Precedence:
#   1. explicit -ConnectionString
#   2. a .env file (default: scripts\.env)  <- drop one in to target another DB (e.g. prod)
#   3. the app's .NET user secrets (ConnectionStrings:MongoDb)
function Get-MongoConnectionString {
    param(
        [string]$ConnectionString,
        [string]$EnvFile
    )

    # 1. Explicit argument wins.
    if ($ConnectionString) { return $ConnectionString }

    # 2. Optional .env file in the scripts folder.
    if (-not $EnvFile) { $EnvFile = Join-Path $PSScriptRoot '.env' }
    if (Test-Path $EnvFile) {
        $envMap = Read-EnvFile -Path $EnvFile
        foreach ($key in @('ConnectionStrings__MongoDb', 'MongoDb', 'MONGODB_CONNECTION_STRING', 'MONGODB_URI', 'ConnectionString')) {
            if ($envMap.ContainsKey($key) -and $envMap[$key]) {
                Write-Host "Using connection string from $EnvFile ($key)." -ForegroundColor DarkGray
                return $envMap[$key]
            }
        }
        # Only error if the file actually has settings but none we recognize.
        # An empty/comment-only .env is treated as "no override" and falls through.
        if ($envMap.Count -gt 0) {
            throw "Env file '$EnvFile' found but no recognized key. Use one of: " +
                  "ConnectionStrings__MongoDb, MongoDb, MONGODB_CONNECTION_STRING, MONGODB_URI, ConnectionString."
        }
    }

    # 3. Fall back to the app's .NET user secrets.
    $repoRoot = Split-Path $PSScriptRoot -Parent
    $csproj = Join-Path $repoRoot 'ChatUiT2\ChatUiT2.csproj'
    if (-not (Test-Path $csproj)) { throw "Could not find $csproj to read UserSecretsId." }

    [xml]$xml = Get-Content $csproj
    $id = ($xml.Project.PropertyGroup.UserSecretsId | Where-Object { $_ } | Select-Object -First 1)
    if (-not $id) { throw "UserSecretsId not found in $csproj." }

    $secretsPath = Join-Path $env:APPDATA "Microsoft\UserSecrets\$id\secrets.json"
    if (-not (Test-Path $secretsPath)) { throw "User secrets file not found: $secretsPath" }

    $json = Get-Content $secretsPath -Raw | ConvertFrom-Json
    $cs = $null
    if ($json.PSObject.Properties.Name -contains 'ConnectionStrings') { $cs = $json.ConnectionStrings.MongoDb }
    if (-not $cs -and ($json.PSObject.Properties.Name -contains 'ConnectionStrings:MongoDb')) { $cs = $json.'ConnectionStrings:MongoDb' }
    if (-not $cs) { throw "ConnectionStrings:MongoDb not found in user secrets ($secretsPath)." }

    return $cs
}

# Print a connection target without leaking credentials (host only).
function Get-MongoHostForDisplay {
    param([Parameter(Mandatory)][string]$ConnectionString)
    return (($ConnectionString -replace '^mongodb(\+srv)?://', '') -replace '^[^@]*@', '') -replace '[/?].*$', ''
}

# Count documents in a collection (used to detect when a resilient restore is complete).
function Get-MongoCount {
    param(
        [Parameter(Mandatory)][string]$ConnectionString,
        [Parameter(Mandatory)][string]$Database,
        [Parameter(Mandatory)][string]$Collection
    )
    $mongosh = Resolve-MongoTool -Name 'mongosh.exe'
    $js = "print(db.getSiblingDB('$Database').getCollection('$Collection').countDocuments({}))"
    $out = & $mongosh "$ConnectionString" --quiet --eval $js
    if ($LASTEXITCODE -ne 0) { throw "countDocuments failed for $Database.$Collection (exit $LASTEXITCODE)." }
    return [int](($out | Where-Object { $_ -match '^\d+$' } | Select-Object -Last 1))
}

# Create a Cosmos DB for MongoDB collection sharded on $ShardKey (hashed).
# mongodump/mongorestore do NOT capture or recreate the Cosmos shard key, so a
# restored collection comes back UNSHARDED and writes fail with
# "PartitionKey extracted from document doesn't match the one specified in the header".
# Sharding the collection up front (it's created by this command) avoids that.
function Set-MongoShardKey {
    param(
        [Parameter(Mandatory)][string]$ConnectionString,
        [Parameter(Mandatory)][string]$Database,
        [Parameter(Mandatory)][string]$Collection,
        [Parameter(Mandatory)][string]$ShardKey
    )
    $mongosh = Resolve-MongoTool -Name 'mongosh.exe'
    $ns = "$Database.$Collection"
    # shardCollection must run against the admin database on Cosmos DB for MongoDB.
    $js = "var r = db.adminCommand({ shardCollection: '$ns', key: { '$ShardKey': 'hashed' } }); print('shardCollection $ns -> ' + JSON.stringify(r));"
    & $mongosh "$ConnectionString" --quiet --eval $js
    if ($LASTEXITCODE -ne 0) { throw "shardCollection failed for $ns (exit $LASTEXITCODE)." }
}
