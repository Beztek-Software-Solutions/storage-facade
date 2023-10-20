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
    public class AzureBlobStorageProvider : IStorageProvider
    {
        AzureBlobStorageProviderConfig AzureBlobStorageProviderConfig { get; }
        private BlobContainerClient BlobContainerClient;

        public AzureBlobStorageProvider(AzureBlobStorageProviderConfig azureBlobStorageProviderConfig)
        {
            this.AzureBlobStorageProviderConfig = azureBlobStorageProviderConfig;

            BlobServiceClient blobServiceClient = new BlobServiceClient(new Uri($"https://{azureBlobStorageProviderConfig.AccountName}.blob.core.windows.net"), new StorageSharedKeyCredential(azureBlobStorageProviderConfig.AccountName, azureBlobStorageProviderConfig.AccessKey));
            this.BlobContainerClient = blobServiceClient.GetBlobContainerClient(azureBlobStorageProviderConfig.ContainerName);
        }

        public IEnumerable<StorageInfo> EnumerateStorageInfo(string rootPath, bool isRecursive = false, StorageFilter storageFilter = null)
        {
            foreach (BlobHierarchyItem blobOrFolder in BlobContainerClient.GetBlobsByHierarchy(prefix: rootPath, delimiter: "/"))
            {
                if (blobOrFolder.IsBlob)
                {
                    BlobProperties blobProperties = BlobContainerClient.GetBlobClient(blobOrFolder.Blob.Name).GetProperties();
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

        public StorageInfo GetStorageInfo(string storagePath)
        {
            BlobClient blobClient = BlobContainerClient.GetBlobClient(storagePath);
            BlobProperties blobProperties = blobClient.GetProperties();

            return GetStorageInfo(storagePath, storagePath, blobProperties);
        }

        public async Task<Stream> ReadStorageAsync(StorageInfo storageInfo)
        {
            BlobClient blobClient = BlobContainerClient.GetBlobClient(storageInfo.Name);
            if (!await blobClient.ExistsAsync())
                throw new Exception($"Unable to find {storageInfo.Name}");

            var response = await blobClient.DownloadAsync();
            return await Task.FromResult(new StreamReader(response.Value.Content).BaseStream);
        }

        public async Task WriteStorageAsync(string storagePath, Stream inputStream)
        {
            // Get a reference to a blob
            BlobClient blobClient = BlobContainerClient.GetBlobClient(storagePath);
            // Upload data from the local file
            await blobClient.UploadAsync(inputStream, overwrite: true).ConfigureAwait(false);
        }

        public async Task DeleteStorageAsync(string storagePath)
        {
            // Get a reference to a blob
            BlobClient blobClient = BlobContainerClient.GetBlobClient(storagePath);
            // Delete the blob
            await blobClient.DeleteAsync();
        }

        // Internal

        private StorageInfo GetStorageInfo(string name, string path, BlobProperties blobProperties)
        {
            StorageInfo storageInfo = new StorageInfo();
            storageInfo.IsFile = true;
            storageInfo.Name = name;
            storageInfo.Path = path;
            storageInfo.Timestamp = blobProperties.LastModified.UtcDateTime;
            storageInfo.SizeBytes = blobProperties.ContentLength;

            return storageInfo;
        }
    }
}
