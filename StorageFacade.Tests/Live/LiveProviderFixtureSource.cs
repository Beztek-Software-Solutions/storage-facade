// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Tests.Live
{
    using System.Collections;
    using NUnit.Framework;

    /// <summary>
    /// Feeds <see cref="LiveProviderTests"/> one fixture per selected provider.
    /// Default (unset env) is File only.
    /// </summary>
    public static class LiveProviderFixtureSource
    {
        public static IEnumerable Providers()
        {
            foreach (StorageFacadeType provider in LiveProviderSelection.Resolve())
            {
                yield return new TestFixtureData(provider)
                    .SetArgDisplayNames(provider.ToString());
            }
        }
    }
}
