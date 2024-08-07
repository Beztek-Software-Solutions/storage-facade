﻿// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Providers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Threading.Tasks;
    using Azure;
    using Azure.Storage;
    using Azure.Storage.Blobs;
    using Azure.Storage.Blobs.Models;
    using Beztek.Facade.Storage;

    /// <summary>
    /// Implements the storage provider for Azure Blob Storage
    /// </summary>
    internal class AzureBlobStorageProvider : IStorageProvider
    {
        private AzureBlobStorageProviderConfig azureBlobStorageProviderConfig { get; }
        private BlobContainerClient blobContainerClient;

        internal AzureBlobStorageProvider(AzureBlobStorageProviderConfig azureBlobStorageProviderConfig)
        {
            this.azureBlobStorageProviderConfig = azureBlobStorageProviderConfig;

            if (azureBlobStorageProviderConfig.AccountKey != null)
            {
                // Account Key and Account Name
                BlobServiceClient blobServiceClient = new BlobServiceClient(azureBlobStorageProviderConfig.BlobUri, new StorageSharedKeyCredential(azureBlobStorageProviderConfig.AccountName, azureBlobStorageProviderConfig.AccountKey));
                this.blobContainerClient = blobServiceClient!.GetBlobContainerClient(azureBlobStorageProviderConfig.ContainerName);
            }
            else
            {
                // SAS Token
                this.blobContainerClient = new BlobContainerClient(azureBlobStorageProviderConfig.BlobUri);
            }
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
                foreach (BlobHierarchyItem blobOrFolder in blobContainerClient.GetBlobsByHierarchy(prefix: prefix, delimiter: "/").AsEnumerable())
                {
                    // A hierarchical listing may return both virtual directories and blobs.
                    if (blobOrFolder.IsBlob)
                    {
                        BlobProperties blobProperties = blobContainerClient.GetBlobClient($"/{blobOrFolder.Blob.Name}").GetProperties();
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
                foreach (BlobItem blobItem in blobContainerClient.GetBlobs(BlobTraits.None, BlobStates.None, prefix).AsEnumerable())
                {
                    if (blobItem.Name.Split("/").Length > prefix.Split("/").Length)
                    {
                        if (!isRecursive)
                        {
                            // Continue processing only if listing recursively
                            continue;
                        }
                    }

                    BlobProperties blobProperties = blobContainerClient.GetBlobClient(blobItem.Name).GetProperties();
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
            BlobClient blobClient = GetBlobClient(logicalPath);
            BlobProperties blobProperties = blobClient.GetProperties();
            string name = GetNameFromLogicalPath(logicalPath);
            return GetStorageInfo(name, logicalPath, blobProperties);
        }

        public async Task<Stream> ReadStorageAsync(StorageInfo storageInfo)
        {
            return await ReadStorageAsync(storageInfo.LogicalPath);
        }

        public async Task WriteStorageAsync(string logicalPath, Stream inputStream, bool createParentDirectories = false)
        {
            // Get a reference to a blob
            BlobClient blobClient = GetBlobClient(logicalPath);

            // Upload data from the local file
            await blobClient.UploadAsync(inputStream, overwrite: true);
        }

        public async Task DeleteStorageAsync(string logicalPath)
        {
            // Get a reference to a blob
            BlobClient blobClient = GetBlobClient(logicalPath);
            // Delete the blob
            await blobClient.DeleteAsync();
        }

        public async Task<string> ComputeMD5Checksum(string logicalPath)
        {
            using var md5 = MD5.Create();
            using var stream = await ReadStorageAsync(logicalPath);
            return Convert.ToBase64String(md5.ComputeHash(stream));
        }

        // Internal

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

        // This returns the blob client from rom the logical path: https://<store-name>.blob.core.windows.net/<container-name>/<relative path>
        private BlobClient GetBlobClient(string logicalPath)
        {
            return blobContainerClient.GetBlobClient($"{GetRelativePath(logicalPath)}");
        }

        // This returns the relative path from the logical path: https://<store-name>.blob.core.windows.net/<container-name>/<relative path>
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
            BlobClient blobClient = GetBlobClient(logicalPath);
            if (!await blobClient.ExistsAsync())
                throw new Exception($"Unable to find {logicalPath}");

            var response = await blobClient.DownloadAsync();
            return await Task.FromResult(new StreamReader(response.Value.Content).BaseStream);
        }
    }
}
