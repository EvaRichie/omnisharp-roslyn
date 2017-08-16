using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Reflection;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using OmniSharp.Services;
using System.Linq;
using System;
using Microsoft.Extensions.Logging;

namespace OmniSharp
{
    [Export]
    public class HostServicesAggregator
    {
        private ImmutableArray<Assembly> _assemblies;

        [ImportingConstructor]
        public HostServicesAggregator(
            [ImportMany] IEnumerable<IHostServicesProvider> hostServicesProviders, ILoggerFactory loggerFactory)
        {
            var logger = loggerFactory.CreateLogger<HostServicesAggregator>();
            var builder = ImmutableHashSet.CreateBuilder<Assembly>();

            // We always include the default Roslyn assemblies, which includes:
            //
            //   * Microsoft.CodeAnalysis.Workspaces
            //   * Microsoft.CodeAnalysis.CSharp.Workspaces
            //   * Microsoft.CodeAnalysis.VisualBasic.Workspaces

            foreach (var assembly in MefHostServices.DefaultAssemblies)
            {
                builder.Add(assembly);
            }

            foreach (var provider in hostServicesProviders)
            {
                foreach (var assembly in provider.Assemblies)
                {
                    try
                    {
                        var exportedTypes = assembly.ExportedTypes;
                        builder.Add(assembly);
                        logger.LogTrace("Successfully added {assembly} to host service assemblies.", assembly.FullName);
                    }
                    catch (Exception)
                    {
                        // if we can't see exported types, it means that the assembly cannot participate
                        // in MefHostServices as one or more of its dependencies (typically a Visual Studio or GACed DLL) is missing
                        logger.LogWarning("Expected to use {assembly} in host services but the assembly cannot be used due to missing dependencies.", assembly.FullName);
                    }
                }
            }

            _assemblies = builder.ToImmutableArray();
        }

        public HostServices CreateHostServices()
        {
            return MefHostServices.Create(_assemblies);
        }
    }
}
