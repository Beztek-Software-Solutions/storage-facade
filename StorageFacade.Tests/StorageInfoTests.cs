// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Tests
{
    using NUnit.Framework;

    [TestFixture]
    public class StorageInfoTests
    {
        [Test]
        public void IsDirectory_IsInverseOfIsFile()
        {
            var info = new StorageInfo { IsFile = true };
            Assert.That(info.IsDirectory, Is.False);

            info.IsFile = false;
            Assert.That(info.IsDirectory, Is.True);
        }

        [Test]
        public void Extension_ReturnsSegmentAfterLastDot()
        {
            var info = new StorageInfo { Name = "report.final.pdf" };
            Assert.That(info.Extension, Is.EqualTo("pdf"));
        }

        [Test]
        public void MimeType_MapsFromExtension()
        {
            var info = new StorageInfo { Name = "photo.jpg" };
            Assert.That(info.MimeType, Does.Contain("jpeg").Or.Contain("jpg"));
        }
    }
}
