// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Implements the storage provider configuration for Azure Blob Storage
    /// </summary>
    public class AzureBlobStorageProviderConfig : IStorageProviderConfig
    {
        public string Name { get; }

        public StorageFacadeType StorageFacadeType { get; }

        internal Uri BlobUri { get; }

        internal string AccountName { get; }

        internal string AccountKey { get; }

        internal string ContainerName { get; }

        public AzureBlobStorageProviderConfig(string accountName, string accountKey, string containerName)
        {
            this.StorageFacadeType = StorageFacadeType.AzureBlobStore;
            this.BlobUri = new Uri($"https://{accountName}.storage.core.windows.net");
            this.AccountName = accountName;
            this.AccountKey = accountKey;
            this.ContainerName = containerName;
            this.Name = $"{BlobUri.ToString()}/{containerName}".ToLower();
        }

        public AzureBlobStorageProviderConfig(Uri blobUri, string containerName)
        {
            this.StorageFacadeType = StorageFacadeType.AzureBlobStore;
            this.BlobUri = blobUri;
            this.AccountName = GetAccountNameFromBlobUri(blobUri);
            this.ContainerName = containerName;
            this.Name = $"{BlobUri.ToString()}/{containerName}".ToLower();
        }

        // Internal

        private string GetAccountNameFromBlobUri(Uri blobUri)
        {
            return "";
        }
    }
}
