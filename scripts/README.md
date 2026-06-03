# Maintenance scripts

PowerShell helpers for maintaining the MongoDB (Azure Cosmos DB for MongoDB) store.

## Why these exist

Azure Cosmos DB for MongoDB supports documents up to **16 MB**, but the
`EnableMongo16MBDocumentSupport` capability **only applies to collections created
after the feature is enabled**. Collections created earlier keep the old **2 MB**
limit, which causes `RequestEntityTooLarge (413) – "Request size is too large"`
when saving large content (e.g. a generated image stored inline in a chat message).

The fix is to **recreate** the affected collections while the capability is active.
These scripts back the data up and recreate the collections from the backup.

> **Shard key matters.** The collections are sharded on **`Username`** (hashed).
> `mongodump`/`mongorestore` do **not** capture or restore the Cosmos shard key, so a
> plain restore recreates them *unsharded* and every write then fails with
> `PartitionKey extracted from document doesn't match the one specified in the header`.
> These scripts re-shard each collection (via `shardCollection`) **before** loading the
> data. Override the key with `-ShardKey` if it ever differs.

## Prerequisites

- MongoDB CLI tools:
  ```powershell
  winget install -e --id MongoDB.DatabaseTools
  winget install -e --id MongoDB.Shell
  ```
  (Reopen the shell afterwards, or the scripts will fall back to the default install path.)
- The **16 MB feature must already be enabled** on the Cosmos account (Azure Portal → the
  account → Features → "Version 16 MB document support for MongoDB").

## Choosing the target database

Every script resolves the MongoDB connection string in this order:

1. An explicit `-ConnectionString "<value>"` argument.
2. A **`.env` file in this `scripts` folder** (optional).
3. The app's local .NET user secrets (`ConnectionStrings:MongoDb`).

To run against another database (e.g. **production**), copy `.env.example` to `.env`
and set the connection string:

```
ConnectionStrings__MongoDb=mongodb://<account>:<key>@<host>.mongo.cosmos.azure.com:10255/?ssl=true&...
```

Accepted keys (first match wins): `ConnectionStrings__MongoDb`, `MongoDb`,
`MONGODB_CONNECTION_STRING`, `MONGODB_URI`, `ConnectionString`.

> `.env` is **gitignored** - never commit a real connection string. When a `.env` is
> present the scripts print which file/key they used, and every script prints the target
> host, so you can confirm you're pointing at the right database before the destructive step.
> Delete or rename `.env` to fall back to user secrets (the test DB).

## Scripts

| Script | Purpose | Destructive |
| --- | --- | --- |
| `Common.ps1` | Shared helpers (tool + connection-string resolution). Dot-sourced by the others. | no |
| `Backup-MongoCollections.ps1` | `mongodump` the given collections to `..\backups\mongo-<timestamp>`. | no |
| `Restore-MongoCollections.ps1` | Per collection: `-Drop`, re-shard on `-ShardKey` (hashed), then load documents. | with `-Drop` |
| `Migrate-CollectionsTo16MB.ps1` | Backup → confirm → drop + re-shard + restore, so collections inherit the 16 MB limit and keep their shard key. | yes |

## Usage

Run the full migration (backs up, asks for confirmation, then recreates):

```powershell
cd scripts
.\Migrate-CollectionsTo16MB.ps1
```

Or step by step:

```powershell
# 1. Back up (safe, read-only) — prints the backup folder path
$dump = .\Backup-MongoCollections.ps1

# 2. Recreate the collections from that backup (destructive)
.\Restore-MongoCollections.ps1 -DumpDir $dump -Drop
```

Defaults target the `ChatMessages` and `Files` collections in the `Users` database.
Override with `-Database` / `-Collections` if needed.

> If `RequestEntityTooLarge` persists after recreating, the capability isn't actually
> active on the account (e.g. blocked by continuous backup mode) — recreating collections
> can't help until the feature is enabled.
