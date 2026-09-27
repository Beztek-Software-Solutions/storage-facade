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
    /// <remarks>
    /// Public exception contract (enforced here for every provider):
    /// <list type="bullet">
    /// <item><description>Missing object on enumerate / get / read / checksum → <see cref="StorageNotFoundException"/></description></item>
    /// <item><description>Missing object on delete → no-op (idempotent)</description></item>
    /// <item><description>Invalid arguments / cancel / dispose → unchanged</description></item>
    /// <item><description>All other I/O, protocol, SDK, and checksum failures → <see cref="StorageFacadeException"/></description></item>
    /// </list>
    /// </remarks>
    public class StorageFacade : IStorageFacade, IDisposable
    {
        private readonly IStorageProvider storageProvider;
        private bool _disposed;

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
        {
            IEnumerator<StorageInfo> enumerator;
            try
            {
                enumerator = storageProvider.EnumerateStorageInfo(rootPath, isRecursive, storageFilter).GetEnumerator();
            }
            catch (Exception ex)
            {
                StorageExceptionPolicy.RethrowForEnumerate(rootPath, ex);
                throw;
            }

            try
            {
                while (true)
                {
                    bool moved;
                    try
                    {
                        moved = enumerator.MoveNext();
                    }
                    catch (Exception ex)
                    {
                        StorageExceptionPolicy.RethrowForEnumerate(rootPath, ex);
                        throw;
                    }

                    if (!moved)
                        yield break;

                    yield return enumerator.Current;
                }
            }
            finally
            {
                enumerator.Dispose();
            }
        }

        /// <inheritdoc/>
        public StorageInfo GetStorageInfo(string storagePath)
        {
            try
            {
                return storageProvider.GetStorageInfo(storagePath);
            }
            catch (Exception ex)
            {
                StorageExceptionPolicy.RethrowForRead(storagePath, ex);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<Stream> ReadStorageAsync(StorageInfo storageInfo)
        {
            try
            {
                return await storageProvider.ReadStorageAsync(storageInfo).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                StorageExceptionPolicy.RethrowForRead(storageInfo?.LogicalPath, ex);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<string> WriteStorageAsync(string storagePath, Stream inputStream, bool createParentDirectories = false, bool validateChecksum = false)
        {
            try
            {
                HashAlgorithm hashAlgorithm = MD5.Create();
                Stream stream = new CryptoStream(inputStream, hashAlgorithm, CryptoStreamMode.Read, true);
                await storageProvider.WriteStorageAsync(storagePath, stream, createParentDirectories).ConfigureAwait(false);
                string inputChecksum = Convert.ToBase64String(hashAlgorithm.Hash);

                if (validateChecksum)
                {
                    string outputChecksum = await storageProvider.ComputeMD5Checksum(storagePath).ConfigureAwait(false);
                    if (!inputChecksum.Equals(outputChecksum))
                    {
                        throw new StorageFacadeException(
                            $"Output checksum ({outputChecksum}) does not match the input checksum ({inputChecksum})");
                    }
                }

                return inputChecksum;
            }
            catch (Exception ex)
            {
                StorageExceptionPolicy.RethrowForWrite(storagePath, ex);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task DeleteStorageAsync(string storagePath)
        {
            try
            {
                await storageProvider.DeleteStorageAsync(storagePath).ConfigureAwait(false);
            }
            catch (Exception ex) when (StorageExceptionPolicy.IsMissing(ex))
            {
                // Idempotent: File.Delete and S3 DeleteObject succeed when the key is already gone.
            }
            catch (Exception ex)
            {
                StorageExceptionPolicy.RethrowForDelete(storagePath, ex);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<string> ComputeMD5Checksum(string storagePath)
        {
            try
            {
                return await storageProvider.ComputeMD5Checksum(storagePath).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                StorageExceptionPolicy.RethrowForRead(storagePath, ex);
                throw;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            (storageProvider as IDisposable)?.Dispose();
        }
    }
}
