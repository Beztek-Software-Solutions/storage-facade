// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Tests
{
    using Beztek.Facade.Storage.Providers;
    using NUnit.Framework;

    [TestFixture]
    public class CloudLogicalPathTests
    {
        [Test]
        public void GetRelativePath_StripsStorePrefix()
        {
            Assert.That(
                CloudLogicalPath.GetRelativePath("s3://orders", "s3://orders/a/b.txt"),
                Is.EqualTo("a/b.txt"));
        }

        [Test]
        public void GetRelativePath_StoreRoot_ReturnsEmpty()
        {
            Assert.That(
                CloudLogicalPath.GetRelativePath("oss://bucket", "oss://bucket"),
                Is.EqualTo(string.Empty));
        }

        [Test]
        public void IsListedObjectVisible_NonRecursive_OnlyDirectChildren()
        {
            Assert.That(
                CloudLogicalPath.IsListedObjectVisible("a", "a/file.txt", isRecursive: false),
                Is.True);
            Assert.That(
                CloudLogicalPath.IsListedObjectVisible("a", "a/nested/file.txt", isRecursive: false),
                Is.False);
            Assert.That(
                CloudLogicalPath.IsListedObjectVisible("a", "a/nested/file.txt", isRecursive: true),
                Is.True);
            Assert.That(
                CloudLogicalPath.IsListedObjectVisible("a", "a/", isRecursive: true),
                Is.False);
        }
    }
}
