// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Tests.Live
{
    using System.Collections;
    using NUnit.Framework;

    /// <summary>
    /// Feeds <see cref="LiveComboProviderTests"/> one fixture per selected provider
    /// (File, S3, Azure, SMB, GCS, OSS). File exercises the combo’s built-in local fallback;
    /// remotes exercise prefix routing to that backend plus the same File fallback.
    /// </summary>
    public static class LiveComboFixtureSource
    {
        public static IEnumerable Providers()
        {
            foreach (StorageFacadeType provider in LiveProviderSelection.Resolve())
            {
                yield return new TestFixtureData(provider)
                    .SetArgDisplayNames("Combo+" + provider);
            }
        }
    }
}
