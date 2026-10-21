using System.Reflection;
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

    /// <summary>The class `MapGet`, `MapPost` and their siblings hang off.</summary>
    private const string MinimalApiExtensions = "EndpointRouteBuilderExtensions";

    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic
        | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

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
        var breaches = (
            from endpoint in EndpointsOf(Assembly.Load("Survsys.Api"))
            from named in SignatureOf(endpoint)
            where named.Assembly.GetName().Name == DataAssembly
            select $"{endpoint.DeclaringType?.FullName}.{endpoint.Name} names {named.Name}")
            .Distinct()
            .OrderBy(breach => breach, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            breaches.Count == 0,
            $"An endpoint signature names a Survsys.Data type: {string.Join("; ", breaches)}. "
            + "Endpoints take and return Survsys.Contracts types; the mapping between entities "
            + "and wire shapes stays inside Survsys.Api and never reaches the wire.");
    }

    [Fact]
    public void SurvsysMessagingReferencesNeitherSurvsysDataNorEntityFrameworkCore()
    {
        var forbidden = ReachableFrom("Survsys.Messaging")
            .Where(name => name == DataAssembly
                || name.StartsWith(EfCorePrefix, StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            forbidden.Count == 0,
            $"Survsys.Messaging reaches {string.Join(", ", forbidden)}. The messaging module "
            + "parses lab messages into Survsys.Contracts intake types and must never reach the "
            + "database: it takes nHapi and Survsys.Contracts and nothing of Survsys.Data or "
            + "Entity Framework Core.");
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
    /// The endpoints of an assembly, in both shapes it can write one: the
    /// action methods a controller exposes together with any method a route
    /// attribute marks, and the handlers a minimal-API registration hands
    /// over.
    /// </summary>
    private static IEnumerable<MethodInfo> EndpointsOf(Assembly assembly)
    {
        foreach (var type in TypesOf(assembly))
        {
            var controller = typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract;

            foreach (var method in type.GetMethods(Declared))
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

        foreach (var handler in MinimalApiHandlersOf(assembly))
        {
            yield return handler;
        }
    }

    /// <summary>
    /// The handlers a minimal-API registration hands over. A lambda has no
    /// declaration of its own to read, so what is read is the body that
    /// registers it: at each call to `MapGet` or one of its siblings, the
    /// handler is the delegate that body made for that call, which is the
    /// last one it made before it -- directly, or out of the compiler's
    /// cache field. That reaches a named handler as readily as a lambda, and
    /// reaches both wherever the registration is written rather than only in
    /// the entry point, while leaving out both a helper the body merely calls
    /// and a delegate it made for something else, such as a service
    /// registration over a DbContext.
    /// </summary>
    private static IEnumerable<MethodInfo> MinimalApiHandlersOf(Assembly assembly)
    {
        foreach (var registration in BodiesOf(assembly))
        {
            byte[]? il;
            try
            {
                il = registration.GetMethodBody()?.GetILAsByteArray();
            }
            catch (InvalidOperationException)
            {
                continue;
            }

            if (il is null)
            {
                continue;
            }

            var (called, handedOver) = MethodTokensIn(il);

            foreach (var call in called)
            {
                if (!IsMinimalApiRegistration(Resolve(assembly, call.Token)))
                {
                    continue;
                }

                var handler = handedOver
                    .Where(delegated => delegated.At < call.At)
                    .OrderByDescending(delegated => delegated.At)
                    .Select(delegated => Resolve(assembly, delegated.Token))
                    .OfType<MethodInfo>()
                    .FirstOrDefault();

                if (handler is not null)
                {
                    yield return handler;
                }
            }
        }
    }

    private static bool IsMinimalApiRegistration(MethodBase? called) =>
        called?.DeclaringType?.Name == MinimalApiExtensions
        && called.Name.StartsWith("Map", StringComparison.Ordinal);

    /// <summary>
    /// Where an instruction stream calls a method (`call`, `callvirt`) and
    /// where it turns one into a delegate (`ldftn`, `ldvirtftn`), each with
    /// its offset, so a call can be paired with the delegate made for it. The
    /// scan reads every byte rather than decoding each instruction's length,
    /// so it offers candidates and never misses one; <see cref="Resolve"/>
    /// discards the bytes that only looked like a token.
    /// </summary>
    private static (List<Site> Called, List<Site> HandedOver) MethodTokensIn(byte[] il)
    {
        const byte call = 0x28;
        const byte callVirtual = 0x6F;
        const byte extendedPrefix = 0xFE;
        const byte loadFunction = 0x06;
        const byte loadVirtualFunction = 0x07;

        var called = new List<Site>();
        var handedOver = new List<Site>();

        for (var at = 0; at + 5 < il.Length; at++)
        {
            if (il[at] is call or callVirtual)
            {
                called.Add(new Site(at, BitConverter.ToInt32(il, at + 1)));
            }
            else if (il[at] == extendedPrefix && il[at + 1] is loadFunction or loadVirtualFunction)
            {
                handedOver.Add(new Site(at, BitConverter.ToInt32(il, at + 2)));
            }
        }

        return (called, handedOver);
    }

    /// <summary>A metadata token, and where in a method body it was read.</summary>
    private readonly record struct Site(int At, int Token);

    private static MethodBase? Resolve(Assembly assembly, int token)
    {
        try
        {
            return assembly.ManifestModule.ResolveMethod(token);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    private static IEnumerable<MethodBase> BodiesOf(Assembly assembly) =>
        TypesOf(assembly).SelectMany(type => type.GetMethods(Declared)
            .Cast<MethodBase>()
            .Concat(type.GetConstructors(Declared)));

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
