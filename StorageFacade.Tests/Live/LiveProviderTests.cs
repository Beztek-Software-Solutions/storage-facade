// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Tests.Live
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using NUnit.Framework;

    /// <summary>
    /// Cross-provider live suite. Default (no env) is File only.
    /// Remote providers require Make flags
    /// (<c>--use-s3-container</c>, <c>--use-azure-container</c>, <c>--use-smb-container</c>,
    /// <c>--use-gcs-container</c>, <c>--use-oss-live</c>).
    /// </summary>
    [TestFixtureSource(typeof(LiveProviderFixtureSource), nameof(LiveProviderFixtureSource.Providers))]
    [Category("Live")]
    public class LiveProviderTests
    {
        private readonly StorageFacadeType _providerType;
        private LiveProviderHost _host;

        public LiveProviderTests(StorageFacadeType providerType)
        {
            _providerType = providerType;
        }

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            try
            {
                _host = await LiveProviderHost.StartAsync(_providerType).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                Assert.Inconclusive(ex.Message);
            }
            catch (Exception ex) when (LiveTestHelpers.IsStartupFailure(ex))
            {
                Assert.Inconclusive($"Skipping {_providerType}: startup failed — {ex.Message}");
            }
        }

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            if (_host != null)
                await _host.DisposeAsync().ConfigureAwait(false);
        }

        [SetUp]
        public void SetUp()
        {
            Assume.That(_host, Is.Not.Null);
        }

        [Test]
        public async Task WriteReadDelete_RoundTrips()
        {
            string relative = "live/" + Guid.NewGuid().ToString("N") + ".txt";
            string path = _host.ObjectPath(relative);
            byte[] payload = Encoding.UTF8.GetBytes("storage-facade-live-" + Guid.NewGuid().ToString("N"));

            await using (var write = new MemoryStream(payload))
            {
                string checksum = await _host.Storage.WriteStorageAsync(
                    path,
                    write,
                    createParentDirectories: true).ConfigureAwait(false);
                Assert.That(checksum, Is.Not.Null.And.Not.Empty);
            }

            StorageInfo info = _host.Storage.GetStorageInfo(path);
            Assert.That(info.IsFile, Is.True);
            Assert.That(info.SizeBytes, Is.EqualTo(payload.Length));

            await using (Stream read = await _host.Storage.ReadStorageAsync(info).ConfigureAwait(false))
            using (var ms = new MemoryStream())
            {
                await read.CopyToAsync(ms).ConfigureAwait(false);
                Assert.That(ms.ToArray(), Is.EqualTo(payload));
            }

            await _host.Storage.DeleteStorageAsync(path).ConfigureAwait(false);

            // After delete the object must be gone — GetStorageInfo and/or Read must fail.
            await AssertObjectAbsentAsync(path).ConfigureAwait(false);
        }

        [Test]
        public void GetStorageInfo_MissingObject_Throws()
        {
            string path = _host.ObjectPath("missing/" + Guid.NewGuid().ToString("N") + ".txt");
            LiveTestHelpers.AssertThrowsOnMissing(
                () => _host.Storage.GetStorageInfo(path),
                $"GetStorageInfo on missing path for {_providerType}");
        }

        [Test]
        public async Task ReadStorageAsync_MissingObject_Throws()
        {
            string path = _host.ObjectPath("missing/" + Guid.NewGuid().ToString("N") + ".txt");
            var info = new StorageInfo
            {
                LogicalPath = path,
                Name = Path.GetFileName(path),
                IsFile = true,
            };

            await LiveTestHelpers.AssertThrowsOnMissingAsync(
                () => _host.Storage.ReadStorageAsync(info),
                $"ReadStorageAsync on missing path for {_providerType}").ConfigureAwait(false);
        }

        [Test]
        public async Task ComputeMD5Checksum_MissingObject_Throws()
        {
            string path = _host.ObjectPath("missing/" + Guid.NewGuid().ToString("N") + ".txt");
            await LiveTestHelpers.AssertThrowsOnMissingAsync(
                () => _host.Storage.ComputeMD5Checksum(path),
                $"ComputeMD5Checksum on missing path for {_providerType}").ConfigureAwait(false);
        }

        [Test]
        public async Task DeleteStorageAsync_MissingObject_IsAbsentAfterward()
        {
            // S3/GCS-style deletes are often idempotent (no throw); File/Azure/SMB typically throw.
            // Either way, the object must not be readable afterward.
            string path = _host.ObjectPath("missing-del/" + Guid.NewGuid().ToString("N") + ".bin");
            try
            {
                await _host.Storage.DeleteStorageAsync(path).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Expected for non-idempotent providers.
            }

            await AssertObjectAbsentAsync(path).ConfigureAwait(false);
        }

        [Test]
        public void GetName_MatchesProviderKind()
        {
            string name = _host.Storage.GetName();
            Assert.That(_host.Storage.GetType(), Is.EqualTo(_providerType));

            switch (_providerType)
            {
                case StorageFacadeType.LocalFileStore:
                    Assert.That(name, Is.EqualTo("Local Store"));
                    break;
                case StorageFacadeType.AmazonS3Store:
                    Assert.That(name, Does.StartWith("s3://"));
                    break;
                case StorageFacadeType.AzureBlobStore:
                    Assert.That(name, Does.Contain("://"));
                    break;
                case StorageFacadeType.SMBNetworkStore:
                    Assert.That(name, Does.StartWith(@"\\"));
                    break;
                case StorageFacadeType.GoogleCloudStorageStore:
                    Assert.That(name, Does.StartWith("gs://"));
                    break;
                case StorageFacadeType.AlibabaOssStore:
                    Assert.That(name, Does.StartWith("oss://"));
                    break;
            }
        }

        [Test]
        public async Task Write_WithValidateChecksum_Succeeds()
        {
            string path = _host.ObjectPath("live-chk/" + Guid.NewGuid().ToString("N") + ".txt");
            byte[] payload = Encoding.UTF8.GetBytes("checksum-" + Guid.NewGuid().ToString("N"));

            await using (var write = new MemoryStream(payload))
            {
                string checksum = await _host.Storage.WriteStorageAsync(
                    path,
                    write,
                    createParentDirectories: true,
                    validateChecksum: true).ConfigureAwait(false);
                Assert.That(checksum, Is.Not.Null.And.Not.Empty);

                string recomputed = await _host.Storage.ComputeMD5Checksum(path).ConfigureAwait(false);
                Assert.That(recomputed, Is.EqualTo(checksum));
            }

            await _host.Storage.DeleteStorageAsync(path).ConfigureAwait(false);
        }

        [Test]
        public async Task EnumerateStorageInfo_FindsWrittenObject_NonRecursiveAndRecursive()
        {
            string folder = "live-enum/" + Guid.NewGuid().ToString("N");
            string nestedRelative = folder + "/nested/deep.txt";
            string directRelative = folder + "/direct.txt";
            string nestedPath = _host.ObjectPath(nestedRelative);
            string directPath = _host.ObjectPath(directRelative);
            string folderPath = _host.ObjectPath(folder);

            byte[] payload = Encoding.UTF8.GetBytes("enum-payload");
            await using (var w1 = new MemoryStream(payload))
                await _host.Storage.WriteStorageAsync(directPath, w1, createParentDirectories: true)
                    .ConfigureAwait(false);
            await using (var w2 = new MemoryStream(payload))
                await _host.Storage.WriteStorageAsync(nestedPath, w2, createParentDirectories: true)
                    .ConfigureAwait(false);

            try
            {
                var directOnly = new System.Collections.Generic.List<StorageInfo>(
                    _host.Storage.EnumerateStorageInfo(folderPath, isRecursive: false));
                Assert.That(directOnly.Exists(i => i.LogicalPath == directPath || i.Name == "direct.txt"), Is.True,
                    $"expected direct.txt under {folderPath}; got: {string.Join(", ", directOnly.ConvertAll(i => i.LogicalPath))}");

                var recursive = new System.Collections.Generic.List<StorageInfo>(
                    _host.Storage.EnumerateStorageInfo(folderPath, isRecursive: true));
                Assert.That(recursive.Count, Is.GreaterThanOrEqualTo(2));
                Assert.That(
                    recursive.Exists(i => i.LogicalPath == nestedPath || i.Name == "deep.txt"),
                    Is.True,
                    $"expected nested deep.txt; got: {string.Join(", ", recursive.ConvertAll(i => i.LogicalPath))}");
            }
            finally
            {
                try { await _host.Storage.DeleteStorageAsync(directPath).ConfigureAwait(false); } catch { /* best-effort */ }
                try { await _host.Storage.DeleteStorageAsync(nestedPath).ConfigureAwait(false); } catch { /* best-effort */ }
            }
        }

        private async Task AssertObjectAbsentAsync(string path)
        {
            bool getThrew = false;
            try
            {
                _ = _host.Storage.GetStorageInfo(path);
            }
            catch (Exception)
            {
                getThrew = true;
            }

            if (getThrew)
                return;

            // Some backends return metadata stubs; read must still fail.
            var info = new StorageInfo { LogicalPath = path, IsFile = true, Name = "gone" };
            await LiveTestHelpers.AssertThrowsOnMissingAsync(
                () => _host.Storage.ReadStorageAsync(info),
                $"object still readable after expected absence for {_providerType}: {path}").ConfigureAwait(false);
        }
    }
}
