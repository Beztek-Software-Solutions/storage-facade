// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface defining the back-end requirements for the StorageFacade
    /// </summary>
    public interface IStorageProvider
    {
        string GetName();

        StorageFacadeType GetType();

        IEnumerable<StorageInfo> EnumerateStorageInfo(string rootPath, bool isRecursive = false, StorageFilter storageFilter = null);

        StorageInfo GetStorageInfo(string storagePath);

        Task<Stream> ReadStorageAsync(StorageInfo storageInfo);

        Task WriteStorageAsync(string storagePath, Stream inputStream, bool createParentDirectories=false);

        Task DeleteStorageAsync(string storagePath);

        Task<string> ComputeMD5Checksum(string logicalPath);
    }
}
