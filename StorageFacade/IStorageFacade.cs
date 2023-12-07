// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    public interface IStorageFacade
    {
        public string GetName();
        
        StorageFacadeType GetType();

        IEnumerable<StorageInfo> EnumerateStorageInfo(string logicalPath, bool isRecursive = false, StorageFilter storageFilter = null);

        StorageInfo GetStorageInfo(string logicalPath);

        Task<Stream> ReadStorageAsync(StorageInfo logicalPath);

        Task WriteStorageAsync(string logicalPath, Stream inputStream, bool createParentDirectories=false);

        Task DeleteStorageAsync(string logicalPath);
    }
}