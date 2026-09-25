// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Providers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Security.Cryptography;
    using System.Threading.Tasks;
    using Google.Apis.Auth.OAuth2;
    using Google.Cloud.Storage.V1;
    using Object = Google.Apis.Storage.v1.Data.Object;

    /// <summary>Listed object metadata used by <see cref="GoogleCloudStorageProvider"/>.</summary>
    internal sealed record GcsListedObject(string Name, DateTimeOffset? Updated, ulong? Size);

    /// <summary>Abstraction over <see cref="StorageClient"/> for unit tests.</summary>
    internal interface IGcsObjectClient : IDisposable
    {
        IEnumerable<GcsListedObject> ListObjects(string prefix);

        GcsListedObject GetObject(string objectName);

        Task DownloadObjectAsync(string objectName, Stream destination);

        Task UploadObjectAsync(string objectName, Stream content);

        Task DeleteObjectAsync(string objectName);
    }

    /// <summary>Thin SDK forwarder — exercised by live GCS; excluded so Coverlet tracks provider logic.</summary>
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal sealed class GcsObjectClientAdapter : IGcsObjectClient
    {
        private readonly StorageClient _client;
        private readonly string _bucket;
        private bool _disposed;

        internal GcsObjectClientAdapter(StorageClient client, string bucket)
        {
            _client = client;
            _bucket = bucket;
        }

        public IEnumerable<GcsListedObject> ListObjects(string prefix)
        {
            foreach (Object obj in _client.ListObjects(_bucket, prefix))
            {
                yield return ToListedObject(obj);
            }
        }

        public GcsListedObject GetObject(string objectName)
        {
            Object obj = _client.GetObject(_bucket, objectName);
            return ToListedObject(obj);
        }

        public async Task DownloadObjectAsync(string objectName, Stream destination)
            => await _client.DownloadObjectAsync(_bucket, objectName, destination).ConfigureAwait(false);

        public async Task UploadObjectAsync(string objectName, Stream content)
            => await _client.UploadObjectAsync(_bucket, objectName, contentType: null, content).ConfigureAwait(false);

        public async Task DeleteObjectAsync(string objectName)
            => await _client.DeleteObjectAsync(_bucket, objectName).ConfigureAwait(false);

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _client.Dispose();
        }

        /// <summary>
        /// fake-gcs-server (and some gateways) return timestamps DiscoveryFormat cannot parse;
        /// <see cref="Object.UpdatedDateTimeOffset"/> then throws. Prefer raw fields with fallback.
        /// </summary>
        private static GcsListedObject ToListedObject(Object obj)
            => new(obj.Name, TryGetTimestamp(obj), obj.Size);

        private static DateTimeOffset? TryGetTimestamp(Object obj)
        {
            try
            {
                DateTimeOffset? updated = obj.UpdatedDateTimeOffset;
                if (updated.HasValue)
                    return updated;
            }
            catch (FormatException)
            {
                // Emulator / non-RFC3339 Updated string — fall through.
            }

            try
            {
                return obj.TimeCreatedDateTimeOffset;
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }

    /// <summary>Storage provider for Google Cloud Storage.</summary>
    internal class GoogleCloudStorageProvider : IStorageProvider, IDisposable
    {
        private readonly GoogleCloudStorageProviderConfig _config;
        private readonly IGcsObjectClient _client;
        private bool _disposed;

        internal GoogleCloudStorageProvider(GoogleCloudStorageProviderConfig config)
            : this(config, new GcsObjectClientAdapter(CreateClient(config), config.BucketName))
        {
        }

        internal GoogleCloudStorageProvider(GoogleCloudStorageProviderConfig config, IGcsObjectClient client)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public string GetName() => _config.Name;

        public new StorageFacadeType GetType() => _config.StorageFacadeType;

        public IEnumerable<StorageInfo> EnumerateStorageInfo(string logicalPath, bool isRecursive = false, StorageFilter storageFilter = null)
        {
            string prefix = CloudLogicalPath.GetRelativePath(GetName(), logicalPath);
            if (prefix == "/")
                prefix = "";

            foreach (GcsListedObject obj in _client.ListObjects(prefix))
            {
                if (!CloudLogicalPath.IsListedObjectVisible(prefix, obj.Name, isRecursive))
                    continue;

                StorageInfo info = ToStorageInfo(obj);
                if (StorageFilter.IsMatch(storageFilter, info))
                    yield return info;
            }
        }

        public StorageInfo GetStorageInfo(string logicalPath)
        {
            GcsListedObject obj = _client.GetObject(CloudLogicalPath.GetRelativePath(GetName(), logicalPath));
            return ToStorageInfo(obj, logicalPath);
        }

        public Task<Stream> ReadStorageAsync(StorageInfo storageInfo)
            => ReadStorageAsync(storageInfo.LogicalPath);

        public async Task WriteStorageAsync(string logicalPath, Stream inputStream, bool createParentDirectories = false)
        {
            _ = createParentDirectories;
            await _client.UploadObjectAsync(CloudLogicalPath.GetRelativePath(GetName(), logicalPath), inputStream)
                .ConfigureAwait(false);
        }

        public async Task DeleteStorageAsync(string logicalPath)
        {
            await _client.DeleteObjectAsync(CloudLogicalPath.GetRelativePath(GetName(), logicalPath)).ConfigureAwait(false);
        }

        public async Task<string> ComputeMD5Checksum(string logicalPath)
        {
            using var md5 = MD5.Create();
            await using Stream stream = await ReadStorageAsync(logicalPath).ConfigureAwait(false);
            return Convert.ToBase64String(await md5.ComputeHashAsync(stream).ConfigureAwait(false));
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _client.Dispose();
        }

        internal static StorageClient CreateClient(GoogleCloudStorageProviderConfig config)
        {
            var builder = new StorageClientBuilder();
            bool hasCredentialsFile = !string.IsNullOrWhiteSpace(config.CredentialsFilePath);

            if (!string.IsNullOrWhiteSpace(config.ServiceUri))
            {
                string baseUri = config.ServiceUri.TrimEnd('/');
                if (!baseUri.EndsWith("/storage/v1", StringComparison.OrdinalIgnoreCase))
                    baseUri += "/storage/v1";
                builder.BaseUri = baseUri + "/";
                builder.UnauthenticatedAccess = !hasCredentialsFile;
            }

            if (hasCredentialsFile)
            {
                ServiceAccountCredential sa = CredentialFactory.FromFile<ServiceAccountCredential>(
                    config.CredentialsFilePath);
                builder.Credential = sa.ToGoogleCredential();
            }

            return builder.Build();
        }

        private StorageInfo ToStorageInfo(GcsListedObject obj, string logicalPath = null)
        {
            return new StorageInfo
            {
                IsFile = true,
                Name = CloudLogicalPath.GetLeafName(obj.Name),
                LogicalPath = logicalPath ?? $"{GetName()}/{obj.Name}",
                Timestamp = obj.Updated?.UtcDateTime ?? DateTime.UtcNow,
                SizeBytes = (long)(obj.Size ?? 0),
            };
        }

        private async Task<Stream> ReadStorageAsync(string logicalPath)
        {
            var ms = new MemoryStream();
            await _client.DownloadObjectAsync(CloudLogicalPath.GetRelativePath(GetName(), logicalPath), ms)
                .ConfigureAwait(false);
            ms.Position = 0;
            return ms;
        }
    }
}
