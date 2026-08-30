# Storage Facade library

## Introduction

`Beztek.Facade.Storage` is a unified storage facade for .NET. Services read and write files through a single `IStorageFacade` API; the library routes operations to local disk, SMB shares, Azure Blob Storage, or Amazon S3 based on configuration.

## Core API (`IStorageFacade`)

| Method | Behavior |
|--------|----------|
| `GetName` | Logical store prefix (e.g. `s3://bucket`, `https://account.blob.core.windows.net/container`, `\\server\share`) |
| `GetType` | `StorageFacadeType` for the backing store |
| `EnumerateStorageInfo` | List files under a path; optional recursion and `StorageFilter` |
| `GetStorageInfo` | Metadata for one object |
| `ReadStorageAsync` | Open a read stream |
| `WriteStorageAsync` | Write a stream; returns base64 MD5; optional checksum validation |
| `DeleteStorageAsync` | Remove an object |
| `ComputeMD5Checksum` | MD5 of stored content |

Obtain instances via `StorageFacadeFactory.GetStorageFacade`.

## Initializing storage

### Local files

```csharp
IStorageProviderConfig config = new FileStorageProviderConfig();
IStorageFacade storage = StorageFacadeFactory.GetStorageFacade(config);

await storage.WriteStorageAsync(@"/tmp/demo/hello.txt", stream, createParentDirectories: true);
```

### Amazon S3

```csharp
var config = new AwsS3StorageProviderConfig(accessKeyId, secretAccessKey, region, bucketName);
IStorageFacade storage = StorageFacadeFactory.GetStorageFacade(config);

await storage.WriteStorageAsync($"s3://{bucketName}/folder/file.pdf", stream, createParentDirectories: true);
```

### Azure Blob Storage (account key)

```csharp
var config = new AzureBlobStorageProviderConfig(
    "myaccount.blob.core.windows.net",
    accountKey,
    containerName,
    isHierarchicalNamespace: false);
IStorageFacade storage = StorageFacadeFactory.GetStorageFacade(config);
```

### Azure Blob Storage (SAS URI)

```csharp
var uri = new Uri("https://myaccount.blob.core.windows.net/container?sv=...&sig=...");
var config = new AzureBlobStorageProviderConfig(uri);
IStorageFacade storage = StorageFacadeFactory.GetStorageFacade(config);
```

### SMB network share

```csharp
var config = new SMBNetworkStorageProviderConfig(
    logicalServer: "fileserver",
    shareName: "Documents",
    domain: "CORP",
    username: "svc-account",
    password: "secret",
    physicalServer: "physical-host"); // optional DFS mapping
IStorageFacade storage = StorageFacadeFactory.GetStorageFacade(config);

await storage.WriteStorageAsync(@"\\fileserver\Documents\reports\q1.pdf", stream, createParentDirectories: true);
```

## Combo storage (multi-store)

`ComboStorageFacade` lets a service use **one** `IStorageFacade` while talking to several backends (S3, Azure, SMB, and local disk) in the same process. The combo does not merge namespaces; it **routes each call** to the child facade whose store prefix matches the logical path.

This mirrors how a Windows user might open `\\fileserver\share\doc.pdf`, `s3://bucket/key`, and `C:\temp\local.txt` from the same app — the path itself tells you which store to use.

### How routing works

Every `IStorageFacade` operation (`EnumerateStorageInfo`, `GetStorageInfo`, `ReadStorageAsync`, `WriteStorageAsync`, `DeleteStorageAsync`, `ComputeMD5Checksum`) follows the same rule:

1. Take the **logical path** passed to the call (or `StorageInfo.LogicalPath` for reads).
2. Compare it against each registered child facade’s **`GetName()`** value, which acts as that store’s **path prefix**.
3. Route to the **first** registered facade whose prefix matches.
4. If **no** registered prefix matches, route to the **built-in default local-file facade**.

```
logical path
    │
    ▼
starts with "s3://orders"?          ──yes──► S3 facade
    │ no
    ▼
starts with "https://acct.../archive"? ──yes──► Azure facade
    │ no
    ▼
starts with "\\fileserver\docs"?   ──yes──► SMB facade
    │ no
    ▼
default local-file facade          (OS path, e.g. /tmp/x or C:\data\x)
```

Matching is implemented as:

```csharp
logicalPath.ToLower().StartsWith(entry.Key)
```

Registered store names from the provider configs are already normalized to lowercase (`s3://bucket`, `https://account.blob.core.windows.net/container`, `\\server\share`), so paths should use the same casing conventions.

**Important routing details:**

| Topic | Behavior |
|-------|----------|
| Match rule | **Prefix** match on the full logical path string |
| Tie-breaking | **First registered** facade wins — **not** longest-prefix. If two prefixes could both match, registration order matters |
| Default fallback | Any path that matches **no** registered prefix uses local files |
| Path shape | Remote paths must include the store prefix (e.g. `s3://orders/invoices/a.pdf`, not `invoices/a.pdf`) |
| Local paths | Standard OS paths (`/var/data/x`, `C:\temp\x`) typically match nothing registered and fall through to local files |

### Default local-file facade

The combo **always** creates a default local-file facade in its constructor:

```csharp
_defaultStorageFacade = StorageFacadeFactory.GetStorageFacade(new FileStorageProviderConfig());
```

This default is **not** added to the prefix routing table. `FileStorageProviderConfig` sets `Name` to `"Local Store"`, which is a display label — not a path prefix — so it never participates in `StartsWith` matching.

Instead, the default acts purely as a **catch-all**:

- Registered prefixes cover your remote stores (S3 URI, Azure container URL, SMB UNC root).
- Everything else is treated as a **native filesystem path** on the machine running the process.

The local provider passes `logicalPath` straight to `System.IO` (`File.OpenRead`, `Directory.CreateDirectory`, etc.), so combo fallback paths must be valid for the host OS.

Example fallback paths:

| Path | Routed to |
|------|-----------|
| `/tmp/combo/local.txt` | Default local file facade |
| `C:\Users\me\Downloads\file.pdf` | Default local file facade |
| `s3://orders/a.pdf` | S3 facade (registered prefix) |
| `\\fileserver\docs\a.pdf` | SMB facade (registered prefix) |

You do **not** configure the default local store separately — it is fixed to `FileStorageProviderConfig()` with no custom root. To use a different local root, pass an absolute OS path in your logical paths; the provider writes wherever that path points.

### Setup example

```csharp
var s3 = StorageFacadeFactory.GetStorageFacade(new AwsS3StorageProviderConfig(key, secret, region, "orders"));
// s3.GetName() => "s3://orders"

var azure = StorageFacadeFactory.GetStorageFacade(new AzureBlobStorageProviderConfig(blobSasUri));
// azure.GetName() => "https://myaccount.blob.core.windows.net/archive"

var smb = StorageFacadeFactory.GetStorageFacade(new SMBNetworkStorageProviderConfig(
    "fileserver", "Documents", "CORP", user, pass));
// smb.GetName() => "\\fileserver\documents"

var combo = new ComboStorageFacade(new List<IStorageFacade> { s3, azure, smb });

// S3 — prefix "s3://orders"
await combo.WriteStorageAsync("s3://orders/invoices/2024/a.pdf", stream, createParentDirectories: true);

// Azure — prefix "https://myaccount.blob.core.windows.net/archive"
await combo.WriteStorageAsync("https://myaccount.blob.core.windows.net/archive/backup/b.pdf", stream, true);

// SMB — prefix "\\fileserver\documents"
await combo.WriteStorageAsync(@"\\fileserver\Documents\reports\q1.pdf", stream, true);

// Local fallback — no registered prefix matches
await combo.WriteStorageAsync(@"/var/app/scratch/temp.dat", stream, true);
```

### Registration order and overlapping prefixes

Because routing stops at the **first** match, register **more specific** prefixes before general ones if they could overlap. For example, if you ever registered two S3 buckets, put the longer/more specific URI prefix first:

```csharp
// Good: specific bucket before any hypothetical shared prefix
new List<IStorageFacade> { ordersS3Facade, archiveS3Facade, ... }
```

If `s3://orders` and `s3://orders-archive` were both registered, `s3://orders-archive/file` would match `s3://orders` first (because `"s3://orders-archive".StartsWith("s3://orders")` is true). Order facades accordingly or use non-overlapping bucket names.

### When to use combo vs single facade

| Use combo when | Use a single facade when |
|----------------|--------------------------|
| One service reads/writes local scratch **and** remote stores | All paths live in one backend |
| Paths already encode the store (URI / UNC / OS path) | You want a single bucket/share root without prefixes |
| You want one DI registration for “all storage” | Prefix routing would add no value |

## SMB and DFS

The SMB provider connects to a **physical server** while exposing a **logical server** name in UNC paths. This supports a manual DFS mapping when the underlying SMB library cannot resolve DFS automatically: map `\\dfs-server\share` to `\\physical-server\share` via `physicalServer` in `SMBNetworkStorageProviderConfig`. The mapping must remain stable for the lifetime of the facade instance.

## Filtering listings

`StorageFilter` supports extension lists, regex patterns on `LogicalPath`, and a half-open UTC date range:

```csharp
var filter = new StorageFilter
{
    Extensions = new List<string> { ".pdf", ".txt" },
    DateRange = Tuple.Create(startUtc, endUtc)
};

foreach (StorageInfo info in storage.EnumerateStorageInfo(root, isRecursive: true, filter))
    Console.WriteLine(info.LogicalPath);
```

## Providers

| Provider | Configuration | Notes |
|----------|---------------|-------|
| Local files | `FileStorageProviderConfig` | Uses OS paths directly |
| SMB | `SMBNetworkStorageProviderConfig` | Linux-friendly SMB client; optional DFS mapping |
| Azure Blob | `AzureBlobStorageProviderConfig` | Account key or SAS URI; flat or hierarchical namespace |
| Amazon S3 | `AwsS3StorageProviderConfig` | Standard S3 API |
| Combo | `ComboStorageFacade` | Prefix-based routing across facades |

## Testing

Unit tests use **Moq** for S3, Azure, and SMB provider dependencies (mock `IAmazonS3`, `IAzureBlobContainerAdapter`, `ISmbClientFactory` / `ISMBFileStore`). Local file tests use the real filesystem. No cloud credentials or network shares are required in CI.

XML documentation is included in the NuGet package (`GenerateDocumentationFile`).
