// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    /// <summary>
    /// The Combo Storage Provider combines multiple SMBNetworkStores and the Local File Store to provide a seamless experience across SMB and mounted file stores
    /// </summary>
    public class ComboStorageFacade : IStorageFacade
    {
        private Dictionary<string, IStorageFacade> _storageProviders = new Dictionary<string, IStorageFacade>();
        private IStorageFacade _defaultStorageFacade;

        public ComboStorageFacade(List<IStorageFacade> smbStorageFacades)
        {
            foreach (IStorageFacade smbFacade in smbStorageFacades)
            {
                _storageProviders[smbFacade.GetName()] = smbFacade;
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

        public async Task WriteStorageAsync(string logicalPath, Stream inputStream)
        {
            await GetStorageFacade(logicalPath).WriteStorageAsync(logicalPath, inputStream).ConfigureAwait(false);
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
                if (value.GetType() != StorageFacadeType.SMBNetworkStore)
                {
                    throw new ArgumentException("Combo storage facade can only have SMB Facades in the constructor");
                }
                if (logicalPath.ToLower().StartsWith(key))
                {
                    return entry.Value;
                }
            }
            return _defaultStorageFacade;
        }
    }
}
