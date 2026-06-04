#:package MongoDB.Driver@3.9.0

// Inserts only the MISSING documents from a mongodump .bson into a collection.
// - Reads the dump once (streaming), inserts each document.
// - Already-present docs (duplicate _id) are skipped, not re-written.
// - Throttled inserts (Cosmos 16500 / 429 "Request rate is large") are retried
//   per-document with exponential backoff instead of aborting the whole run.
// - NEVER drops or shards anything.
//
// Usage (connection string via --uri or the MONGO_FILL_URI env var):
//   dotnet run FillMissingDocuments.cs -- --uri "<cs>" --db Users --collection Files --bson <path-to.bson>
//   dotnet run FillMissingDocuments.cs -- --dry-run --bson <path-to.bson> --limit 5

using System.Diagnostics;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

string? uri = Environment.GetEnvironmentVariable("MONGO_FILL_URI");
string db = "Users";
string? collection = null;
string? bsonPath = null;
int progressEvery = 200;
int maxDelaySeconds = 60;
int maxThrottleAttempts = 50;
long limit = 0;
bool dryRun = false;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--uri": uri = args[++i]; break;
        case "--db": db = args[++i]; break;
        case "--collection": collection = args[++i]; break;
        case "--bson": bsonPath = args[++i]; break;
        case "--progress": progressEvery = int.Parse(args[++i]); break;
        case "--max-delay-seconds": maxDelaySeconds = int.Parse(args[++i]); break;
        case "--max-throttle-attempts": maxThrottleAttempts = int.Parse(args[++i]); break;
        case "--limit": limit = long.Parse(args[++i]); break;
        case "--dry-run": dryRun = true; break;
        default: Console.Error.WriteLine($"Unknown argument: {args[i]}"); return 2;
    }
}

if (string.IsNullOrWhiteSpace(bsonPath) || !File.Exists(bsonPath))
{
    Console.Error.WriteLine($"--bson file not found: {bsonPath}");
    return 2;
}
if (!dryRun && string.IsNullOrWhiteSpace(collection)) { Console.Error.WriteLine("--collection is required"); return 2; }
if (!dryRun && string.IsNullOrWhiteSpace(uri)) { Console.Error.WriteLine("--uri (or MONGO_FILL_URI) is required"); return 2; }

IMongoCollection<BsonDocument>? coll = null;
if (!dryRun)
{
    var client = new MongoClient(uri);
    coll = client.GetDatabase(db).GetCollection<BsonDocument>(collection);
    Console.WriteLine($"Target: {db}.{collection} (insert-missing only; no drop)");
}
else
{
    Console.WriteLine("DRY RUN: reading the dump only, no database connection, no writes.");
}

long processed = 0, inserted = 0, skipped = 0, failed = 0, throttleWaits = 0;
var sw = Stopwatch.StartNew();

using var fs = File.OpenRead(bsonPath);
using var reader = new BsonBinaryReader(fs);

while (!reader.IsAtEndOfFile())
{
    BsonDocument doc = BsonSerializer.Deserialize<BsonDocument>(reader);
    processed++;

    if (!dryRun)
    {
        int attempt = 0;
        while (true)
        {
            try { coll!.InsertOne(doc); inserted++; break; }
            catch (Exception ex) when (IsDuplicate(ex)) { skipped++; break; }
            catch (Exception ex) when (IsThrottle(ex))
            {
                attempt++;
                throttleWaits++;
                if (attempt > maxThrottleAttempts)
                {
                    Console.Error.WriteLine($"  GIVING UP after {attempt} throttled attempts on _id={SafeId(doc)}");
                    failed++;
                    break;
                }
                int delayMs = Math.Min(1000 * (1 << Math.Min(attempt, 6)), maxDelaySeconds * 1000);
                Thread.Sleep(delayMs);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  FAILED _id={SafeId(doc)}: {ex.GetType().Name}: {ex.Message}");
                failed++;
                break;
            }
        }
    }

    if (processed % progressEvery == 0)
        Console.WriteLine($"processed={processed} inserted={inserted} skipped(existing)={skipped} failed={failed} throttleWaits={throttleWaits} elapsed={sw.Elapsed:hh\\:mm\\:ss}");

    if (limit > 0 && processed >= limit) break;
}

Console.WriteLine($"DONE. processed={processed} inserted={inserted} skipped(existing)={skipped} failed={failed} throttleWaits={throttleWaits} elapsed={sw.Elapsed:hh\\:mm\\:ss}");
return failed > 0 ? 1 : 0;

static string SafeId(BsonDocument d) => d.TryGetValue("_id", out var v) ? (v.ToString() ?? "?") : "?";

static bool IsDuplicate(Exception ex)
{
    if (ex is MongoWriteException mwe && mwe.WriteError != null &&
        (mwe.WriteError.Category == ServerErrorCategory.DuplicateKey || mwe.WriteError.Code == 11000))
        return true;
    string m = ex.Message ?? string.Empty;
    return m.Contains("E11000") || m.Contains("duplicate key");
}

static bool IsThrottle(Exception ex)
{
    string m = ex.Message ?? string.Empty;
    if (m.Contains("16500") || m.Contains("TooManyRequests") || m.Contains("Request rate is large") || m.Contains("429"))
        return true;
    if (ex is MongoCommandException mce && mce.Code == 16500) return true;
    if (ex is MongoWriteException mwe && mwe.WriteError != null && mwe.WriteError.Code == 16500) return true;
    return false;
}
