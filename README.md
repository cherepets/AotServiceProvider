# AotServiceProvider

[![Build](https://github.com/cherepets/AotServiceProvider/actions/workflows/build.yml/badge.svg)](https://github.com/cherepets/AotServiceProvider/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/AotServiceProvider.svg)](https://www.nuget.org/packages/AotServiceProvider)

Automatically discover and register services at compile time.
No reflection at runtime, so it works with trimming and Native AOT. Targets .NET Standard 2.0.

Main differences from MS DI:
- Auto discovery
- Fail at build time
- Ease of overriding priorities
- Test mock already included
- Decide lifecycle at call, not at init

## Why?
I don't like writing the service registration code manually.
Failing at build time provides additional safety.
Sometimes tests require different lifecycles compared to real implementation (give a different mock to a specific call, not the whole test).

## Usage

Mark your service interfaces with `ServiceAttribute`:

```csharp
[Service]
public interface IAbstract
{
}
```

Install the package in your .NET project, add `GenerateServiceConfigAttribute` with one type from each assembly to scan, ordered by priority (last = highest).
Example:

```csharp
[assembly: GenerateServiceConfig(
    typeof(Demo.Interfaces.IAbstract),
    typeof(Demo.Implementation.MyClass),
    typeof(Demo.Tests.MyClassTest)
)]
```

At the moment when you need to register your dependencies (like app startup), call `Services.Init`:

```csharp
public App()
{
    Services.Init(new ServiceConfig());
}
```

## Used by
[Authi](https://github.com/cherepets/Authi-OSS)
