// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;

    public class StorageFilter
    {
        public List<string> RegexPatterns { get; set; }

        public List<string> Extensions { get; set; }

        public Tuple<DateTime, DateTime> DateRange { get; set; }

        public static bool IsMatch(StorageFilter storageFilter, StorageInfo storageInfo)
        {
            bool isMatch = true;

            // Match Regex Patterns
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

            // Match extensions
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