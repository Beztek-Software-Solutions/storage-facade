// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Tests.Live
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using NUnit.Framework;

    /// <summary>
    /// Live <see cref="ComboStorageFacade"/> for every selected provider (File, S3, Azure, SMB,
    /// GCS, OSS). Uses <see cref="LiveProviderHost.Storage"/> (already a combo wrapping that
    /// provider) to assert prefix routing for remotes and the built-in File fallback for OS paths.
    /// </summary>
    [TestFixtureSource(typeof(LiveComboFixtureSource), nameof(LiveComboFixtureSource.Providers))]
    [Category("Live")]
    public class LiveComboProviderTests
    {
        private readonly StorageFacadeType _providerType;
        private LiveProviderHost _host;
        private IStorageFacade _combo;
        private string _localTempRoot;
        private bool _isRemote;

        public LiveComboProviderTests(StorageFacadeType providerType)
        {
            _providerType = providerType;
        }

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            try
            {
                _isRemote = _providerType != StorageFacadeType.LocalFileStore;
                _host = await LiveProviderHost.StartAsync(_providerType).ConfigureAwait(false);
                _combo = _host.Storage;
                _localTempRoot = Path.Combine(
                    Path.GetTempPath(),
                    "storage-facade-combo-live-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_localTempRoot);
            }
            catch (InvalidOperationException ex)
            {
                Assert.Inconclusive(ex.Message);
            }
            catch (Exception ex) when (LiveTestHelpers.IsStartupFailure(ex))
            {
                Assert.Inconclusive($"Skipping Combo+{_providerType}: startup failed — {ex.Message}");
            }
        }

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            if (_host != null)
                await _host.DisposeAsync().ConfigureAwait(false);

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
            Assume.That(_host, Is.Not.Null);
        }

        [Test]
        public void GetType_IsComboStore()
        {
            Assert.That(_combo.GetType(), Is.EqualTo(StorageFacadeType.ComboStore));
            Assert.That(_combo.GetName(), Is.EqualTo("ComboProvider"));
        }

        [Test]
        public async Task ProviderPrefix_RoundTripsThroughCombo()
        {
            if (!_isRemote)
            {
                Assert.Pass("File provider uses the combo local-file fallback (no remote prefix).");
                return;
            }

            string remotePath = _host.ObjectPath("combo/" + Guid.NewGuid().ToString("N") + ".txt");
            byte[] payload = Encoding.UTF8.GetBytes("combo-remote-" + Guid.NewGuid().ToString("N"));

            await RoundTripAsync(_combo, remotePath, payload).ConfigureAwait(false);
        }

        [Test]
        public async Task LocalFileFallback_RoundTripsThroughCombo()
        {
            string localPath = Path.Combine(_localTempRoot, "combo-local-" + Guid.NewGuid().ToString("N") + ".txt");
            byte[] payload = Encoding.UTF8.GetBytes("combo-local-" + Guid.NewGuid().ToString("N"));

            await RoundTripAsync(_combo, localPath, payload).ConfigureAwait(false);
        }

        [Test]
        public void ProviderPrefix_MissingObject_GetStorageInfoThrows()
        {
            if (!_isRemote)
            {
                Assert.Pass("File provider uses the combo local-file fallback (no remote prefix).");
                return;
            }

            string remotePath = _host.ObjectPath("combo-missing/" + Guid.NewGuid().ToString("N") + ".txt");
            LiveTestHelpers.AssertThrowsOnMissing(
                () => _combo.GetStorageInfo(remotePath),
                $"Combo GetStorageInfo missing remote ({_providerType})");
        }

        [Test]
        public async Task ProviderPrefix_MissingObject_ReadThrows()
        {
            if (!_isRemote)
            {
                Assert.Pass("File provider uses the combo local-file fallback (no remote prefix).");
                return;
            }

            string remotePath = _host.ObjectPath("combo-missing/" + Guid.NewGuid().ToString("N") + ".txt");
            var info = new StorageInfo { LogicalPath = remotePath, IsFile = true, Name = "gone" };
            await LiveTestHelpers.AssertThrowsOnMissingAsync(
                () => _combo.ReadStorageAsync(info),
                $"Combo ReadStorageAsync missing remote ({_providerType})").ConfigureAwait(false);
        }

        [Test]
        public void LocalFileFallback_MissingObject_GetStorageInfoThrows()
        {
            string localPath = Path.Combine(_localTempRoot, "no-such-" + Guid.NewGuid().ToString("N") + ".txt");
            LiveTestHelpers.AssertThrowsOnMissing(
                () => _combo.GetStorageInfo(localPath),
                "Combo GetStorageInfo missing local file");
        }

        private static async Task RoundTripAsync(IStorageFacade storage, string path, byte[] payload)
        {
            await using (var write = new MemoryStream(payload))
            {
                string checksum = await storage.WriteStorageAsync(
                    path,
                    write,
                    createParentDirectories: true).ConfigureAwait(false);
                Assert.That(checksum, Is.Not.Null.And.Not.Empty);
            }

            StorageInfo info = storage.GetStorageInfo(path);
            Assert.That(info.IsFile, Is.True);
            Assert.That(info.SizeBytes, Is.EqualTo(payload.Length));

            await using (Stream read = await storage.ReadStorageAsync(info).ConfigureAwait(false))
            using (var ms = new MemoryStream())
            {
                await read.CopyToAsync(ms).ConfigureAwait(false);
                Assert.That(ms.ToArray(), Is.EqualTo(payload));
            }

            await storage.DeleteStorageAsync(path).ConfigureAwait(false);

            LiveTestHelpers.AssertThrowsOnMissing(
                () => storage.GetStorageInfo(path),
                $"object still present after combo delete: {path}");
        }
    }
}
