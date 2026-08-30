// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Security.Cryptography;
    using System.Threading.Tasks;

    /// <summary>
    /// Default <see cref="IStorageFacade"/> implementation that wraps an <see cref="IStorageProvider"/>
    /// and computes MD5 checksums on write.
    /// </summary>
    public class StorageFacade : IStorageFacade
    {
        private IStorageProvider storageProvider;

        /// <summary>Creates a facade over the given provider.</summary>
        public StorageFacade(IStorageProvider storageProvider)
        {
            this.storageProvider = storageProvider;
        }

        /// <inheritdoc/>
        public string GetName() => storageProvider.GetName();

        /// <inheritdoc/>
        public new StorageFacadeType GetType() => storageProvider.GetType();

        /// <inheritdoc/>
        public IEnumerable<StorageInfo> EnumerateStorageInfo(string rootPath, bool isRecursive = false, StorageFilter storageFilter = null)
            => storageProvider.EnumerateStorageInfo(rootPath, isRecursive, storageFilter);

        /// <inheritdoc/>
        public StorageInfo GetStorageInfo(string storagePath) => storageProvider.GetStorageInfo(storagePath);

        /// <inheritdoc/>
        public async Task<Stream> ReadStorageAsync(StorageInfo storageInfo)
            => await storageProvider.ReadStorageAsync(storageInfo).ConfigureAwait(false);

        /// <inheritdoc/>
        public async Task<string> WriteStorageAsync(string storagePath, Stream inputStream, bool createParentDirectories = false, bool validateChecksum = false)
        {
            HashAlgorithm hashAlgorithm = MD5.Create();
            Stream stream = new CryptoStream(inputStream, hashAlgorithm, CryptoStreamMode.Read, true);
            await storageProvider.WriteStorageAsync(storagePath, stream, createParentDirectories);
            string inputChecksum = Convert.ToBase64String(hashAlgorithm.Hash);

            if (validateChecksum)
            {
                string outputChecksum = await storageProvider.ComputeMD5Checksum(storagePath);
                if (!inputChecksum.Equals(outputChecksum))
                {
                    throw new Exception($"Output checksum ({outputChecksum}) does not match the input checksum ({inputChecksum})");
                }
            }

            return inputChecksum;
        }

        /// <inheritdoc/>
        public async Task DeleteStorageAsync(string storagePath)
            => await storageProvider.DeleteStorageAsync(storagePath);

        /// <inheritdoc/>
        public async Task<string> ComputeMD5Checksum(string storagePath)
            => await storageProvider.ComputeMD5Checksum(storagePath);
    }
}
