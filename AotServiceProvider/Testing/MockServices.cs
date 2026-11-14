using AotServiceProvider.Abstractions;
using System.Collections.Generic;

namespace AotServiceProvider.Testing
{
    public class MockServices(IConfig config) : Services(config)
    {
        private readonly Dictionary<string, object> _overrides = [];

        public MockServices Override<T>(T mock) where T : class
        {
            _overrides[Key<T>()] = mock;
            return this;
        }

        protected override object Get(string key)
        {
            return _overrides.TryGetValue(key, out var mock) ? mock : base.Get(key);
        }

        protected override object New(string key)
        {
            return _overrides.TryGetValue(key, out var mock) ? mock : base.New(key);
        }
    }
}
