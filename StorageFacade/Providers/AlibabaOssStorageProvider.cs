// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Providers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Security.Cryptography;
    using System.Threading.Tasks;
    using Aliyun.OSS;
    using Aliyun.OSS.Common.Authentication;

    /// <summary>Listed object metadata used by <see cref="AlibabaOssStorageProvider"/> (testable without SDK types).</summary>
    internal sealed record OssListedObject(string Key, DateTime LastModified, long Size);

    /// <summary>Object metadata used by <see cref="AlibabaOssStorageProvider"/>.</summary>
    internal sealed record OssObjectInfo(DateTime LastModified, long ContentLength);

    /// <summary>Abstraction over <see cref="OssClient"/> for unit tests.</summary>
    internal interface IOssObjectClient : IDisposable
    {
        IEnumerable<OssListedObject> ListObjects(string prefix);

        OssObjectInfo GetObjectMetadata(string key);

        Stream GetObjectStream(string key);

        void PutObject(string key, Stream content);

        void DeleteObject(string key);
    }

    /// <summary>Thin SDK forwarder — exercised by live OSS; excluded so Coverlet tracks provider logic.</summary>
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal sealed class OssObjectClientAdapter : IOssObjectClient
    {
        private readonly OssClient _client;
        private readonly string _bucket;
        private bool _disposed;

        internal OssObjectClientAdapter(OssClient client, string bucket)
        {
            _client = client;
            _bucket = bucket;
        }

        public IEnumerable<OssListedObject> ListObjects(string prefix)
        {
            string marker = null;
            do
            {
                var request = new ListObjectsRequest(_bucket)
                {
                    Prefix = prefix,
                    Marker = marker,
                    MaxKeys = 1000,
                };
                ObjectListing listing = _client.ListObjects(request);
                foreach (OssObjectSummary summary in listing.ObjectSummaries)
                {
                    yield return new OssListedObject(
                        summary.Key,
                        summary.LastModified.ToUniversalTime(),
                        summary.Size);
                }

                marker = listing.IsTruncated ? listing.NextMarker : null;
            }
            while (marker != null);
        }

        public OssObjectInfo GetObjectMetadata(string key)
        {
            ObjectMetadata meta = _client.GetObjectMetadata(_bucket, key);
            return new OssObjectInfo(meta.LastModified.ToUniversalTime(), meta.ContentLength);
        }

        public Stream GetObjectStream(string key)
        {
            OssObject obj = _client.GetObject(_bucket, key);
            return obj.Content;
        }

        public void PutObject(string key, Stream content) => _client.PutObject(_bucket, key, content);

        public void DeleteObject(string key) => _client.DeleteObject(_bucket, key);

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            (_client as IDisposable)?.Dispose();
        }
    }

    /// <summary>Storage provider for Alibaba Cloud Object Storage Service (OSS).</summary>
    internal class AlibabaOssStorageProvider : IStorageProvider, IDisposable
    {
        private readonly AlibabaOssStorageProviderConfig _config;
        private readonly IOssObjectClient _client;
        private bool _disposed;

        internal AlibabaOssStorageProvider(AlibabaOssStorageProviderConfig config)
            : this(config, new OssObjectClientAdapter(CreateOssClient(config), config.BucketName))
        {
        }

        internal AlibabaOssStorageProvider(AlibabaOssStorageProviderConfig config, IOssObjectClient client)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        internal static OssClient CreateOssClient(AlibabaOssStorageProviderConfig config)
        {
            ICredentialsProvider credentials = !string.IsNullOrEmpty(config.AccessKeyId)
                && !string.IsNullOrEmpty(config.AccessKeySecret)
                ? new DefaultCredentialsProvider(
                    new DefaultCredentials(config.AccessKeyId, config.AccessKeySecret, config.SecurityToken))
                : new AlibabaEnvironmentCredentialsProvider();

            return new OssClient(config.Endpoint, credentials);
        }

        public string GetName() => _config.Name;

        public new StorageFacadeType GetType() => _config.StorageFacadeType;

        public IEnumerable<StorageInfo> EnumerateStorageInfo(string logicalPath, bool isRecursive = false, StorageFilter storageFilter = null)
        {
            string prefix = CloudLogicalPath.GetRelativePath(GetName(), logicalPath);
            if (prefix == "/")
                prefix = "";

            foreach (OssListedObject summary in _client.ListObjects(prefix))
            {
                if (!CloudLogicalPath.IsListedObjectVisible(prefix, summary.Key, isRecursive))
                    continue;

                StorageInfo info = ToStorageInfo(summary);
                if (StorageFilter.IsMatch(storageFilter, info))
                    yield return info;
            }
        }

        public StorageInfo GetStorageInfo(string logicalPath)
        {
            string key = CloudLogicalPath.GetRelativePath(GetName(), logicalPath);
            OssObjectInfo meta = _client.GetObjectMetadata(key);
            return new StorageInfo
            {
                IsFile = true,
                Name = CloudLogicalPath.GetLeafName(key),
                LogicalPath = logicalPath,
                Timestamp = meta.LastModified.ToUniversalTime(),
                SizeBytes = meta.ContentLength,
            };
        }

        public Task<Stream> ReadStorageAsync(StorageInfo storageInfo)
            => Task.FromResult(_client.GetObjectStream(CloudLogicalPath.GetRelativePath(GetName(), storageInfo.LogicalPath)));

        public Task WriteStorageAsync(string logicalPath, Stream inputStream, bool createParentDirectories = false)
        {
            _ = createParentDirectories;
            _client.PutObject(CloudLogicalPath.GetRelativePath(GetName(), logicalPath), inputStream);
            return Task.CompletedTask;
        }

        public Task DeleteStorageAsync(string logicalPath)
        {
            _client.DeleteObject(CloudLogicalPath.GetRelativePath(GetName(), logicalPath));
            return Task.CompletedTask;
        }

        public async Task<string> ComputeMD5Checksum(string logicalPath)
        {
            using var md5 = MD5.Create();
            await using Stream stream = _client.GetObjectStream(CloudLogicalPath.GetRelativePath(GetName(), logicalPath));
            return Convert.ToBase64String(await md5.ComputeHashAsync(stream).ConfigureAwait(false));
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _client.Dispose();
        }

        private StorageInfo ToStorageInfo(OssListedObject summary)
        {
            return new StorageInfo
            {
                IsFile = true,
                Name = CloudLogicalPath.GetLeafName(summary.Key),
                LogicalPath = $"{GetName()}/{summary.Key}",
                Timestamp = summary.LastModified.ToUniversalTime(),
                SizeBytes = summary.Size,
            };
        }
    }
}
