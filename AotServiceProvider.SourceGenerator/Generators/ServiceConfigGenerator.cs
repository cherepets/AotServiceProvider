using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AotServiceProvider.SourceGenerator.Generators
{
    [Generator(LanguageNames.CSharp)]
    public class ServiceConfigGenerator : IIncrementalGenerator
    {
        private const string ConfigAttributeName = "AotServiceProvider.GenerateServiceConfigAttribute";
        private const string ServiceAttributeName = "AotServiceProvider.ServiceAttribute";
        private const string PriorityAttributeName = "AotServiceProvider.PriorityAttribute";
        private const string Category = "AotServiceProvider";

        private static readonly DiagnosticDescriptor NoImplementation = new(
            "AOTSP001",
            "Service has no implementation",
            "Service [{0}] has no implementation",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor AmbiguousImplementation = new(
            "AOTSP002",
            "Service has multiple implementations with the same priority",
            "Service [{0}] has multiple implementations with the same priority",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor InaccessibleImplementation = new(
            "AOTSP003",
            "Service implementation is not accessible",
            "Implementation [{0}] of service [{1}] is not accessible from assembly [{2}]",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor NoParameterlessConstructor = new(
            "AOTSP004",
            "Service implementation has no parameterless constructor",
            "Implementation [{0}] of service [{1}] has no accessible parameterless constructor",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor GenericService = new(
            "AOTSP005",
            "Generic services are not supported",
            "Generic service [{0}] is not supported",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterSourceOutput(context.CompilationProvider, (context, compilation) =>
            {
                var configAttribute = compilation.Assembly
                    .GetAttributes()
                    .FirstOrDefault(x => x.AttributeClass?.ToDisplayString() == ConfigAttributeName);
                if (configAttribute?.ConstructorArguments.Length != 1 || compilation.AssemblyName == null) return;

                var location = configAttribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation();
                var catalog = configAttribute.ConstructorArguments[0].Values
                    .Select(x => (x.Value as ITypeSymbol)?.ContainingAssembly)
                    .OfType<IAssemblySymbol>()
                    .Distinct<IAssemblySymbol>(SymbolEqualityComparer.Default)
                    .Select((x, index) => (Index: index, Types: GetNamespaceTypes(x.GlobalNamespace).ToList()))
                    .ToList();

                var registrations = new List<(string Key, string Implementation)>();
                foreach (var service in catalog.SelectMany(x => x.Types).Where(x => x.TypeKind == TypeKind.Interface
                    && x.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == ServiceAttributeName)))
                {
                    if (Resolve(context, compilation, catalog, service, location) is INamedTypeSymbol implementation)
                    {
                        registrations.Add((
                            GetFullMetadataName(service),
                            implementation.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                    }
                }

                context.AddSource("ServiceConfig.g.cs", SourceText.From(
                    GenerateSource(compilation.AssemblyName, registrations),
                    Encoding.UTF8));
            });
        }

        private static INamedTypeSymbol? Resolve(
            SourceProductionContext context,
            Compilation compilation,
            IReadOnlyList<(int Index, List<INamedTypeSymbol> Types)> catalog,
            INamedTypeSymbol service,
            Location? location)
        {
            var serviceName = service.ToDisplayString();
            if (service.IsGenericType)
            {
                context.ReportDiagnostic(Diagnostic.Create(GenericService, location, serviceName));
                return null;
            }

            var candidates = catalog
                .SelectMany(x => x.Types
                    .Where(t => t.TypeKind == TypeKind.Class
                        && !t.IsAbstract
                        && !t.IsStatic
                        && !t.IsGenericType
                        && t.AllInterfaces.Contains(service, SymbolEqualityComparer.Default))
                    .Select(t => (Type: t, Priority: GetPriority(t) ?? x.Index)))
                .ToList();
            if (candidates.Count == 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(NoImplementation, location, serviceName));
                return null;
            }

            var maxPriority = candidates.Max(x => x.Priority);
            var topPriorityCandidates = candidates.Where(x => x.Priority == maxPriority).ToList();
            if (topPriorityCandidates.Count > 1)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    AmbiguousImplementation,
                    location,
                    serviceName,
                    maxPriority));
                return null;
            }

            var implementation = topPriorityCandidates[0].Type;
            if (!compilation.IsSymbolAccessibleWithin(implementation, compilation.Assembly))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InaccessibleImplementation,
                    location,
                    implementation.ToDisplayString(),
                    serviceName,
                    compilation.AssemblyName));
                return null;
            }

            if (!implementation.InstanceConstructors.Any(x => x.Parameters.Length == 0
                && compilation.IsSymbolAccessibleWithin(x, compilation.Assembly)))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    NoParameterlessConstructor,
                    location,
                    implementation.ToDisplayString(),
                    serviceName));
                return null;
            }

            return implementation;
        }

        private static int? GetPriority(INamedTypeSymbol type)
            => type.GetAttributes()
                .Where(x => x.AttributeClass?.ToDisplayString() == PriorityAttributeName)
                .Select(x => x.ConstructorArguments.Length == 1 ? x.ConstructorArguments[0].Value as int? : null)
                .FirstOrDefault();

        private static IEnumerable<INamedTypeSymbol> GetNamespaceTypes(INamespaceSymbol @namespace)
            => @namespace.GetTypeMembers().SelectMany(x => GetNestedTypes(x))
                .Concat(@namespace.GetNamespaceMembers().SelectMany(x => GetNamespaceTypes(x)));

        private static IEnumerable<INamedTypeSymbol> GetNestedTypes(INamedTypeSymbol type)
            => new[] { type }.Concat(type.GetTypeMembers().SelectMany(x => GetNestedTypes(x)));

        private static string GetFullMetadataName(INamedTypeSymbol type)
        {
            var name = type.MetadataName;
            var outer = type;
            while (outer.ContainingType != null)
            {
                outer = outer.ContainingType;
                name = $"{outer.MetadataName}+{name}";
            }
            return outer.ContainingNamespace.IsGlobalNamespace
                ? name
                : $"{outer.ContainingNamespace.ToDisplayString()}.{name}";
        }

        private static string GenerateSource(string rootNamespace, IReadOnlyList<(string Key, string Implementation)> registrations)
        {
            var entries = string.Join(@"
", registrations.Select(x => $@"                {{ ""{x.Key}"", () => new {x.Implementation}() }},"));

            return $@"// <auto-generated />
namespace {rootNamespace}
{{
    /// <summary>
    /// Auto-generated service configuration by AotServiceProvider
    /// </summary>
    internal sealed partial class ServiceConfig : global::AotServiceProvider.Abstractions.IConfig
    {{
        private static readonly global::System.Collections.Generic.IReadOnlyDictionary<string, global::System.Func<object>> _resolved =
            new global::System.Collections.Generic.Dictionary<string, global::System.Func<object>>
            {{
{entries}
            }};

        /// <summary>
        /// Service factories keyed by the full name of the service type.
        /// </summary>
        public global::System.Collections.Generic.IReadOnlyDictionary<string, global::System.Func<object>> Resolved => _resolved;
    }}
}}
";
        }
    }
}
