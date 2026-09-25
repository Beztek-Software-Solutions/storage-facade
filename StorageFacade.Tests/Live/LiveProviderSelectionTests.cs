// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Tests.Live
{
    using System;
    using System.Collections.Generic;
    using NUnit.Framework;

    [TestFixture]
    public class LiveProviderSelectionTests
    {
        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable(LiveProviderSelection.EnvVar, null);
        }

        [Test]
        public void Resolve_Unset_DefaultsToFile()
        {
            Environment.SetEnvironmentVariable(LiveProviderSelection.EnvVar, null);
            Assert.That(
                LiveProviderSelection.Resolve(),
                Is.EqualTo(new[] { StorageFacadeType.LocalFileStore }));
        }

        [Test]
        public void Resolve_SingleProvider_ReturnsOne()
        {
            Environment.SetEnvironmentVariable(LiveProviderSelection.EnvVar, "s3");
            Assert.That(
                LiveProviderSelection.Resolve(),
                Is.EqualTo(new[] { StorageFacadeType.AmazonS3Store }));
        }

        [Test]
        public void Resolve_Subset_ParsesAliases()
        {
            Environment.SetEnvironmentVariable(LiveProviderSelection.EnvVar, "file, azure, smb, gcs, oss");
            Assert.That(
                LiveProviderSelection.Resolve(),
                Is.EqualTo(new[]
                {
                    StorageFacadeType.LocalFileStore,
                    StorageFacadeType.AzureBlobStore,
                    StorageFacadeType.SMBNetworkStore,
                    StorageFacadeType.GoogleCloudStorageStore,
                    StorageFacadeType.AlibabaOssStore,
                }));
        }

        [Test]
        public void Resolve_All_ReturnsEveryProvider()
        {
            Environment.SetEnvironmentVariable(LiveProviderSelection.EnvVar, "all");
            IReadOnlyList<StorageFacadeType> providers = LiveProviderSelection.Resolve();
            Assert.That(providers, Does.Contain(StorageFacadeType.LocalFileStore));
            Assert.That(providers, Does.Contain(StorageFacadeType.AmazonS3Store));
            Assert.That(providers, Does.Contain(StorageFacadeType.AzureBlobStore));
            Assert.That(providers, Does.Contain(StorageFacadeType.SMBNetworkStore));
            Assert.That(providers, Does.Contain(StorageFacadeType.GoogleCloudStorageStore));
            Assert.That(providers, Does.Contain(StorageFacadeType.AlibabaOssStore));
            Assert.That(providers.Count, Is.EqualTo(6));
        }

        [Test]
        public void Resolve_UnknownToken_Throws()
        {
            Environment.SetEnvironmentVariable(LiveProviderSelection.EnvVar, "not-a-provider");
            Assert.Throws<ArgumentException>(() => LiveProviderSelection.Resolve());
        }
    }
}
