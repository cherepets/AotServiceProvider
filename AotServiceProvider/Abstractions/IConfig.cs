using System;
using System.Collections.Generic;

namespace AotServiceProvider.Abstractions
{
    public interface IConfig
    {
        IReadOnlyDictionary<string, Func<object>> Resolved { get; }
    }
}
