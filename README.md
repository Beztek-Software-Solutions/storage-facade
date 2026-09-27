# Storage Facade

Unified .NET storage facade (`Beztek.Facade.Storage`) over local files, SMB network shares, Azure Blob Storage, Amazon S3, Google Cloud Storage, and Alibaba OSS — including a prefix-routed `ComboStorageFacade` across all of them.

Source: https://github.com/Beztek-Software-Solutions/storage-facade

## Projects

| Project | Description |
|--------|-------------|
| [`StorageFacade/`](StorageFacade/) | Library package `Beztek.Facade.Storage` (see [StorageFacade/README.md](StorageFacade/README.md) for full API and provider guidance) |
| [`StorageFacade.Tests/`](StorageFacade.Tests/) | NUnit unit tests + optional live suite (`Category=Live`) |

## Quick start

```bash
make test                 # unit + live File (no containers)
make test-unit            # unit only (excludes Category=Live)
make test-azure           # unit + live File + Azure Blob (Azurite)
make test-gcs             # unit + live File + GCS (fake-gcs-server)
make -- test --use-s3-container --use-smb-container --use-azure-container --use-gcs-container
make -- test --use-oss-live   # needs OSS__Endpoint + ALIBABA_CLOUD_* (no local emulator)
make coverage             # Coverlet + terminal summary (unit by default)
make coverage-html        # HTML report (opens in browser)
make containers-stop
```

Or without Make:

```bash
dotnet restore Beztek.Facade.Storage.sln
dotnet build Beztek.Facade.Storage.sln
dotnet test StorageFacade.Tests/Beztek.Facade.Storage.Tests.csproj
```

### Live container tests

Default live path is **File** (temp directory; no container engine). Optional backends are started by Make (podman preferred, else docker). Selection is written to `.live.env` as `STORAGEFACADE_LIVE_PROVIDERS`:

| Flag | Container / backend | Notes |
|------|---------------------|--------|
| `--use-s3-container` | `adobe/s3mock` | Host port **19090**→9090 (override `S3MOCK_HOST_PORT`); throwaway keys `test`/`test` |
| `--use-azure-container` | Azurite 3.37 | Blob port 10000 (`AZURITE_HOST_PORT`); well-known Azurite account key; `--skipApiVersionCheck` for current `Azure.Storage.Blobs` |
| `--use-smb-container` | `dperson/samba` | Host **1445**→container 445 (`SAMBA_HOST_PORT`; no root). Client uses `SMB__Port` |
| `--use-gcs-container` | `fsouza/fake-gcs-server` | Host **4443**; unauthenticated emulator; bucket `storage-live` |
| `--use-oss-live` | *(no container)* | Requires `OSS__Endpoint` + `ALIBABA_CLOUD_ACCESS_KEY_ID`/`SECRET` (or `OSS_ACCESS_KEY_*`) in the environment |

```bash
make test-azure
make test-gcs
make -- test --use-s3-container
make -- test --use-s3-container --use-azure-container --use-smb-container --use-gcs-container
make -- test --use-oss-live
make test-live            # Category=Live only (after containers + .live.env)
make print-live-env
```

If a remote provider is listed but unreachable, that fixture is marked **inconclusive** with a Make hint.

Every selected provider’s live I/O runs through **`ComboStorageFacade`** (`LiveProviderHost.Storage`). **`LiveComboProviderTests`** adds per-provider checks for that provider’s path prefix (when remote) plus the combo’s built-in File fallback. When two or more remotes are selected, **`LiveComboAllProvidersTests`** registers all of them in one combo and round-trips each.

With coverage (Coverlet; target ≥ 85% line coverage with live containers):

```bash
make coverage             # unit summary (threshold off by default)
make coverage-html        # opens coverage/html/index.html
# Full suite after containers:
make -- test --use-s3-container --use-azure-container --use-smb-container --use-gcs-container
make coverage-html COVERAGE_FILTER= COVERAGE_THRESHOLD=0
```

Or without Make:

```bash
dotnet test StorageFacade.Tests/Beztek.Facade.Storage.Tests.csproj \
  /p:CollectCoverage=true \
  /p:CoverletOutputFormat=cobertura \
  /p:CoverletOutput=./coverage/ \
  /p:Include='[Beztek.Facade.Storage]*' \
  /p:Threshold=85 \
  /p:ThresholdType=line
```

## NuGet

```bash
dotnet add package Beztek.Facade.Storage
```

See [StorageFacade/README.md](StorageFacade/README.md) for initialization samples, logical path conventions, and the combo (multi-store) facade.

## Providers

| Provider | Configuration type | Status |
|----------|-------------------|--------|
| Local files | `FileStorageProviderConfig` | Implemented |
| SMB network share | `SMBNetworkStorageProviderConfig` | Implemented (DFS mapping; optional TCP port) |
| Azure Blob Storage | `AzureBlobStorageProviderConfig` | Implemented |
| Amazon S3 | `AwsS3StorageProviderConfig` | Implemented (default AWS credential chain preferred) |
| Google Cloud Storage | `GoogleCloudStorageProviderConfig` | Implemented (ADC preferred; no embedded keys) |
| Alibaba OSS | `AlibabaOssStorageProviderConfig` | Implemented (env credentials preferred; no embedded keys) |
| Combo (multi-store) | `ComboStorageFacade` | Implemented |
