// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage
{
    using System;

    /// <summary>Configuration for the Azure Blob Storage provider.</summary>
    public class AzureBlobStorageProviderConfig : IStorageProviderConfig
    {
        /// <inheritdoc/>
        public string Name { get; }

        /// <inheritdoc/>
        public StorageFacadeType StorageFacadeType { get; }

        internal Uri BlobUri { get; }

        internal string AccountName { get; }

        internal string AccountKey { get; }

        internal string ContainerName { get; }

        internal bool IsHierarchicalNamespace { get; }

        /// <summary>
        /// Creates a configuration using account name/key credentials.
        /// <see cref="Name"/> is normalized to <c>https://{domainName}/{containerName}</c>.
        /// </summary>
        /// <param name="domainName">Blob endpoint host, e.g. <c>myaccount.blob.core.windows.net</c>.</param>
        /// <param name="accountKey">Storage account key.</param>
        /// <param name="containerName">Target container name.</param>
        /// <param name="isHierarchicalNamespace">When true, uses hierarchical (ADLS Gen2) listing.</param>
        public AzureBlobStorageProviderConfig(string domainName, string accountKey, string containerName, bool isHierarchicalNamespace = false)
        {
            this.StorageFacadeType = StorageFacadeType.AzureBlobStore;
            this.AccountName = domainName.Split(".")[0];
            this.AccountKey = accountKey;
            this.ContainerName = containerName;
            this.Name = $"https://{domainName}/{this.ContainerName}".ToLower();
            this.BlobUri = new Uri($"https://{domainName}".ToLower());
            this.IsHierarchicalNamespace = isHierarchicalNamespace;
        }

        /// <summary>
        /// Creates a configuration from a container SAS URI.
        /// </summary>
        /// <param name="blobUri">
        /// URI of the form
        /// <c>https://{account}.blob.core.windows.net/{container}/?{SASToken}</c>.
        /// </param>
        /// <param name="isHierarchicalNamespace">When true, uses hierarchical (ADLS Gen2) listing.</param>
        public AzureBlobStorageProviderConfig(Uri blobUri, bool isHierarchicalNamespace = false)
        {
            this.StorageFacadeType = StorageFacadeType.AzureBlobStore;
            this.BlobUri = blobUri;
            this.AccountName = GetAccountNameFromBlobUri(blobUri);
            this.ContainerName = GetContainerNameFromBlobUri(blobUri);
            string SASToken = this.GetSASTokenFromBlobUri(blobUri);
            this.Name = $"https://{this.AccountName}.blob.core.windows.net/{this.ContainerName}".ToLower();
            this.IsHierarchicalNamespace = isHierarchicalNamespace;
        }

        // Internal

        // This returns the account name from the blob Uri of the format: https://<account-name>.blob.core.windows.net/<container-name>/?<SASToken>
        private string GetAccountNameFromBlobUri(Uri blobUri)
        {
            return blobUri.ToString().Split("?")[0].Split("/")[2].Split(".")[0];
        }

        // This returns the account name from the blob Uri of the format: https://<account-name>.blob.core.windows.net/<container-name>/?<SASToken>
        private string GetContainerNameFromBlobUri(Uri blobUri)
        {
            return blobUri.ToString().Split("?")[0].Split("/")[3];
        }

        // This returns the account name from the blob Uri of the format: https://<account-name>.blob.core.windows.net/<container-name>/?<SASToken>
        private string GetSASTokenFromBlobUri(Uri blobUri)
        {
            return blobUri.ToString().Split("?")[1];
        }
    }
}
