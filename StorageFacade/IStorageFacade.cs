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

        /// <summary>
        /// Writes the given inputstream to the specified location, optionally creating parent directories or validating the checksum.
        /// </summary>
        /// <param name="logicalPath">The path to write to</param>
        /// <param name="inputStream">The stream to write from</param>
        /// <param name="createParentDirectories">Flags whether to create parent directories if they do not exist</param>
        /// <param name="validateChecksum">Flags whether or not to validate the checksum of the written file</param>
        /// <returns>The base64 encoded string of the MD5 checksum of the data that was written</returns>
        Task<string> WriteStorageAsync(string logicalPath, Stream inputStream, bool createParentDirectories=false, bool validateChecksum = false);

        Task DeleteStorageAsync(string logicalPath);
        
        Task<string> ComputeMD5Checksum(string logicalPath);
    }
}