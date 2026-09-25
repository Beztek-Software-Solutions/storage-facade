// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage
{
    using Beztek.Facade.Storage.Providers;

    /// <summary>
    /// Factory for <see cref="IStorageFacade"/> instances backed by a configured provider.
    /// </summary>
    public static class StorageFacadeFactory
    {
        /// <summary>
        /// Creates a storage facade for the given provider configuration.
        /// </summary>
        /// <param name="storageProviderConfig">Provider-specific configuration (local, SMB, Azure, S3, GCS, or OSS).</param>
        /// <returns>A facade wrapping the selected provider.</returns>
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
            else if (StorageFacadeType.GoogleCloudStorageStore == storageProviderConfig.StorageFacadeType)
            {
                return new StorageFacade(new GoogleCloudStorageProvider((GoogleCloudStorageProviderConfig)storageProviderConfig));
            }
            else if (StorageFacadeType.AlibabaOssStore == storageProviderConfig.StorageFacadeType)
            {
                return new StorageFacade(new AlibabaOssStorageProvider((AlibabaOssStorageProviderConfig)storageProviderConfig));
            }

            return storageFacade;
        }
    }
}
