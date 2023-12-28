// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Azure.Storage;

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

        internal bool IsHierarchicalNamespace { get; }

        public AzureBlobStorageProviderConfig(string accountName, string accountKey, string containerName, bool isHierarchicalNamespace = false)
        {
            this.StorageFacadeType = StorageFacadeType.AzureBlobStore;
            this.AccountName = accountName;
            this.AccountKey = accountKey;
            this.ContainerName = containerName;
            this.Name = $"https://{this.AccountName}.blob.core.windows.net/{this.ContainerName}".ToLower();
            this.BlobUri = new Uri( $"https://{this.AccountName}.blob.core.windows.net");
            this.IsHierarchicalNamespace = isHierarchicalNamespace;
        }

        /// <summary>
        /// Creates a configuration object for Azure Blob Storage Provider
        /// </summary>
        /// <param name="blobUri">of the format: https://<account-name>.blob.core.windows.net/<container-name>/?<SASToken></param>
        /// <param name="containerName">See the format of the blob url above. The container name is a part of the blob url</param>
        public AzureBlobStorageProviderConfig(Uri blobUri, bool isHierarchicalNamespace = false)
        {
            this.StorageFacadeType = StorageFacadeType.AzureBlobStore;
            this.AccountName = GetAccountNameFromBlobUri(blobUri);
            this.ContainerName = GetContainerNameFromBlobUri(blobUri);
            this.Name = $"https://{this.AccountName}.blob.core.windows.net/{this.ContainerName}".ToLower();
            this.BlobUri = new Uri( $"https://{this.AccountName}.blob.core.windows.net");
            this.IsHierarchicalNamespace = isHierarchicalNamespace;
        }

        // Internal

        // This returns the account name from the blob Uri of the format: https://<account-name>.blob.core.windows.net/<container-name>/?<SASToken>
        private string GetAccountNameFromBlobUri(Uri blobUri)
        {
            return blobUri.ToString().Split("?")[0].Split("/")[^3].Split(".")[0];
        }
        
        // This returns the account name from the blob Uri of the format: https://<account-name>.blob.core.windows.net/<container-name>/?<SASToken>
        private string GetContainerNameFromBlobUri(Uri blobUri)
        {
            return blobUri.ToString().Split("?")[0].Split("/")[^2];
        }
    }
}
