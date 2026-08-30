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
    internal class AzureBlobStorageProvider : IStorageProvider
    {
        private AzureBlobStorageProviderConfig azureBlobStorageProviderConfig { get; }
        private IAzureBlobContainerAdapter blobContainerAdapter;

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
            if ("/".Equals(prefix)) prefix = "";
            if (this.azureBlobStorageProviderConfig.IsHierarchicalNamespace)
            {
                foreach (BlobHierarchyItem blobOrFolder in blobContainerAdapter.GetBlobsByHierarchy(prefix))
                {
                    if (blobOrFolder.IsBlob)
                    {
                        BlobProperties blobProperties = blobContainerAdapter.GetBlobProperties(blobOrFolder.Blob.Name);
                        string path = blobOrFolder.Blob.Name;
                        string[] paths = path.Split("/");
                        string name = paths[paths.Length - 1];
                        StorageInfo storageInfo = GetStorageInfo(name, $"{GetName()}/{path}", blobProperties);
                        if (StorageFilter.IsMatch(storageFilter, storageInfo))
                            yield return storageInfo;
                    }
                    else if (isRecursive)
                    {
                        foreach (StorageInfo storageInfo in EnumerateStorageInfo(blobOrFolder.Prefix, true, storageFilter))
                        {
                            yield return storageInfo;
                        }
                    }
                }
                yield break;
            }
            else
            {
                foreach (BlobItem blobItem in blobContainerAdapter.GetBlobs(prefix))
                {
                    if (blobItem.Name.Split("/").Length > prefix.Split("/").Length)
                    {
                        if (!isRecursive)
                        {
                            continue;
                        }
                    }

                    BlobProperties blobProperties = blobContainerAdapter.GetBlobProperties(blobItem.Name);
                    string path = blobItem.Name;
                    string[] paths = path.Split("/");
                    string name = paths[paths.Length - 1];
                    StorageInfo storageInfo = GetStorageInfo(name, $"{GetName()}/{path}", blobProperties);
                    if (StorageFilter.IsMatch(storageFilter, storageInfo))
                        yield return storageInfo;
                }
                yield break;
            }
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
            if (config.AccountKey != null)
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
        {
            int index = logicalPath.LastIndexOf("/") + 1;
            return logicalPath[index..];
        }

        private string GetRelativePath(string logicalPath)
        {
            if (!logicalPath.EndsWith("/")) logicalPath = $"{logicalPath}/";
            int uriLength = GetName().Length;
            int logicalPathLength = logicalPath.Length;
            string currPath = logicalPath.Substring(uriLength + 1, logicalPathLength - uriLength - 1);
            if (currPath.StartsWith("/")) currPath = currPath[1..];
            if (currPath.EndsWith("/")) currPath = currPath[..^1];
            return currPath;
        }

        private async Task<Stream> ReadStorageAsync(string logicalPath)
        {
            string blobName = GetRelativePath(logicalPath);
            if (!await blobContainerAdapter.BlobExistsAsync(blobName))
                throw new Exception($"Unable to find {logicalPath}");

            return await blobContainerAdapter.OpenReadAsync(blobName);
        }
    }
}
