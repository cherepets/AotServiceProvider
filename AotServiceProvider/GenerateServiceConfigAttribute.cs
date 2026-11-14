using System;
using System.Linq;
using System.Reflection;

namespace AotServiceProvider
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class GenerateServiceConfigAttribute(params Type[] assemblyMarkers) : Attribute
    {
        public Assembly[] Assemblies => assemblyMarkers
            .Select(x => x.Assembly)
            .Distinct()
            .ToArray();
    }
}
