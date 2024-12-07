// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage
{
    using System.Collections.Generic;
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
        public static IStorageFacade GetStorageFacade(IStorageProviderConfig storageProviderConfig)
        {
            IStorageFacade storageFacade = null;

            if (StorageFacadeType.LocalFileStore == storageProviderConfig.StorageFacadeType)
            {
                return new StorageFacade(new FileStorageProvider(new FileStorageProviderConfig()));
            }
            else if (StorageFacadeType.SMBNetworkStore == storageProviderConfig.StorageFacadeType)
            {
                return new StorageFacade(new SMBNetworkStorageProvider((SMBNetworkStorageProviderConfig)storageProviderConfig));
            }
            else if (StorageFacadeType.AzureBlobStore == storageProviderConfig.StorageFacadeType)
            {
                return new StorageFacade(new AzureBlobStorageProvider((AzureBlobStorageProviderConfig)storageProviderConfig));
            }
            else if (StorageFacadeType.AmazonS3Store == storageProviderConfig.StorageFacadeType)
            {
                return new StorageFacade(new AwsS3StorageProvider((AwsS3StorageProviderConfig)storageProviderConfig));
            }

            return storageFacade;
        }
    }
}
