using AotServiceProvider.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace AotServiceProvider
{
    public class Services(IConfig config) : IServices, IServiceProvider
    {
        public static IServices Current
        {
            get => _current ?? throw new InvalidOperationException($"[{nameof(AotServiceProvider)}]: [{nameof(Services)}] was not initialized before use.");
            private set => _current = value;
        }
        private static IServices? _current;

        protected static string Key<T>() => Key(typeof(T));
        protected static string Key(Type type) => type.FullName ?? type.Name;

        public static void Init(IConfig config)
        {
            if (_current != null)
            {
                throw new InvalidOperationException($"[{nameof(AotServiceProvider)}]: [{nameof(Services)}] were already initialized.");
            }
            _current = new Services(config);
        }

        public static void Hide(IServices services)
        {
            var newType = services.GetType();
            Debug.WriteLine($"[{nameof(AotServiceProvider)}]: [{nameof(Services)}] were hidden by [{newType.FullName ?? newType.Name}].");
            _current = services;
        }

        public T Get<T>() where T : class => (T)Get(Key<T>());
        public T New<T>() where T : class => (T)New(Key<T>());

        protected readonly ConcurrentDictionary<string, Lazy<object>> Cache = new();

        protected virtual object Get(string key) => Cache
            .GetOrAdd(key, k => new Lazy<object>(() => New(k), LazyThreadSafetyMode.ExecutionAndPublication))
            .Value;

        protected virtual object New(string key)
            => config.Resolved.TryGetValue(key, out var factory)
                ? factory()
                : throw new InvalidOperationException($"[{nameof(AotServiceProvider)}]: Service [{key}] is not registered. Make sure it is marked with [{nameof(ServiceAttribute)}] and its assembly is listed in [{nameof(GenerateServiceConfigAttribute)}].");

        object? IServiceProvider.GetService(Type serviceType)
        {
            var key = Key(serviceType);
            return config.Resolved.ContainsKey(key) ? Get(key) : null;
        }
    }
}
