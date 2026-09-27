// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    /// <summary>
    /// Unified storage API for listing, reading, writing, deleting, and checksumming objects
    /// across local files, SMB shares, Azure Blob Storage, Amazon S3, Google Cloud Storage,
    /// and Alibaba OSS. Obtain instances via <see cref="StorageFacadeFactory"/>.
    /// </summary>
    /// <remarks>
    /// Exception contract (all providers):
    /// <list type="bullet">
    /// <item><description>Missing object on enumerate / get / read / checksum → <see cref="StorageNotFoundException"/></description></item>
    /// <item><description>Missing object on delete → no-op (idempotent)</description></item>
    /// <item><description>Invalid arguments → <see cref="System.ArgumentException"/> family</description></item>
    /// <item><description>Cancel / dispose → <see cref="System.OperationCanceledException"/> / <see cref="System.ObjectDisposedException"/></description></item>
    /// <item><description>All other I/O, protocol, and SDK failures → <see cref="StorageFacadeException"/></description></item>
    /// </list>
    /// </remarks>
    public interface IStorageFacade
    {
        /// <summary>Returns the logical store name (URI or UNC prefix) for this facade.</summary>
        string GetName();

        /// <summary>Returns the backing store type for this facade.</summary>
        StorageFacadeType GetType();

        /// <summary>
        /// Lists file metadata under <paramref name="logicalPath"/>.
        /// </summary>
        /// <param name="logicalPath">Root path in the facade's logical namespace.</param>
        /// <param name="isRecursive">When true, includes nested paths.</param>
        /// <param name="storageFilter">Optional name/extension/date filter; null lists all files.</param>
        /// <exception cref="StorageNotFoundException">When the root path does not exist.</exception>
        /// <exception cref="StorageFacadeException">When the backend fails for a reason other than a missing path.</exception>
        IEnumerable<StorageInfo> EnumerateStorageInfo(string logicalPath, bool isRecursive = false, StorageFilter storageFilter = null);

        /// <summary>Returns metadata for a single object at <paramref name="logicalPath"/>.</summary>
        /// <exception cref="StorageNotFoundException">When the object does not exist.</exception>
        /// <exception cref="StorageFacadeException">When the backend fails for a reason other than a missing object.</exception>
        StorageInfo GetStorageInfo(string logicalPath);

        /// <summary>Opens a read stream for the object described by <paramref name="logicalPath"/>.</summary>
        /// <exception cref="StorageNotFoundException">When the object does not exist.</exception>
        /// <exception cref="StorageFacadeException">When the backend fails for a reason other than a missing object.</exception>
        Task<Stream> ReadStorageAsync(StorageInfo logicalPath);

        /// <summary>
        /// Writes <paramref name="inputStream"/> to the specified location, optionally creating parent directories or validating the checksum.
        /// </summary>
        /// <param name="logicalPath">Destination path in the facade's logical namespace.</param>
        /// <param name="inputStream">Stream to write.</param>
        /// <param name="createParentDirectories">When true, creates parent folders/containers as needed (provider-dependent).</param>
        /// <param name="validateChecksum">When true, recomputes MD5 after write and throws if it does not match.</param>
        /// <returns>Base64-encoded MD5 checksum of the bytes written.</returns>
        /// <exception cref="StorageFacadeException">When the write or checksum validation fails.</exception>
        Task<string> WriteStorageAsync(string logicalPath, Stream inputStream, bool createParentDirectories = false, bool validateChecksum = false);

        /// <summary>
        /// Deletes the object at <paramref name="logicalPath"/>.
        /// Missing objects are ignored (idempotent).
        /// </summary>
        /// <exception cref="StorageFacadeException">When the backend fails for a reason other than a missing object.</exception>
        Task DeleteStorageAsync(string logicalPath);

        /// <summary>Returns the base64-encoded MD5 checksum of the object at <paramref name="logicalPath"/>.</summary>
        /// <exception cref="StorageNotFoundException">When the object does not exist.</exception>
        /// <exception cref="StorageFacadeException">When the backend fails for a reason other than a missing object.</exception>
        Task<string> ComputeMD5Checksum(string logicalPath);
    }
}
