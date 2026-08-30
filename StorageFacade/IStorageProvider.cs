// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    /// <summary>
    /// Provider contract implemented by each backing store (local disk, SMB, Azure Blob, S3).
    /// Application code should use <see cref="IStorageFacade"/> instead.
    /// </summary>
    public interface IStorageProvider
    {
        /// <summary>Returns the logical store name for this provider.</summary>
        string GetName();

        /// <summary>Returns the store type for this provider.</summary>
        StorageFacadeType GetType();

        /// <summary>Lists objects under <paramref name="rootPath"/>.</summary>
        IEnumerable<StorageInfo> EnumerateStorageInfo(string rootPath, bool isRecursive = false, StorageFilter storageFilter = null);

        /// <summary>Returns metadata for a single object.</summary>
        StorageInfo GetStorageInfo(string storagePath);

        /// <summary>Opens a read stream for <paramref name="storageInfo"/>.</summary>
        Task<Stream> ReadStorageAsync(StorageInfo storageInfo);

        /// <summary>Writes <paramref name="inputStream"/> to <paramref name="storagePath"/>.</summary>
        Task WriteStorageAsync(string storagePath, Stream inputStream, bool createParentDirectories = false);

        /// <summary>Deletes the object at <paramref name="storagePath"/>.</summary>
        Task DeleteStorageAsync(string storagePath);

        /// <summary>Returns the base64-encoded MD5 checksum for <paramref name="logicalPath"/>.</summary>
        Task<string> ComputeMD5Checksum(string logicalPath);
    }
}
