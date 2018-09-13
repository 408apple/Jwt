﻿// Copyright (c) 2018 Yann Crumeyrolle. All rights reserved.
// Licensed under the MIT license. See the LICENSE file in the project root for more information.

namespace JsonWebToken.Internal
{
    public abstract class PooledObjectPolicy<T>
    {
        public abstract T Create();
    }
}
