// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    /// <summary>
    /// Routes storage operations to one of several configured facades by matching the path prefix,
    /// falling back to the local file store when no prefix matches.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each child facade registered in the constructor is indexed by its <see cref="IStorageFacade.GetName"/>
    /// value, which must match the prefix of logical paths for that store (e.g. <c>s3://bucket</c>,
    /// <c>https://account.blob.core.windows.net/container</c>, <c>\\server\share</c>,
    /// <c>gs://bucket</c>, <c>oss://bucket</c>).
    /// </para>
    /// <para>
    /// Routing uses the first registered facade where
    /// <c>logicalPath.StartsWith(entry.Key, OrdinalIgnoreCase)</c>. Registration order matters when prefixes
    /// overlap; this is not a longest-prefix match.
    /// </para>
    /// <para>
    /// A default local-file facade is always created via
    /// <see cref="StorageFacadeFactory.GetStorageFacade"/> and <see cref="FileStorageProviderConfig"/>.
    /// It is not part of the prefix table; it handles any path that does not start with a registered
    /// prefix (typical OS paths such as <c>/tmp/file</c> or <c>C:\data\file</c>).
    /// </para>
    /// <para>
    /// Exception behavior matches <see cref="IStorageFacade"/>: child facades from
    /// <see cref="StorageFacadeFactory"/> already normalize provider/SDK failures.
    /// </para>
    /// </remarks>
    public class ComboStorageFacade : IStorageFacade, IDisposable
    {
        private readonly List<KeyValuePair<string, IStorageFacade>> _storageProviders = new();
        private readonly IStorageFacade _defaultStorageFacade;
        private bool _disposed;

        /// <summary>
        /// Registers the given facades for prefix-based routing and initializes the default local-file fallback.
        /// </summary>
        /// <param name="storageFacades">
        /// Child facades to register. Each <see cref="IStorageFacade.GetName"/> becomes a routing prefix.
        /// Order matters when prefixes overlap (first match wins).
        /// </param>
        public ComboStorageFacade(List<IStorageFacade> storageFacades)
        {
            foreach (IStorageFacade storageFacade in storageFacades)
            {
                _storageProviders.Add(new KeyValuePair<string, IStorageFacade>(
                    storageFacade.GetName(), storageFacade));
            }
            _defaultStorageFacade = StorageFacadeFactory.GetStorageFacade(new FileStorageProviderConfig());
        }

        /// <inheritdoc/>
        public string GetName() => "ComboProvider";

        /// <inheritdoc/>
        public new StorageFacadeType GetType() => StorageFacadeType.ComboStore;

        /// <inheritdoc/>
        public IEnumerable<StorageInfo> EnumerateStorageInfo(string logicalPath, bool isRecursive = false, StorageFilter storageFilter = null)
            => GetStorageFacade(logicalPath).EnumerateStorageInfo(logicalPath, isRecursive, storageFilter);

        /// <inheritdoc/>
        public StorageInfo GetStorageInfo(string logicalPath)
            => GetStorageFacade(logicalPath).GetStorageInfo(logicalPath);

        /// <inheritdoc/>
        public async Task<Stream> ReadStorageAsync(StorageInfo storageInfo)
            => await GetStorageFacade(storageInfo.LogicalPath).ReadStorageAsync(storageInfo).ConfigureAwait(false);

        /// <inheritdoc/>
        public async Task<string> WriteStorageAsync(string logicalPath, Stream inputStream, bool createParentDirectories = false, bool validateHash = false)
            => await GetStorageFacade(logicalPath).WriteStorageAsync(logicalPath, inputStream, createParentDirectories, validateHash);

        /// <inheritdoc/>
        public async Task DeleteStorageAsync(string logicalPath)
            => await GetStorageFacade(logicalPath).DeleteStorageAsync(logicalPath);

        /// <inheritdoc/>
        public async Task<string> ComputeMD5Checksum(string storagePath)
            => await GetStorageFacade(storagePath).ComputeMD5Checksum(storagePath);

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            foreach (KeyValuePair<string, IStorageFacade> entry in _storageProviders)
                (entry.Value as IDisposable)?.Dispose();
            (_defaultStorageFacade as IDisposable)?.Dispose();
        }

        IStorageFacade GetStorageFacade(string logicalPath)
        {
            foreach (KeyValuePair<string, IStorageFacade> entry in _storageProviders)
            {
                if (logicalPath.StartsWith(entry.Key, StringComparison.OrdinalIgnoreCase))
                    return entry.Value;
            }
            return _defaultStorageFacade;
        }
    }
}
