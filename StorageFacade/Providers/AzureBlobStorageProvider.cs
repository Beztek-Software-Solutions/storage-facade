// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Providers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
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

            BlobServiceClient blobServiceClient;
            if (azureBlobStorageProviderConfig.AccountKey != null)
            {
                // Account Key and Account Name
                blobServiceClient = new BlobServiceClient(azureBlobStorageProviderConfig.BlobUri, new StorageSharedKeyCredential(azureBlobStorageProviderConfig.AccountName, azureBlobStorageProviderConfig.AccountKey));
            }
            else
            {
                // SAS Token
                blobServiceClient = new BlobServiceClient(azureBlobStorageProviderConfig.BlobUri);
            }
            this.blobContainerClient = blobServiceClient!.GetBlobContainerClient(azureBlobStorageProviderConfig.ContainerName);
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
            if (this.azureBlobStorageProviderConfig.IsHierarchicalNamespace)
            {
                foreach (BlobHierarchyItem blobOrFolder in blobContainerClient.GetBlobsByHierarchy(prefix: $"{GetRelativePath(logicalPath)}/", delimiter: "/"))
                {
                    if (blobOrFolder.IsBlob)
                    {
                        BlobProperties blobProperties = blobContainerClient.GetBlobClient($"/{blobOrFolder.Blob.Name}").GetProperties();
                        string path = blobOrFolder.Blob.Name;
                        string[] paths = path.Split("/");
                        string name = paths[paths.Length - 1];
                        StorageInfo storageInfo = GetStorageInfo(name, path, blobProperties);
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
                string prefix = $"{GetRelativePath(logicalPath)}/";
                foreach (BlobItem blobItem in blobContainerClient.GetBlobs(default, default, prefix).AsEnumerable())
                {
                    if (blobItem.Name.Split("/").Length > prefix.Split("/").Length)
                    {
                        // Continue processing only if listing recursively
                        if (isRecursive)
                        {
                            continue;
                        }
                        else
                        {
                            break;
                        }
                    }

                    BlobProperties blobProperties = blobContainerClient.GetBlobClient(blobItem.Name).GetProperties();
                    string path = blobItem.Name;
                    string[] paths = path.Split("/");
                    string name = paths[paths.Length - 1];
                    StorageInfo storageInfo = GetStorageInfo(name, path, blobProperties);
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

            return GetStorageInfo(logicalPath, logicalPath, blobProperties);
        }

        public async Task<Stream> ReadStorageAsync(StorageInfo storageInfo)
        {
            BlobClient blobClient = GetBlobClient(storageInfo.Name);
            if (!await blobClient.ExistsAsync())
                throw new Exception($"Unable to find {storageInfo.Name}");

            var response = await blobClient.DownloadAsync();
            return await Task.FromResult(new StreamReader(response.Value.Content).BaseStream);
        }

        public async Task WriteStorageAsync(string logicalPath, Stream inputStream, bool createParentDirectories = false)
        {
            // Get a reference to a blob
            BlobClient blobClient = GetBlobClient(logicalPath);
            // Upload data from the local file
            await blobClient.UploadAsync(inputStream, overwrite: true).ConfigureAwait(false);
        }

        public async Task DeleteStorageAsync(string logicalPath)
        {
            // Get a reference to a blob
            BlobClient blobClient = GetBlobClient(logicalPath);
            // Delete the blob
            await blobClient.DeleteAsync();
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

        // This returns the blob client from rom the logical path: https://<store-name>.blob.core.windows.net/<container-name>/<relative path>
        private BlobClient GetBlobClient(string logicalPath)
        {
            return blobContainerClient.GetBlobClient($"/{GetRelativePath(logicalPath)}");
        }

        // This returns the relative path from the logical path: https://<store-name>.blob.core.windows.net/<container-name>/<relative path>
        private string GetRelativePath(string logicalPath)
        {
            int prefixLength = GetName().Length + 1;
            return logicalPath[prefixLength..];
        }
    }

    internal class ResultContinuation
    {
    }
}
