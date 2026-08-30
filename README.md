# Storage Facade

Unified .NET storage facade (`Beztek.Facade.Storage`) over local files, SMB network shares, Azure Blob Storage, and Amazon S3.

Source: https://github.com/Beztek-Software-Solutions/storage-facade

## Projects

| Project | Description |
|---------|-------------|
| [`StorageFacade/`](StorageFacade/) | Library package `Beztek.Facade.Storage` (see [StorageFacade/README.md](StorageFacade/README.md) for full API and provider guidance) |
| [`StorageFacade.Tests/`](StorageFacade.Tests/) | NUnit unit tests |

## Quick start

```bash
dotnet restore Beztek.Facade.Storage.sln
dotnet build Beztek.Facade.Storage.sln
dotnet test StorageFacade.Tests/Beztek.Facade.Storage.Tests.csproj
```

With coverage (Coverlet; target ≥ 85% line coverage):

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
| SMB network share | `SMBNetworkStorageProviderConfig` | Implemented (includes DFS physical-server mapping) |
| Azure Blob Storage | `AzureBlobStorageProviderConfig` | Implemented |
| Amazon S3 | `AwsS3StorageProviderConfig` | Implemented |
| Combo (multi-store) | `ComboStorageFacade` | Implemented |
