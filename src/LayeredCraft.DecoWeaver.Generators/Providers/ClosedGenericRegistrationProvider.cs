using LayeredCraft.DecoWeaver.Model;
using LayeredCraft.DecoWeaver.Util;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LayeredCraft.DecoWeaver.Providers;

internal enum RegistrationKind
{
    /// <summary>Parameterless: AddScoped&lt;T1, T2&gt;()</summary>
    Parameterless,
    /// <summary>Factory with two type params: AddScoped&lt;T1, T2&gt;(Func&lt;IServiceProvider, T2&gt;)</summary>
    FactoryTwoTypeParams,
    /// <summary>Factory with single type param: AddScoped&lt;T&gt;(Func&lt;IServiceProvider, T&gt;)</summary>
    FactorySingleTypeParam,
    /// <summary>Keyed parameterless: AddKeyedScoped&lt;T1, T2&gt;(object? serviceKey)</summary>
    KeyedParameterless,
    /// <summary>Keyed factory with two type params: AddKeyedScoped&lt;T1, T2&gt;(object? serviceKey, Func&lt;IServiceProvider, object?, T2&gt;)</summary>
    KeyedFactoryTwoTypeParams,
    /// <summary>Keyed factory with single type param: AddKeyedScoped&lt;T&gt;(object? serviceKey, Func&lt;IServiceProvider, object?, T&gt;)</summary>
    KeyedFactorySingleTypeParam,
    /// <summary>Instance with single type param: AddSingleton&lt;T&gt;(T implementationInstance)</summary>
    InstanceSingleTypeParam,
    /// <summary>Keyed instance with single type param: AddKeyedSingleton&lt;T&gt;(object? serviceKey, T implementationInstance)</summary>
    KeyedInstanceSingleTypeParam
}

internal readonly record struct ClosedGenericRegistration(
    TypeDefId ServiceDef,
    TypeDefId ImplDef,
    string ServiceFqn, // Fully qualified closed type (e.g., "global::DecoWeaver.Sample.IRepository<global::DecoWeaver.Sample.Customer>")
    string ImplFqn, // Fully qualified closed type
    string Lifetime, // "AddTransient" | "AddScoped" | "AddSingleton" | "AddKeyedTransient" | "AddKeyedScoped" | "AddKeyedSingleton"
    string InterceptsData, // "file|start|length"
    RegistrationKind Kind = RegistrationKind.Parameterless,
    string? FactoryParameterName = null, // Parameter name from the original registration (e.g., "implementationFactory")
    string? ServiceKeyParameterName = null, // Parameter name for keyed services (e.g., "serviceKey")
    string? InstanceParameterName = null, // Parameter name for instance registrations (e.g., "implementationInstance")
    // Fully qualified type argument names of the closed service type, in declaration order
    // (empty if the service isn't generic). Lets the emitter close open-generic decorator type
    // definitions (e.g. CachingRepository<>) at generation time instead of via runtime
    // Type.MakeGenericType, since DecoWeaver only ever discovers closed generic service
    // registrations, so this argument list is always fully known here.
    EquatableArray<string> ServiceTypeArgFqns = default
);

internal readonly record struct RegistrationValidationResult(
    RegistrationKind Kind,
    string? FactoryParameterName,
    string? ServiceKeyParameterName,
    string? InstanceParameterName
);

/// <summary>
/// Discovers closed generic registrations like services.AddScoped&lt;IRepo&lt;T&gt;, SqlRepo&lt;T&gt;&gt;()
/// Open generic registrations with typeof() are NOT supported - decorators only work with closed types.
/// </summary>
internal static class ClosedGenericRegistrationProvider
{
    private static readonly string[] AllowedContainingTypes =
    [
        "Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions",
        "Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions"
    ];

    internal static bool Predicate(SyntaxNode node, CancellationToken _)
        => node is InvocationExpressionSyntax;

    internal static ClosedGenericRegistration? Transformer(GeneratorSyntaxContext ctx, CancellationToken _)
    {
        if (ctx.Node is not InvocationExpressionSyntax inv) return null;

        if (ctx.SemanticModel.GetSymbolInfo(inv).Symbol is not IMethodSymbol symbol) return null;

        // For extension methods, we want the unreduced (static) form to check the containing type
        var symbolToCheck = symbol.ReducedFrom ?? symbol;

        // Allow both DI extension containers
        if (!AllowedContainingTypes.Contains(symbolToCheck.ContainingType?.ToDisplayString()))
            return null;

        var name = symbol.Name;
        var isKeyed = name is "AddKeyedTransient" or "AddKeyedScoped" or "AddKeyedSingleton";
        var isNonKeyed = name is "AddTransient" or "AddScoped" or "AddSingleton";

        if (!isKeyed && !isNonKeyed) return null;

        // Must be generic method with 1 or 2 type arguments
        if (!symbol.IsGenericMethod) return null;
        if (symbol.TypeArguments.Length is not (1 or 2)) return null;

        // Validate registration pattern and extract metadata
        var validationResult = isKeyed
            ? ValidateKeyedRegistration(symbolToCheck, symbol)
            : ValidateNonKeyedRegistration(symbolToCheck, symbol);

        if (validationResult is null) return null;

        // Extract service and implementation types
        var (svc, impl) = ExtractServiceAndImplementationTypes(symbol, validationResult.Value.Kind, inv, ctx.SemanticModel);
        if (svc is null || impl is null) return null;

        // Get interceptable location
        var interceptsData = GetInterceptsData(ctx.SemanticModel, inv);
        if (interceptsData is null) return null;

        // Generate fully qualified names for the closed types
        var serviceFqn = svc.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var implFqn = impl.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        // Capture the service's own closed type arguments so open-generic decorators can be
        // closed at generation time (positionally, matching how runtime MakeGenericType(serviceType
        // .GetGenericArguments()) would have closed them) instead of via runtime reflection.
        var serviceTypeArgFqns = svc.TypeArguments.Length > 0
            ? new EquatableArray<string>(svc.TypeArguments
                .Select(a => a.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .ToArray())
            : default;

        return new ClosedGenericRegistration(
            ServiceDef: TypeId.Create(svc).Definition,
            ImplDef: TypeId.Create(impl).Definition,
            ServiceFqn: serviceFqn,
            ImplFqn: implFqn,
            Lifetime: name,
            InterceptsData: interceptsData,
            Kind: validationResult.Value.Kind,
            FactoryParameterName: validationResult.Value.FactoryParameterName,
            ServiceKeyParameterName: validationResult.Value.ServiceKeyParameterName,
            InstanceParameterName: validationResult.Value.InstanceParameterName,
            ServiceTypeArgFqns: serviceTypeArgFqns
        );
    }

    private static RegistrationValidationResult? ValidateNonKeyedRegistration(
        IMethodSymbol symbolToCheck,
        IMethodSymbol symbol)
    {
        // NON-KEYED SERVICES
        // Accept:
        // - 1 param: Parameterless (IServiceCollection services)
        // - 2 params: Factory delegate (IServiceCollection services, Func<IServiceProvider, T> implementationFactory)
        var paramCount = symbolToCheck.Parameters.Length;
        if (paramCount is not (1 or 2)) return null;

        if (paramCount == 1)
        {
            // Parameterless registration: AddScoped<T1, T2>()
            if (symbol.TypeArguments.Length != 2) return null;
            return new RegistrationValidationResult(RegistrationKind.Parameterless, null, null, null);
        }

        // paramCount == 2 - could be factory delegate OR instance registration
        var param2 = symbolToCheck.Parameters[1];

        // Check if it's an instance parameter (not a delegate)
        if (!IsFactoryDelegate(param2))
        {
            // ONLY accept AddSingleton for instance registrations
            if (symbol.Name != "AddSingleton") return null;

            // Instance registrations ONLY exist with single type parameter in .NET DI
            // AddSingleton<TService>(TService implementationInstance)
            // There is NO AddSingleton<TService, TImplementation>(TImplementation) overload
            if (symbol.TypeArguments.Length != 1) return null;

            var instanceParamName = param2.Name;

            // param2.Type should be the TService type parameter from the method signature
            var svcTypeParam = symbolToCheck.TypeParameters.Length >= 1 ? symbolToCheck.TypeParameters[0] : null;
            if (svcTypeParam == null || !SymbolEqualityComparer.Default.Equals(param2.Type, svcTypeParam))
                return null;

            return new RegistrationValidationResult(
                RegistrationKind.InstanceSingleTypeParam,
                null,
                null,
                instanceParamName);
        }

        // Factory delegate registration
        if (!ValidateFactoryDelegate(param2, expectedArgCount: 2, out var funcReturnType))
            return null;

        var factoryParamName = param2.Name;

        if (symbol.TypeArguments.Length == 2)
        {
            // AddScoped<TService, TImplementation>(Func<IServiceProvider, TImplementation>)
            return new RegistrationValidationResult(
                RegistrationKind.FactoryTwoTypeParams,
                factoryParamName,
                null,
                null);
        }

        // AddScoped<TService>(Func<IServiceProvider, TService>)
        // Verify factory return type matches TService (only type arg)
        var svcTypeArg2 = symbol.TypeArguments[0];
        if (!SymbolEqualityComparer.Default.Equals(funcReturnType, svcTypeArg2))
            return null;

        return new RegistrationValidationResult(
            RegistrationKind.FactorySingleTypeParam,
            factoryParamName,
            null,
            null);
    }

    private static RegistrationValidationResult? ValidateKeyedRegistration(
        IMethodSymbol symbolToCheck,
        IMethodSymbol symbol)
    {
        // KEYED SERVICES
        // Accept:
        // - 2 params: Keyed parameterless (IServiceCollection services, object? serviceKey)
        // - 3 params: Keyed factory delegate (IServiceCollection services, object? serviceKey, Func<IServiceProvider, object?, T>)
        var paramCount = symbolToCheck.Parameters.Length;
        if (paramCount is not (2 or 3)) return null;

        // Second parameter must be the service key (object?)
        var keyParam = symbolToCheck.Parameters[1];
        if (keyParam.Type.SpecialType != SpecialType.System_Object) return null;
        var serviceKeyParamName = keyParam.Name;

        if (paramCount == 2)
        {
            // Keyed parameterless: AddKeyedScoped<T1, T2>(key)
            if (symbol.TypeArguments.Length != 2) return null;
            return new RegistrationValidationResult(
                RegistrationKind.KeyedParameterless,
                null,
                serviceKeyParamName,
                null);
        }

        // paramCount == 3 - could be keyed factory delegate OR keyed instance registration
        var param3 = symbolToCheck.Parameters[2];

        // Check if it's a keyed instance parameter (not a delegate)
        if (!IsKeyedFactoryDelegate(param3))
        {
            // ONLY accept AddKeyedSingleton for keyed instance registrations
            if (symbol.Name != "AddKeyedSingleton") return null;

            // Keyed instance registrations ONLY exist with single type parameter in .NET DI
            // AddKeyedSingleton<TService>(object? serviceKey, TService implementationInstance)
            // There is NO AddKeyedSingleton<TService, TImplementation>(key, TImplementation) overload
            if (symbol.TypeArguments.Length != 1) return null;

            var instanceParamName = param3.Name;

            // param3.Type should be the TService type parameter from the method signature
            var svcTypeParam = symbolToCheck.TypeParameters.Length >= 1 ? symbolToCheck.TypeParameters[0] : null;
            if (svcTypeParam == null || !SymbolEqualityComparer.Default.Equals(param3.Type, svcTypeParam))
                return null;

            return new RegistrationValidationResult(
                RegistrationKind.KeyedInstanceSingleTypeParam,
                null,
                serviceKeyParamName,
                instanceParamName);
        }

        // Keyed factory delegate registration
        if (!ValidateKeyedFactoryDelegate(param3, out var funcReturnType))
            return null;

        var factoryParamName = param3.Name;

        if (symbol.TypeArguments.Length == 2)
        {
            // AddKeyedScoped<TService, TImplementation>(key, Func<IServiceProvider, object?, TImplementation>)
            return new RegistrationValidationResult(
                RegistrationKind.KeyedFactoryTwoTypeParams,
                factoryParamName,
                serviceKeyParamName,
                null);
        }

        // AddKeyedScoped<TService>(key, Func<IServiceProvider, object?, TService>)
        // Verify factory return type matches TService (only type arg)
        var svcTypeArg = symbol.TypeArguments[0];
        if (!SymbolEqualityComparer.Default.Equals(funcReturnType, svcTypeArg))
            return null;

        return new RegistrationValidationResult(
            RegistrationKind.KeyedFactorySingleTypeParam,
            factoryParamName,
            serviceKeyParamName,
            null);
    }

    private static bool IsKeyedFactoryDelegate(IParameterSymbol param)
    {
        if (param.Type is not INamedTypeSymbol namedType) return false;
        var originalDef = namedType.OriginalDefinition.ToDisplayString();
        return originalDef == "System.Func<T1, T2, TResult>";
    }

    private static bool IsFactoryDelegate(IParameterSymbol param)
    {
        if (param.Type is not INamedTypeSymbol namedType) return false;
        var originalDef = namedType.OriginalDefinition.ToDisplayString();
        return originalDef == "System.Func<T, TResult>" || originalDef == "System.Func<T1, T2, TResult>";
    }

    private static bool ValidateFactoryDelegate(
        IParameterSymbol factoryParam,
        int expectedArgCount,
        out ITypeSymbol? funcReturnType)
    {
        funcReturnType = null;

        // Check if parameter type is Func<IServiceProvider, TReturn>
        if (factoryParam.Type is not INamedTypeSymbol funcType) return false;
        if (funcType.OriginalDefinition.ToDisplayString() != "System.Func<T, TResult>") return false;
        if (funcType.TypeArguments.Length != expectedArgCount) return false;

        var funcArgType = funcType.TypeArguments[0];

        // First arg must be IServiceProvider
        if (funcArgType.ToDisplayString() != "System.IServiceProvider") return false;

        funcReturnType = funcType.TypeArguments[1];
        return true;
    }

    private static bool ValidateKeyedFactoryDelegate(
        IParameterSymbol factoryParam,
        out ITypeSymbol? funcReturnType)
    {
        funcReturnType = null;

        // Check if parameter type is Func<IServiceProvider, object?, TReturn>
        if (factoryParam.Type is not INamedTypeSymbol funcType) return false;
        if (funcType.OriginalDefinition.ToDisplayString() != "System.Func<T1, T2, TResult>") return false;
        if (funcType.TypeArguments.Length != 3) return false;

        var funcArg1Type = funcType.TypeArguments[0];
        var funcArg2Type = funcType.TypeArguments[1];

        // First arg must be IServiceProvider
        if (funcArg1Type.ToDisplayString() != "System.IServiceProvider") return false;

        // Second arg must be object? (the key)
        if (funcArg2Type.SpecialType != SpecialType.System_Object) return false;

        funcReturnType = funcType.TypeArguments[2];
        return true;
    }

    private static (INamedTypeSymbol? Service, INamedTypeSymbol? Implementation) ExtractServiceAndImplementationTypes(
        IMethodSymbol symbol,
        RegistrationKind kind,
        InvocationExpressionSyntax inv,
        SemanticModel semanticModel)
    {
        if (symbol.TypeArguments.Length == 2)
        {
            var svc = symbol.TypeArguments[0] as INamedTypeSymbol;
            var impl = symbol.TypeArguments[1] as INamedTypeSymbol;
            return (svc, impl);
        }

        // For single type param: AddScoped<T>(...)
        var serviceType = symbol.TypeArguments[0] as INamedTypeSymbol;

        // For instance registrations, extract the actual implementation type from the argument
        if (kind == RegistrationKind.InstanceSingleTypeParam)
        {
            // Get the actual argument expression (the instance being passed)
            // For: services.AddSingleton<IRepository<Customer>>(instance)
            // We need to get the type of 'instance'
            // Note: Extension method syntax doesn't include 'this' parameter in ArgumentList
            var args = inv.ArgumentList.Arguments;
            if (args.Count >= 1) // Just the instance parameter
            {
                var instanceArg = args[0].Expression;
                var instanceType = semanticModel.GetTypeInfo(instanceArg).Type as INamedTypeSymbol;
                if (instanceType != null)
                {
                    return (serviceType, instanceType);
                }
            }
        }

        // For keyed instance registrations, extract the actual implementation type from the argument
        if (kind == RegistrationKind.KeyedInstanceSingleTypeParam)
        {
            // Get the actual argument expression (the instance being passed)
            // For: services.AddKeyedSingleton<IRepository<Customer>>("key", instance)
            // We need to get the type of 'instance' (second argument after the key)
            // Note: Extension method syntax doesn't include 'this' parameter in ArgumentList
            var args = inv.ArgumentList.Arguments;
            if (args.Count >= 2) // Key parameter + instance parameter
            {
                var instanceArg = args[1].Expression;
                var instanceType = semanticModel.GetTypeInfo(instanceArg).Type as INamedTypeSymbol;
                if (instanceType != null)
                {
                    return (serviceType, instanceType);
                }
            }
        }

        // For factory delegates or if we can't determine the instance type, use the service type for both
        return (serviceType, serviceType);
    }

    private static string? GetInterceptsData(SemanticModel semanticModel, InvocationExpressionSyntax inv)
    {
        // Hash-based interceptable location (Roslyn experimental)
#pragma warning disable RSEXPERIMENTAL002
        var il = semanticModel.GetInterceptableLocation(inv);
#pragma warning restore RSEXPERIMENTAL002
        return il?.Data;
    }
}