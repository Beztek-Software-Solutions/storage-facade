// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage
{
    public enum StorageFacadeType
    {
        LocalFileStore,
        SMBNetworkStore,
        AzureBlobStore,
        AmazonS3Store,
        ComboStore
    }
}
