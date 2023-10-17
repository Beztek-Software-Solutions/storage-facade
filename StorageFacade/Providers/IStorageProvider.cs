// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Providers
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface defining the back-end requirements for the StorageFacade
    /// </summary>
    public interface IStorageProvider
    {
        IEnumerable<StorageInfo> EnumerateStorageInfo(string rootPath, bool isRecursive = false, StorageFilter storageFilter = null);

        StorageInfo GetStorageInfo(string storagePath);

        Task<Stream> ReadStorageAsync(StorageInfo storageInfo);

        Task WriteStorageAsync(string storagePath, Stream inputStream);

        Task DeleteStorageAsync(string storagePath);
    }
}
