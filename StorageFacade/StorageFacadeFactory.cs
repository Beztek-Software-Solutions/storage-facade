// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage
{
    using Beztek.Facade.Storage.Providers;

    /// <summary>
    /// QueueClientFactory.
    /// </summary>
    public static class StorageFacadeFactory
    {
        /// <summary>
        /// Gets an instance of a StorageFacade based on the provider config provided.
        /// </summary>
        /// <param name="storageProviderConfig"></param>
        /// <returns></returns>
        public static StorageFacade GetStorageFacade(IStorageProviderConfig storageProviderConfig)
        {
            StorageFacade storageFacade = null;

            if (StorageProviderType.LocalFileStore == storageProviderConfig.StorageProviderType)
            {
                return new StorageFacade(new FileStorageProvider((FileStorageProviderConfig)storageProviderConfig));
            }
            else if (StorageProviderType.SMBNetworkStore == storageProviderConfig.StorageProviderType)
            {
                return new StorageFacade(new SMBNetworkStorageProvider((SMBNetworkStorageProviderConfig)storageProviderConfig));
            }
            else if (StorageProviderType.AzureBlobStore == storageProviderConfig.StorageProviderType)
            {
                return new StorageFacade(new AzureBlobStorageProvider((AzureBlobStorageProviderConfig)storageProviderConfig));
            }

            return storageFacade;
        }
    }
}
