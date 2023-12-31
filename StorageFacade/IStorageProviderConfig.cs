﻿// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage
{
    public interface IStorageProviderConfig
    {
        string Name { get; }

        StorageFacadeType StorageFacadeType { get; }
    }
}
