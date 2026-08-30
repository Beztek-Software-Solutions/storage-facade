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

            bool isMatch = true;

            if (isMatch && storageFilter.RegexPatterns != null && storageFilter.RegexPatterns.Count > 0)
            {
                foreach (string pattern in storageFilter.RegexPatterns)
                {
                    if (Regex.Match(storageInfo.LogicalPath, pattern).Success)
                    {
                        isMatch = true;
                        break;
                    }
                    isMatch = false;
                }
            }

            if (isMatch && storageFilter.Extensions != null && storageFilter.Extensions.Count > 0)
            {
                foreach (string extension in storageFilter.Extensions)
                {
                    if (storageInfo.Name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                    {
                        isMatch = true;
                        break;
                    }
                    isMatch = false;
                }
            }
            if (isMatch && storageFilter.DateRange != null)
            {
                isMatch = storageInfo.Timestamp >= storageFilter.DateRange.Item1;
                if (isMatch)
                {
                    isMatch = storageInfo.Timestamp < storageFilter.DateRange.Item2;
                }
            }
            return isMatch;
        }
    }
}
