// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage
{
    using System.Collections.Generic;

    /// <summary>
    /// Implements the storage provider configuration for Azure Blob Storage
    /// </summary>
    public class AzureBlobStorageProviderConfig : IStorageProviderConfig
    {
        public string Name { get; }

        public StorageFacadeType StorageFacadeType { get; }

        internal string AccessKey { get; }

        internal string AccountName { get; }

        internal string ContainerName { get; }

        public AzureBlobStorageProviderConfig(string accountName, string accessKey, string containerName)
        {
            this.StorageFacadeType = StorageFacadeType.AzureBlobStore;
            this.AccessKey = accessKey;
            this.AccountName = accountName;
            this.ContainerName = containerName;
            this.Name = $"{accessKey}|{accountName}|{containerName}";
        }
    }
}
