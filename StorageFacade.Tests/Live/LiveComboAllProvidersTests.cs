// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Tests.Live
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using NUnit.Framework;

    /// <summary>
    /// When two or more remote providers are selected, builds one
    /// <see cref="ComboStorageFacade"/> that registers all of them and round-trips a path
    /// for each provider through the same combo instance.
    /// </summary>
    [TestFixture]
    [Category("Live")]
    public class LiveComboAllProvidersTests
    {
        private readonly List<LiveProviderHost> _hosts = new();
        private ComboStorageFacade _combo;
        private string _localTempRoot;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            IReadOnlyList<StorageFacadeType> remotes = LiveProviderSelection.Resolve()
                .Where(p => p != StorageFacadeType.LocalFileStore)
                .ToList();

            if (remotes.Count < 2)
            {
                Assert.Inconclusive(
                    "Combo-all-providers live test needs at least two remote providers "
                    + "(e.g. make -- test --use-s3-container --use-azure-container).");
            }

            try
            {
                var children = new List<IStorageFacade>();
                foreach (StorageFacadeType provider in remotes)
                {
                    LiveProviderHost host = await LiveProviderHost.StartAsync(provider, wrapInCombo: false).ConfigureAwait(false);
                    host.RelinquishStorageOwnership();
                    _hosts.Add(host);
                    children.Add(host.DirectStorage);
                }

                _combo = new ComboStorageFacade(children);
                _localTempRoot = Path.Combine(
                    Path.GetTempPath(),
                    "storage-facade-combo-all-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_localTempRoot);
            }
            catch (InvalidOperationException ex)
            {
                Assert.Inconclusive(ex.Message);
            }
            catch (Exception ex) when (LiveTestHelpers.IsStartupFailure(ex))
            {
                Assert.Inconclusive($"Skipping combo-all-providers: startup failed — {ex.Message}");
            }
        }

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            (_combo as IDisposable)?.Dispose();

            foreach (LiveProviderHost host in _hosts)
                await host.DisposeAsync().ConfigureAwait(false);

            if (!string.IsNullOrEmpty(_localTempRoot) && Directory.Exists(_localTempRoot))
            {
                try { Directory.Delete(_localTempRoot, recursive: true); }
                catch { /* best-effort */ }
            }
        }

        [SetUp]
        public void SetUp()
        {
            Assume.That(_combo, Is.Not.Null);
            Assume.That(_hosts.Count, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public async Task RoundTripsEachRegisteredProvider_AndLocalFallback()
        {
            foreach (LiveProviderHost host in _hosts)
            {
                string path = host.ObjectPath("combo-all/" + Guid.NewGuid().ToString("N") + ".txt");
                byte[] payload = Encoding.UTF8.GetBytes("combo-all-" + host.ProviderType + "-" + Guid.NewGuid().ToString("N"));

                await using (var write = new MemoryStream(payload))
                {
                    string checksum = await _combo.WriteStorageAsync(path, write, createParentDirectories: true)
                        .ConfigureAwait(false);
                    Assert.That(checksum, Is.Not.Null.And.Not.Empty, host.ProviderType.ToString());
                }

                StorageInfo info = _combo.GetStorageInfo(path);
                Assert.That(info.SizeBytes, Is.EqualTo(payload.Length), host.ProviderType.ToString());

                await using (Stream read = await _combo.ReadStorageAsync(info).ConfigureAwait(false))
                using (var ms = new MemoryStream())
                {
                    await read.CopyToAsync(ms).ConfigureAwait(false);
                    Assert.That(ms.ToArray(), Is.EqualTo(payload), host.ProviderType.ToString());
                }

                await _combo.DeleteStorageAsync(path).ConfigureAwait(false);
                LiveTestHelpers.AssertThrowsOnMissing(
                    () => _combo.GetStorageInfo(path),
                    $"combo-all delete left object for {host.ProviderType}");
            }

            string localPath = Path.Combine(_localTempRoot, "local-" + Guid.NewGuid().ToString("N") + ".txt");
            byte[] localPayload = Encoding.UTF8.GetBytes("combo-all-local");
            await using (var write = new MemoryStream(localPayload))
            {
                await _combo.WriteStorageAsync(localPath, write, createParentDirectories: true).ConfigureAwait(false);
            }

            StorageInfo localInfo = _combo.GetStorageInfo(localPath);
            Assert.That(localInfo.SizeBytes, Is.EqualTo(localPayload.Length));
            await _combo.DeleteStorageAsync(localPath).ConfigureAwait(false);
        }
    }
}
