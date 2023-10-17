// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Providers
{
    using Beztek.Facade.Storage;

    /// <summary>
    /// Implements the storage provider configuration for Azure Blob Storage
    /// </summary>
    internal class AzureBlobStorageProviderConfig : IStorageProviderConfig
    {
        private string v;

        public string Name { get; }

        public StorageProviderType StorageProviderType { get; }

        public string AccessKey { get; }

        public string AccountName { get; }

        public string ContainerName { get; }

        public AzureBlobStorageProviderConfig(string name, string accountName, string accessKey, string containerName)
        {
            this.Name = name;
            this.StorageProviderType = StorageProviderType.AzureBlobStore;
            this.AccessKey = accessKey;
            this.AccountName = accountName;
            this.ContainerName = containerName;
        }

        public AzureBlobStorageProviderConfig(string v)
        {
            this.v = v;
        }
    }
}
