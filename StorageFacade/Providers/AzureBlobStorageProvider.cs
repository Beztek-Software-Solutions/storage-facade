// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Providers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
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
                blobServiceClient = new BlobServiceClient(azureBlobStorageProviderConfig.BlobUri, null);
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
            foreach (BlobHierarchyItem blobOrFolder in blobContainerClient.GetBlobsByHierarchy(prefix: logicalPath, delimiter: "/"))
            {
                if (blobOrFolder.IsBlob)
                {
                    BlobProperties blobProperties = blobContainerClient.GetBlobClient(blobOrFolder.Blob.Name).GetProperties();
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

        public StorageInfo GetStorageInfo(string logicalPath)
        {
            BlobClient blobClient = blobContainerClient.GetBlobClient(logicalPath);
            BlobProperties blobProperties = blobClient.GetProperties();

            return GetStorageInfo(logicalPath, logicalPath, blobProperties);
        }

        public async Task<Stream> ReadStorageAsync(StorageInfo storageInfo)
        {
            BlobClient blobClient = blobContainerClient.GetBlobClient(storageInfo.Name);
            if (!await blobClient.ExistsAsync())
                throw new Exception($"Unable to find {storageInfo.Name}");

            var response = await blobClient.DownloadAsync();
            return await Task.FromResult(new StreamReader(response.Value.Content).BaseStream);
        }

        public async Task WriteStorageAsync(string logicalPath, Stream inputStream, bool createParentDirectories=false)
        {
            // Get a reference to a blob
            BlobClient blobClient = blobContainerClient.GetBlobClient(logicalPath);
            // Upload data from the local file
            await blobClient.UploadAsync(inputStream, overwrite: true).ConfigureAwait(false);
        }

        public async Task DeleteStorageAsync(string logicalPath)
        {
            // Get a reference to a blob
            BlobClient blobClient = blobContainerClient.GetBlobClient(logicalPath);
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
    }
}
