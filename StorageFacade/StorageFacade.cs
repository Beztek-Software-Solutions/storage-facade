// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage
{
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using Beztek.Facade.Storage.Providers;

    /// <summary>
    /// Interface defining the back-end requirements for the StorageFacade
    /// </summary>
    public class StorageFacade
    {
        private IStorageProvider storageProvider;

        public StorageFacade(IStorageProvider storageProvider)
        {
            this.storageProvider = storageProvider;
        }

        public IEnumerable<StorageInfo> EnumerateStorageInfo(string rootPath, bool isRecursive = false, StorageFilter storageFilter = null)
        {
            return storageProvider.EnumerateStorageInfo(rootPath, isRecursive, storageFilter);
        }

        public StorageInfo GetStorageInfo(string storagePath)
        {
            return storageProvider.GetStorageInfo(storagePath);
        }

        public async Task<Stream> ReadStorageAsync(StorageInfo storageInfo)
        {
            return await storageProvider.ReadStorageAsync(storageInfo).ConfigureAwait(false);
        }

        public async Task WriteStorageAsync(string storagePath, Stream inputStream)
        {
            await storageProvider.WriteStorageAsync(storagePath, inputStream).ConfigureAwait(false);
        }

        public async Task DeleteStorageAsync(string storagePath)
        {
            await storageProvider.DeleteStorageAsync(storagePath);
        }

        public static string ToString(StorageInfo storageInfo)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"{storageInfo.Path} {storageInfo.IsFile} {storageInfo.Timestamp} {storageInfo.MimeType} {storageInfo.SizeBytes}");
            return sb.ToString();
        }
    }
}
