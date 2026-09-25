// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Tests.Live
{
    using System.Collections;
    using NUnit.Framework;

    /// <summary>
    /// Feeds <see cref="LiveComboProviderTests"/> one fixture per selected <em>remote</em> provider
    /// (File is excluded — combo already embeds a local-file fallback).
    /// </summary>
    public static class LiveComboFixtureSource
    {
        public static IEnumerable Providers()
        {
            foreach (StorageFacadeType provider in LiveProviderSelection.Resolve())
            {
                if (provider == StorageFacadeType.LocalFileStore)
                    continue;

                yield return new TestFixtureData(provider)
                    .SetArgDisplayNames("Combo+" + provider);
            }
        }
    }
}
