// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;

    /// <summary>Optional filter applied when enumerating storage objects.</summary>
    public class StorageFilter
    {
        /// <summary>Regex patterns matched against <see cref="StorageInfo.LogicalPath"/>.</summary>
        public List<string> RegexPatterns { get; set; }

        /// <summary>File extensions to include (case-insensitive suffix match on <see cref="StorageInfo.Name"/>).</summary>
        public List<string> Extensions { get; set; }

        /// <summary>Half-open UTC interval `[Item1, Item2)` matched against <see cref="StorageInfo.Timestamp"/>.</summary>
        public Tuple<DateTime, DateTime> DateRange { get; set; }

        /// <summary>Returns true when <paramref name="storageInfo"/> satisfies all configured criteria (or when filter is null).</summary>
        public static bool IsMatch(StorageFilter storageFilter, StorageInfo storageInfo)
        {
            if (storageFilter == null)
                return true;

            return MatchesAnyRegex(storageFilter.RegexPatterns, storageInfo.LogicalPath)
                && MatchesAnyExtension(storageFilter.Extensions, storageInfo.Name)
                && MatchesDateRange(storageFilter.DateRange, storageInfo.Timestamp);
        }

        private static bool MatchesAnyRegex(List<string> patterns, string logicalPath)
        {
            if (patterns == null || patterns.Count == 0)
                return true;

            foreach (string pattern in patterns)
            {
                if (Regex.IsMatch(logicalPath, pattern))
                    return true;
            }

            return false;
        }

        private static bool MatchesAnyExtension(List<string> extensions, string name)
        {
            if (extensions == null || extensions.Count == 0)
                return true;

            foreach (string extension in extensions)
            {
                if (name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool MatchesDateRange(Tuple<DateTime, DateTime> dateRange, DateTime timestamp)
        {
            if (dateRange == null)
                return true;

            return timestamp >= dateRange.Item1 && timestamp < dateRange.Item2;
        }
    }
}
