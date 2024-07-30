// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using Beztek.Facade.Storage.Providers;

    /// <summary>
    /// Interface defining the back-end requirements for the StorageFacade
    /// </summary>
    public class StorageFacade : IStorageFacade
    {
        private IStorageProvider storageProvider;

        public StorageFacade(IStorageProvider storageProvider)
        {
            this.storageProvider = storageProvider;
        }

        public string GetName()
        {
            return storageProvider.GetName();
        }

        public new StorageFacadeType GetType()
        {
            return storageProvider.GetType();
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

        public async Task WriteStorageAsync(string storagePath, Stream inputStream, bool createParentDirectories=false, bool validateChecksum = false)
        {
            HashAlgorithm? hashAlgorithm = MD5.Create();
            Stream stream = validateChecksum ? new CryptoStream(inputStream, hashAlgorithm, CryptoStreamMode.Read, true) : inputStream;
            await storageProvider.WriteStorageAsync(storagePath, stream, createParentDirectories);

            // validate the checksum
            string inputChecksum = validateChecksum ? Convert.ToBase64String(hashAlgorithm.Hash) : null;
            string outputChecksum = validateChecksum ? await storageProvider.ComputeMD5Checksum(storagePath) : null;
            if (validateChecksum && !inputChecksum.Equals(outputChecksum))
            {
                throw new Exception($"Output checksum ({outputChecksum}) does not match the input checksum ({inputChecksum})");
            }
        }

        public async Task DeleteStorageAsync(string storagePath)
        {
            await storageProvider.DeleteStorageAsync(storagePath);
        }

        public async Task<string> ComputeMD5Checksum(string storagePath)
        {
            return await storageProvider.ComputeMD5Checksum(storagePath);
        }
    }
}
