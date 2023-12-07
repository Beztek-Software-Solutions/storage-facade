// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    /// <summary>
    /// The Combo Storage Provider combines multiple stores and the Local File Store to provide a seamless experience across all file stores and mounted files
    /// </summary>
    public class ComboStorageFacade : IStorageFacade
    {
        private Dictionary<string, IStorageFacade> _storageProviders = new Dictionary<string, IStorageFacade>();
        private IStorageFacade _defaultStorageFacade;

        public ComboStorageFacade(List<IStorageFacade> storageFacades)
        {
            foreach (IStorageFacade storageFacade in storageFacades)
            {
                _storageProviders[storageFacade.GetName()] = storageFacade;
            }
            _defaultStorageFacade = StorageFacadeFactory.GetStorageFacade(new FileStorageProviderConfig());
        }

        public string GetName()
        {
            return "ComboProvider";
        }

        public new StorageFacadeType GetType()
        {
            return StorageFacadeType.ComboStore;
        }

        public IEnumerable<StorageInfo> EnumerateStorageInfo(string logicalPath, bool isRecursive = false, StorageFilter storageFilter = null)
        {
            return GetStorageFacade(logicalPath).EnumerateStorageInfo(logicalPath, isRecursive, storageFilter);
        }

        public StorageInfo GetStorageInfo(string logicalPath)
        {
            return GetStorageFacade(logicalPath).GetStorageInfo(logicalPath);
        }

        public async Task<Stream> ReadStorageAsync(StorageInfo storageInfo)
        {
            return await GetStorageFacade(storageInfo.LogicalPath).ReadStorageAsync(storageInfo).ConfigureAwait(false);
        }

        public async Task WriteStorageAsync(string logicalPath, Stream inputStream, bool createParentDirectories=false)
        {
            await GetStorageFacade(logicalPath).WriteStorageAsync(logicalPath, inputStream, createParentDirectories).ConfigureAwait(false);
        }

        public async Task DeleteStorageAsync(string logicalPath)
        {
            await GetStorageFacade(logicalPath).DeleteStorageAsync(logicalPath);
        }

        // Internal

        IStorageFacade GetStorageFacade(string logicalPath)
        {
            foreach (KeyValuePair<string, IStorageFacade> entry in _storageProviders)
            {
                string key = entry.Key;
                IStorageFacade value = entry.Value;
                if (logicalPath.ToLower().StartsWith(key))
                {
                    return entry.Value;
                }
            }
            return _defaultStorageFacade;
        }
    }
}
