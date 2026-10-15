using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The production split, asserted rather than reviewed. Survsys.Web runs as
/// its own IIS site and may not reach the database; Survsys.Api is the only
/// tier that may, and may not let an entity type onto the wire. Both fences
/// are read off the built assemblies, so a breach fails a test instead of
/// surviving a reading of the diff.
/// </summary>
public class ArchitectureTests
{
    private const string DataAssembly = "Survsys.Data";
    private const string EfCorePrefix = "Microsoft.EntityFrameworkCore";
    private const string ModulePrefix = "Survsys.";
    private const string MinimalApiExtensions = "EndpointRouteBuilderExtensions";

    [Fact]
    public void SurvsysWebReferencesNeitherSurvsysDataNorEntityFrameworkCore()
    {
        var forbidden = ReachableFrom("Survsys.Web")
            .Where(name => name == DataAssembly
                || name.StartsWith(EfCorePrefix, StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            forbidden.Count == 0,
            $"Survsys.Web reaches {string.Join(", ", forbidden)}. The application tier runs "
            + "as its own site and must not reach the database: it takes its request and "
            + "response types from Survsys.Contracts and the data itself from the API tier.");
    }

    [Fact]
    public void NoSurvsysApiEndpointSignatureNamesASurvsysDataType()
    {
        var api = Assembly.Load("Survsys.Api");

        // The surface this test reads is controller actions and the methods a
        // route attribute marks. A minimal-API handler written as a lambda has
        // no signature reflection can reach, so that shape is fenced out
        // rather than left as a hole this test would pass straight over.
        Assert.False(
            RegistersMinimalApiEndpoints(api),
            "Survsys.Api registers an endpoint through MapGet, MapPost or one of their "
            + "siblings. A lambda handler's signature is not reachable by reflection, so "
            + "this test could not see a Survsys.Data type going out on the wire. An endpoint "
            + "is a controller action, or a method a route attribute marks: both are read "
            + "here, and MapControllers registers them without this surface.");

        var breaches = (
            from endpoint in EndpointsOf(api)
            from named in SignatureOf(endpoint)
            where named.Assembly.GetName().Name == DataAssembly
            select $"{endpoint.DeclaringType?.Name}.{endpoint.Name} names {named.Name}")
            .Distinct()
            .OrderBy(breach => breach, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            breaches.Count == 0,
            $"An endpoint signature names a Survsys.Data type: {string.Join("; ", breaches)}. "
            + "Endpoints take and return Survsys.Contracts types; the mapping between entities "
            + "and wire shapes stays inside Survsys.Api and never reaches the wire.");
    }

    /// <summary>
    /// Every assembly the named one can reach. Only a Survsys module can carry
    /// a reference onward to Survsys.Data, so the walk recurses into those and
    /// stops at the framework.
    /// </summary>
    private static IReadOnlyCollection<string> ReachableFrom(string assemblyName)
    {
        var reached = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<Assembly>();
        pending.Enqueue(Assembly.Load(assemblyName));

        while (pending.Count > 0)
        {
            foreach (var reference in pending.Dequeue().GetReferencedAssemblies())
            {
                if (reference.Name is not { } name || !reached.Add(name))
                {
                    continue;
                }

                if (name.StartsWith(ModulePrefix, StringComparison.Ordinal))
                {
                    pending.Enqueue(Assembly.Load(name));
                }
            }
        }

        return reached;
    }

    /// <summary>
    /// Whether the assembly reaches the minimal-API registration surface --
    /// `MapGet`, `MapPost` and their siblings, which all hang off
    /// EndpointRouteBuilderExtensions. Read from the assembly's own member
    /// references, because what is being asked is which framework method the
    /// compiled code calls.
    /// </summary>
    private static bool RegistersMinimalApiEndpoints(Assembly assembly)
    {
        using var file = File.OpenRead(assembly.Location);
        using var portableExecutable = new PEReader(file);
        var metadata = portableExecutable.GetMetadataReader();

        return metadata.MemberReferences
            .Select(metadata.GetMemberReference)
            .Where(member => member.Parent.Kind == HandleKind.TypeReference)
            .Select(member => metadata.GetTypeReference((TypeReferenceHandle)member.Parent))
            .Any(declaring => metadata.GetString(declaring.Name) == MinimalApiExtensions);
    }

    /// <summary>
    /// The endpoints of an assembly: the action methods a controller exposes,
    /// and any method a route attribute marks as a handler, which is how a
    /// minimal-API handler written as a method rather than a lambda is reached.
    /// </summary>
    private static IEnumerable<MethodInfo> EndpointsOf(Assembly assembly)
    {
        const BindingFlags declared = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (var type in TypesOf(assembly))
        {
            var controller = typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract;

            foreach (var method in type.GetMethods(declared))
            {
                if (method.IsSpecialName)
                {
                    continue;
                }

                var routed = method.GetCustomAttributes().Any(
                    attribute => attribute is IRouteTemplateProvider or IActionHttpMethodProvider);
                var action = controller && method is { IsPublic: true, IsStatic: false }
                    && !method.IsDefined(typeof(NonActionAttribute));

                if (routed || action)
                {
                    yield return method;
                }
            }
        }
    }

    /// <summary>
    /// Every type an endpoint's signature names: its parameters and its return
    /// type, with Task&lt;&gt;, ValueTask&lt;&gt;, ActionResult&lt;&gt;, arrays and
    /// collections unwrapped to whatever is inside them, at any depth.
    /// </summary>
    private static IEnumerable<Type> SignatureOf(MethodInfo endpoint) =>
        endpoint.GetParameters()
            .Select(parameter => parameter.ParameterType)
            .Append(endpoint.ReturnType)
            .SelectMany(Unwrap);

    private static IEnumerable<Type> Unwrap(Type type)
    {
        yield return type;

        if (type.GetElementType() is { } element)
        {
            foreach (var inner in Unwrap(element))
            {
                yield return inner;
            }
        }

        foreach (var argument in type.GenericTypeArguments)
        {
            foreach (var inner in Unwrap(argument))
            {
                yield return inner;
            }
        }
    }

    private static IEnumerable<Type> TypesOf(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException unloadable)
        {
            return unloadable.Types.OfType<Type>();
        }
    }
}
