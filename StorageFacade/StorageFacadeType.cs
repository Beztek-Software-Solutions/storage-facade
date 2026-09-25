// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage
{
    /// <summary>Supported backing store kinds for <see cref="IStorageFacade"/>.</summary>
    public enum StorageFacadeType
    {
        /// <summary>Local filesystem paths.</summary>
        LocalFileStore,
        /// <summary>SMB/CIFS network share (UNC paths).</summary>
        SMBNetworkStore,
        /// <summary>Azure Blob Storage container.</summary>
        AzureBlobStore,
        /// <summary>Amazon S3 bucket.</summary>
        AmazonS3Store,
        /// <summary>Google Cloud Storage bucket.</summary>
        GoogleCloudStorageStore,
        /// <summary>Alibaba Cloud Object Storage Service (OSS) bucket.</summary>
        AlibabaOssStore,
        /// <summary>Prefix-routed combination of multiple stores.</summary>
        ComboStore
    }
}
