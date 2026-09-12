// DecoWeaver/Emit/InterceptorEmitter.cs

using LayeredCraft.DecoWeaver.Model;
using LayeredCraft.DecoWeaver.OutputGenerators;
using LayeredCraft.DecoWeaver.Providers;
using LayeredCraft.DecoWeaver.Util;
using Microsoft.CodeAnalysis.CSharp;

namespace LayeredCraft.DecoWeaver.Emit;

/// <summary>Emits the interceptor source for DecoWeaver's open-generic decoration rewrite.</summary>
internal static class InterceptorEmitter
{
    public static string EmitClosedGenericInterceptors(
        EquatableArray<ClosedGenericRegistration> registrations,
        Dictionary<TypeDefId, EquatableArray<TypeDefId>> decoratorsByImplementation)
    {
        // Group registrations by lifetime to preserve ordering
        var byLifetime = registrations
            .GroupBy(r => r.Lifetime)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Convert all registrations to template models
        var models = new List<DecoWeaverInterceptorsSources.RegistrationModel>();
        var methodIndex = 0;

        foreach (var (lifetime, regs) in byLifetime)
        {
            foreach (var reg in regs)
            {
                var decorators = decoratorsByImplementation.TryGetValue(reg.ImplDef, out var decos) && decos.Count > 0
                    ? decos.Select(d => ToClosedFqn(d, reg.ServiceTypeArgFqns)).ToArray()
                    : Array.Empty<string>();

                models.Add(DecoWeaverInterceptorsSources.CreateRegistrationModel(
                    reg, lifetime, methodIndex++, decorators, Escape(reg.InterceptsData)));
            }
        }

        // Use the unified template for ALL registrations
        return DecoWeaverInterceptorsSources.Generate(models);
    }

    /// <summary>
    /// Builds a decorator's fully-qualified type name, closing any open generic type parameters
    /// at generation time against the registration's own (already-closed) service type arguments —
    /// positionally, matching the semantics that a runtime `Type.MakeGenericType(serviceType
    /// .GetGenericArguments())` call would have produced. DecoWeaver only discovers closed generic
    /// service registrations, so these arguments are always known here; emitting the closed name
    /// directly lets the template pass it to `typeof(...)` as a literal, eliminating the runtime
    /// `MakeGenericType`/reflection-based closing this helper used to require.
    /// </summary>
    private static string ToClosedFqn(TypeDefId t, EquatableArray<string> serviceTypeArgFqns)
    {
        var ns = t.ContainingNamespaces is { Count: > 0 } ? string.Join(".", t.ContainingNamespaces) : null;
        var nest = t.ContainingTypes is { Count: > 0 } ? string.Join("+", t.ContainingTypes) : null;
        var head = ns is null ? "" : ns + ".";
        if (!string.IsNullOrEmpty(nest)) head += nest + ".";

        // Strip backtick notation (e.g., "DynamoDbRepository`1" -> "DynamoDbRepository")
        var metadataName = t.MetadataName;
        var backtickIndex = metadataName.IndexOf('`');
        if (backtickIndex >= 0)
            metadataName = metadataName.Substring(0, backtickIndex);

        // Close generic decorators using the service's own closed type arguments, positionally.
        // Falls back to an open `<,,>` form if the argument count doesn't match the decorator's
        // arity (e.g. a non-generic service with a generic decorator) — an already-unsupported
        // shape that would have failed the same way at runtime via MakeGenericType.
        if (t.Arity > 0)
        {
            metadataName = serviceTypeArgFqns.Count == t.Arity
                ? $"{metadataName}<{string.Join(",", serviceTypeArgFqns)}>"
                : $"{metadataName}<{new string(',', t.Arity - 1)}>";
        }

        // Prepend global:: to avoid namespace conflicts in generated code
        return $"global::{head}{metadataName}";
    }

    private static string Escape(string s) =>
        SymbolDisplay.FormatLiteral(s, quote: true);
}
