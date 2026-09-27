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

### Amazon S3 (default credentials — preferred on AWS)

Uses the AWS SDK default credential chain (EC2/ECS instance role, IRSA,
environment variables, shared credentials file). No long-lived access keys.

```csharp
string bucketName = "my-bucket";
var config = new AwsS3StorageProviderConfig(regionName: "us-east-1", bucketName: bucketName);
IStorageFacade storage = StorageFacadeFactory.GetStorageFacade(config);

await storage.WriteStorageAsync($"s3://{bucketName}/folder/file.pdf", stream, createParentDirectories: true);
```

Optional custom endpoint (MinIO / LocalStack / S3Mock):

```csharp
var config = new AwsS3StorageProviderConfig(
    regionName: "us-east-1",
    bucketName: "my-bucket",
    serviceUrl: "http://127.0.0.1:9090");
```

### Amazon S3 (explicit access keys)

```csharp
var config = new AwsS3StorageProviderConfig(accessKeyId, secretAccessKey, region, bucketName);
IStorageFacade storage = StorageFacadeFactory.GetStorageFacade(config);

await storage.WriteStorageAsync($"s3://{bucketName}/folder/file.pdf", stream, createParentDirectories: true);
```

Temporary STS credentials (access key + secret + session token) are supported via
an overload, but those credentials do **not** auto-refresh. Prefer the default
credential chain for IAM roles.

**Multipart cleanup:** Unknown-length / non-seekable streams (including writes through
`StorageFacade`, which wraps the input in a `CryptoStream`) use S3 multipart upload via
`TransferUtility`. If that write fails mid-stream, the provider best-effort aborts any
incomplete multipart uploads for that key. `DeleteObject` alone does **not** remove
incomplete multiparts (they are not objects until completed); `DeleteStorageAsync`
therefore also lists and aborts incomplete multiparts for the key, so a try-delete after
an aborted or orphaned upload frees part storage.

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

### Azure Blob Storage (connection string / Azurite)

```csharp
// Azurite: make test-azure  (or --use-azure-container)
var config = new AzureBlobStorageProviderConfig(
    "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=…;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;",
    containerName: "storage-live");
IStorageFacade storage = StorageFacadeFactory.GetStorageFacade(config);
```

Custom service URI + account key (same stand-in without a connection string):

```csharp
var config = new AzureBlobStorageProviderConfig(
    new Uri("http://127.0.0.1:10000/devstoreaccount1"),
    accountKey,
    containerName: "storage-live");
```

### SMB network share

```csharp
var config = new SMBNetworkStorageProviderConfig(
    logicalServer: "fileserver",
    shareName: "Documents",
    domain: "CORP",
    username: "svc-account",
    password: "secret",
    physicalServer: "physical-host", // optional DFS mapping
    port: 445); // optional; use e.g. 1445 for a container published on a high host port
IStorageFacade storage = StorageFacadeFactory.GetStorageFacade(config);

await storage.WriteStorageAsync(@"\\fileserver\Documents\reports\q1.pdf", stream, createParentDirectories: true);
```

### Google Cloud Storage (Application Default Credentials)

```csharp
// ADC: GOOGLE_APPLICATION_CREDENTIALS, gcloud ADC, GCE/GKE metadata — no keys in code
var config = new GoogleCloudStorageProviderConfig(bucketName: "my-bucket");
IStorageFacade storage = StorageFacadeFactory.GetStorageFacade(config);

// Emulator (fake-gcs-server): service URI only, unauthenticated
var emulator = new GoogleCloudStorageProviderConfig(
    bucketName: "storage-live",
    serviceUri: "http://127.0.0.1:4443/storage/v1/");
```

Optional path to a service-account JSON *file* (same as `GOOGLE_APPLICATION_CREDENTIALS`):

```csharp
var config = new GoogleCloudStorageProviderConfig(
    bucketName: "my-bucket",
    serviceUri: null,
    credentialsFilePath: Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS"));
```

### Alibaba Cloud OSS (environment credentials)

```csharp
// Prefer env: ALIBABA_CLOUD_ACCESS_KEY_ID / ALIBABA_CLOUD_ACCESS_KEY_SECRET
// (optional ALIBABA_CLOUD_SECURITY_TOKEN), or legacy OSS_ACCESS_KEY_*
var config = new AlibabaOssStorageProviderConfig(
    endpoint: "oss-us-west-1.aliyuncs.com",
    bucketName: "my-bucket");
IStorageFacade storage = StorageFacadeFactory.GetStorageFacade(config);
```

Explicit keys only when injected at runtime from a secret store (not hard-coded):

```csharp
var config = new AlibabaOssStorageProviderConfig(
    endpoint: "oss-us-west-1.aliyuncs.com",
    accessKeyId: Environment.GetEnvironmentVariable("ALIBABA_CLOUD_ACCESS_KEY_ID"),
    accessKeySecret: Environment.GetEnvironmentVariable("ALIBABA_CLOUD_ACCESS_KEY_SECRET"),
    bucketName: "my-bucket");
```

## Combo storage (multi-store)

`ComboStorageFacade` lets a service use **one** `IStorageFacade` while talking to several backends (local files, SMB, Azure Blob, Amazon S3, Google Cloud Storage, and Alibaba OSS) in the same process. The combo does not merge namespaces; it **routes each call** to the child facade whose store prefix matches the logical path.

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
starts with "s3://orders"?             ──yes──► S3 facade
    │ no
    ▼
starts with "https://acct.../archive"? ──yes──► Azure facade
    │ no
    ▼
starts with "\\fileserver\docs"?     ──yes──► SMB facade
    │ no
    ▼
starts with "gs://media"?              ──yes──► GCS facade
    │ no
    ▼
starts with "oss://backups"?           ──yes──► OSS facade
    │ no
    ▼
default local-file facade             (OS path, e.g. /tmp/x or C:\data\x)
```

Matching is implemented as:

```csharp
logicalPath.StartsWith(entry.Key, StringComparison.OrdinalIgnoreCase)
```

Registered store names from the provider configs are typically lowercased (`s3://bucket`, `gs://bucket`, `oss://bucket`, `https://…`, `\\server\share`); path matching itself is case-insensitive.

**Important routing details:**

| Topic | Behavior |
|-------|----------|
| Match rule | **Prefix** match on the full logical path string (case-insensitive) |
| Tie-breaking | **First registered** facade wins — **not** longest-prefix. If two prefixes could both match, registration order matters |
| Default fallback | Any path that matches **no** registered prefix uses local files |
| Path shape | Remote paths must include the store prefix (e.g. `s3://orders/invoices/a.pdf`, not `invoices/a.pdf`) |
| Local paths | Standard OS paths (`/var/data/x`, `C:\temp\x`) typically match nothing registered and fall through to local files |
| Errors | Missing object (get/read/enumerate/checksum) → `StorageNotFoundException`; missing on delete → no-op; invalid args/cancel/dispose unchanged; all other I/O/protocol/SDK failures → `StorageFacadeException` (`IOException`) |

### Default local-file facade

The combo **always** creates a default local-file facade in its constructor:

```csharp
_defaultStorageFacade = StorageFacadeFactory.GetStorageFacade(new FileStorageProviderConfig());
```

This default is **not** added to the prefix routing table. `FileStorageProviderConfig` sets `Name` to `"Local Store"`, which is a display label — not a path prefix — so it never participates in `StartsWith` matching.

Instead, the default acts purely as a **catch-all**:

- Registered prefixes cover your remote stores (S3 / GCS / OSS URI, Azure container URL, SMB UNC root).
- Everything else is treated as a **native filesystem path** on the machine running the process.

The local provider passes `logicalPath` straight to `System.IO` (`File.OpenRead`, `Directory.CreateDirectory`, etc.), so combo fallback paths must be valid for the host OS.

Example fallback paths:

| Path | Routed to |
|------|-----------|
| `/tmp/combo/local.txt` | Default local file facade |
| `C:\Users\me\Downloads\file.pdf` | Default local file facade |
| `s3://orders/a.pdf` | S3 facade (registered prefix) |
| `https://acct.blob.core.windows.net/archive/a.pdf` | Azure facade (registered prefix) |
| `\\fileserver\docs\a.pdf` | SMB facade (registered prefix) |
| `gs://media/a.mp4` | GCS facade (registered prefix) |
| `oss://backups/a.bin` | OSS facade (registered prefix) |

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

var gcs = StorageFacadeFactory.GetStorageFacade(new GoogleCloudStorageProviderConfig("media"));
// gcs.GetName() => "gs://media"

var oss = StorageFacadeFactory.GetStorageFacade(new AlibabaOssStorageProviderConfig(endpoint, "backups"));
// oss.GetName() => "oss://backups"

var combo = new ComboStorageFacade(new List<IStorageFacade> { s3, azure, smb, gcs, oss });

// S3 — prefix "s3://orders"
await combo.WriteStorageAsync("s3://orders/invoices/2024/a.pdf", stream, createParentDirectories: true);

// Azure — prefix "https://myaccount.blob.core.windows.net/archive"
await combo.WriteStorageAsync("https://myaccount.blob.core.windows.net/archive/backup/b.pdf", stream, true);

// SMB — prefix "\\fileserver\documents"
await combo.WriteStorageAsync(@"\\fileserver\Documents\reports\q1.pdf", stream, true);

// GCS — prefix "gs://media"
await combo.WriteStorageAsync("gs://media/clips/c.mp4", stream, true);

// OSS — prefix "oss://backups"
await combo.WriteStorageAsync("oss://backups/2024/d.bin", stream, true);

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
| SMB | `SMBNetworkStorageProviderConfig` | Linux-friendly SMB client; optional DFS mapping; optional TCP `port` (default 445) |
| Google Cloud Storage | `GoogleCloudStorageProviderConfig` | ADC by default; optional credentials *file path*; emulator `serviceUri` |
| Alibaba OSS | `AlibabaOssStorageProviderConfig` | Env credentials by default (`ALIBABA_CLOUD_*` / `OSS_*`); optional runtime keys |
| Azure Blob | `AzureBlobStorageProviderConfig` | Account key, SAS URI, connection string, or custom service URI (e.g. Azurite); flat or hierarchical namespace |
| Amazon S3 | `AwsS3StorageProviderConfig` | Default AWS credential chain, static keys, optional session token / `serviceUrl` |
| Combo | `ComboStorageFacade` | Prefix-based routing across facades |

## Testing

Unit tests use **Moq** for S3, Azure, and SMB provider dependencies (mock `IAmazonS3`, `IAzureBlobContainerAdapter`, `ISmbClientFactory` / `ISMBFileStore`). Local file tests use the real filesystem. No cloud credentials or network shares are required for the default suite.

Optional **live** tests (`Category=Live`) exercise real backends. Prefer the repo **Makefile**:

```bash
make test                                                    # File only
make -- test --use-s3-container --use-azure-container --use-smb-container
```

`LiveProviderTests` covers every selected provider **through** `ComboStorageFacade`
(`LiveProviderHost.Storage`), including mid-stream abort + try-delete cleanup and
unknown-length write + delete. `LiveComboProviderTests` adds explicit remote-prefix + local
fallback checks for each selected provider. When two or more remotes are selected,
`LiveComboAllProvidersTests` registers all of them in one combo. S3 incomplete multipart
abort is also covered by `LiveAwsS3MultipartCleanupTests` when `s3` is selected.

See the root [README](../README.md#live-container-tests).

XML documentation is included in the NuGet package (`GenerateDocumentationFile`).
