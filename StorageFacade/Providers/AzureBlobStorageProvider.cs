// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Providers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Threading.Tasks;
    using Azure.Storage;
    using Azure.Storage.Blobs;
    using Azure.Storage.Blobs.Models;
    using Beztek.Facade.Storage;

    /// <summary>
    /// Storage provider for Azure Blob Storage.
    /// </summary>
    internal class AzureBlobStorageProvider : IStorageProvider, IDisposable
    {
        private AzureBlobStorageProviderConfig azureBlobStorageProviderConfig { get; }
        private IAzureBlobContainerAdapter blobContainerAdapter;
        private bool _disposed;

        internal AzureBlobStorageProvider(AzureBlobStorageProviderConfig azureBlobStorageProviderConfig)
            : this(azureBlobStorageProviderConfig, CreateAdapter(azureBlobStorageProviderConfig))
        {
        }

        internal AzureBlobStorageProvider(AzureBlobStorageProviderConfig azureBlobStorageProviderConfig, IAzureBlobContainerAdapter blobContainerAdapter)
        {
            this.azureBlobStorageProviderConfig = azureBlobStorageProviderConfig;
            this.blobContainerAdapter = blobContainerAdapter;
        }

        public string GetName()
        {
            return azureBlobStorageProviderConfig.Name;
        }

        public new StorageFacadeType GetType()
        {
            return azureBlobStorageProviderConfig.StorageFacadeType;
        }

        public IEnumerable<StorageInfo> EnumerateStorageInfo(string logicalPath, bool isRecursive = false, StorageFilter storageFilter = null)
        {
            string prefix = $"{GetRelativePath(logicalPath)}/";
            if ("/".Equals(prefix))
                prefix = "";

            return azureBlobStorageProviderConfig.IsHierarchicalNamespace
                ? EnumerateHierarchical(prefix, isRecursive, storageFilter)
                : EnumerateFlat(prefix, isRecursive, storageFilter);
        }

        private IEnumerable<StorageInfo> EnumerateHierarchical(string prefix, bool isRecursive, StorageFilter storageFilter)
        {
            foreach (BlobHierarchyItem blobOrFolder in blobContainerAdapter.GetBlobsByHierarchy(prefix))
            {
                if (blobOrFolder.IsBlob)
                {
                    StorageInfo storageInfo = ToStorageInfoFromBlobName(blobOrFolder.Blob.Name);
                    if (StorageFilter.IsMatch(storageFilter, storageInfo))
                        yield return storageInfo;
                    continue;
                }

                if (!isRecursive || string.IsNullOrEmpty(blobOrFolder.Prefix))
                    continue;

                string childLogical = $"{GetName()}/{blobOrFolder.Prefix.TrimEnd('/')}";
                foreach (StorageInfo storageInfo in EnumerateStorageInfo(childLogical, true, storageFilter))
                    yield return storageInfo;
            }
        }

        private IEnumerable<StorageInfo> EnumerateFlat(string prefix, bool isRecursive, StorageFilter storageFilter)
        {
            int prefixDepth = prefix.Split('/').Length;
            foreach (BlobItem blobItem in blobContainerAdapter.GetBlobs(prefix))
            {
                if (!isRecursive && blobItem.Name.Split('/').Length > prefixDepth)
                    continue;

                StorageInfo storageInfo = ToStorageInfoFromBlobName(blobItem.Name);
                if (StorageFilter.IsMatch(storageFilter, storageInfo))
                    yield return storageInfo;
            }
        }

        private StorageInfo ToStorageInfoFromBlobName(string blobName)
        {
            BlobProperties blobProperties = blobContainerAdapter.GetBlobProperties(blobName);
            string name = CloudLogicalPath.GetLeafName(blobName);
            return GetStorageInfo(name, $"{GetName()}/{blobName}", blobProperties);
        }

        public StorageInfo GetStorageInfo(string logicalPath)
        {
            BlobProperties blobProperties = blobContainerAdapter.GetBlobProperties(GetRelativePath(logicalPath));
            string name = GetNameFromLogicalPath(logicalPath);
            return GetStorageInfo(name, logicalPath, blobProperties);
        }

        public async Task<Stream> ReadStorageAsync(StorageInfo storageInfo)
        {
            return await ReadStorageAsync(storageInfo.LogicalPath);
        }

        public async Task WriteStorageAsync(string logicalPath, Stream inputStream, bool createParentDirectories = false)
        {
            await blobContainerAdapter.UploadAsync(GetRelativePath(logicalPath), inputStream);
        }

        public async Task DeleteStorageAsync(string logicalPath)
        {
            await blobContainerAdapter.DeleteAsync(GetRelativePath(logicalPath));
        }

        public async Task<string> ComputeMD5Checksum(string logicalPath)
        {
            using var md5 = MD5.Create();
            using var stream = await ReadStorageAsync(logicalPath);
            return Convert.ToBase64String(md5.ComputeHash(stream));
        }

        private static IAzureBlobContainerAdapter CreateAdapter(AzureBlobStorageProviderConfig config)
        {
            BlobContainerClient containerClient;
            if (!string.IsNullOrEmpty(config.ConnectionString))
            {
                var blobServiceClient = new BlobServiceClient(config.ConnectionString);
                containerClient = blobServiceClient.GetBlobContainerClient(config.ContainerName);
            }
            else if (config.AccountKey != null)
            {
                BlobServiceClient blobServiceClient = new BlobServiceClient(config.BlobUri, new StorageSharedKeyCredential(config.AccountName, config.AccountKey));
                containerClient = blobServiceClient.GetBlobContainerClient(config.ContainerName);
            }
            else
            {
                containerClient = new BlobContainerClient(config.BlobUri);
            }

            return new AzureBlobContainerClientAdapter(containerClient);
        }

        private StorageInfo GetStorageInfo(string name, string logicalPath, BlobProperties blobProperties)
        {
            StorageInfo storageInfo = new StorageInfo();
            storageInfo.IsFile = true;
            storageInfo.Name = name;
            storageInfo.LogicalPath = logicalPath;
            storageInfo.Timestamp = blobProperties.LastModified.UtcDateTime;
            storageInfo.SizeBytes = blobProperties.ContentLength;

            return storageInfo;
        }

        private string GetNameFromLogicalPath(string logicalPath)
            => CloudLogicalPath.GetLeafName(logicalPath);

        private string GetRelativePath(string logicalPath)
            => CloudLogicalPath.GetRelativePath(GetName(), logicalPath);

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            (blobContainerAdapter as IDisposable)?.Dispose();
        }

        private async Task<Stream> ReadStorageAsync(string logicalPath)
        {
            string blobName = GetRelativePath(logicalPath);
            if (!await blobContainerAdapter.BlobExistsAsync(blobName))
                throw new FileNotFoundException($"Unable to find {logicalPath}", logicalPath);

            return await blobContainerAdapter.OpenReadAsync(blobName);
        }
    }
}
