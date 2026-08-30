// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Tests
{
    using System;
    using System.Collections.Generic;
    using NUnit.Framework;

    [TestFixture]
    public class StorageFilterTests
    {
        private static StorageInfo Info(string name, string path, DateTime timestamp) =>
            new()
            {
                Name = name,
                LogicalPath = path,
                Timestamp = timestamp,
                IsFile = true,
                SizeBytes = 10
            };

        [Test]
        public void IsMatch_NullFilter_ReturnsTrue()
        {
            Assert.That(StorageFilter.IsMatch(null, Info("a.txt", "/a.txt", DateTime.UtcNow)), Is.True);
        }

        [Test]
        public void IsMatch_Extension_FiltersCaseInsensitively()
        {
            var filter = new StorageFilter { Extensions = new List<string> { ".PDF" } };
            Assert.That(StorageFilter.IsMatch(filter, Info("doc.pdf", "/doc.pdf", DateTime.UtcNow)), Is.True);
            Assert.That(StorageFilter.IsMatch(filter, Info("doc.txt", "/doc.txt", DateTime.UtcNow)), Is.False);
        }

        [Test]
        public void IsMatch_RegexPattern_MatchesLogicalPath()
        {
            var filter = new StorageFilter { RegexPatterns = new List<string> { @"/invoices/\d+\.pdf" } };
            Assert.That(StorageFilter.IsMatch(filter, Info("1.pdf", "/data/invoices/42.pdf", DateTime.UtcNow)), Is.True);
            Assert.That(StorageFilter.IsMatch(filter, Info("x.txt", "/data/other/x.txt", DateTime.UtcNow)), Is.False);
        }

        [Test]
        public void IsMatch_DateRange_RequiresTimestampWithinHalfOpenInterval()
        {
            var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc);
            var filter = new StorageFilter { DateRange = Tuple.Create(start, end) };

            Assert.That(StorageFilter.IsMatch(filter, Info("a.txt", "/a.txt", start)), Is.True);
            Assert.That(StorageFilter.IsMatch(filter, Info("b.txt", "/b.txt", end)), Is.False);
            Assert.That(StorageFilter.IsMatch(filter, Info("c.txt", "/c.txt", start.AddDays(-1))), Is.False);
        }

        [Test]
        public void IsMatch_CombinedCriteria_AllMustPass()
        {
            var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var filter = new StorageFilter
            {
                Extensions = new List<string> { ".txt" },
                DateRange = Tuple.Create(start, start.AddDays(30))
            };

            Assert.That(StorageFilter.IsMatch(filter, Info("a.txt", "/a.txt", start.AddDays(1))), Is.True);
            Assert.That(StorageFilter.IsMatch(filter, Info("a.pdf", "/a.pdf", start.AddDays(1))), Is.False);
        }
    }
}
