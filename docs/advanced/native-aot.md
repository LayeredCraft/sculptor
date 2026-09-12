# Native AOT Support

DecoWeaver's generated decorator-composition code is designed to be safe under [Native AOT](https://learn.microsoft.com/dotnet/core/deploying/native-aot/) publishing and trimming.

## What This Means For You

Nothing extra. If your application publishes cleanly with `PublishAot=true` today, adding DecoWeaver-decorated registrations does not introduce new trim or AOT warnings, and does not require you to:

- root DecoWeaver's generated types manually,
- add `[DynamicallyAccessedMembers]` or `[DynamicDependency]` attributes of your own,
- register closed generic decorator types yourself, or
- suppress any `IL2xxx`/`IL3xxx` warnings.

## How

Every decorator type DecoWeaver applies — concrete or open generic — is closed to its final, concrete type **at generator time**, not at runtime. For an open generic decorator like:

```csharp
[DecoratedBy(typeof(CachingRepository<>))]
public class Repository<T> : IRepository<T> where T : class { }
```

registered against a closed service:

```csharp
services.AddScoped<IRepository<User>, Repository<User>>();
```

the generator already knows, from the registration itself, that `T` is `User`. It emits `typeof(CachingRepository<User>)` directly into the generated interceptor as a literal — never a runtime `Type.MakeGenericType` call. Because DecoWeaver only ever intercepts closed generic service registrations (see [Open Generics](../usage/open-generics.md)), this closing type argument is always available at generation time; there is no supported decorator shape that needs to be closed dynamically at runtime.

Decorator construction itself still runs through [`ActivatorUtilities.CreateInstance`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.activatorutilities.createinstance) — resolving a decorator's constructor-injected dependencies is inherently runtime work, the same as for any other DI-constructed type — but because the target type is passed as a literal `typeof(...)` expression at each call site (never through an intermediate, unannotated `Type` variable), the trim analyzer can resolve and preserve it statically with no annotations needed.

## Supported Target Frameworks

Native AOT compatibility applies to the code DecoWeaver's generator emits into *your* project, which follows your own application's target framework and AOT settings. `LayeredCraft.DecoWeaver.Attributes` (the small package defining `[DecoratedBy]` and friends) targets `netstandard2.0` and `net8.0`, and declares `IsAotCompatible` on `net8.0`+.

`LayeredCraft.DecoWeaver` (the source generator itself) targets `netstandard2.0` and runs inside the Roslyn compiler host during your build — it is never part of your published application and has no bearing on your app's own AOT compatibility.

## Limitations

- **Constructor-injected decorator dependencies are still resolved at runtime**, as described above. This is standard .NET DI behavior for any AOT-published application, not specific to DecoWeaver, and requires no special handling.
- Registration shapes DecoWeaver does not support at all (e.g. open generic *service* registrations via `services.AddScoped(typeof(IRepository<>), typeof(Repository<>))`) are simply not intercepted — decorators aren't applied, and no DecoWeaver-owned dynamic code runs either way. See [Open Generics](../usage/open-generics.md) for supported registration shapes.

## Validation

Native AOT compatibility for DecoWeaver's generated code is validated continuously in CI: a dedicated console application exercises representative decorator scenarios (basic decoration, multiple ordered decorators, open-generic decorators over closed registrations, all three DI lifetimes, keyed services, factory delegates, and instance registrations), is published with `PublishAot=true`, and the resulting native binary is executed — not just published — to confirm the decorated services actually resolve and behave correctly.

## See Also

- [Open Generics](../usage/open-generics.md) — supported registration and decoration shapes
- [How It Works](../core-concepts/how-it-works.md) — compile-time generation model
- [Performance](performance.md) — zero runtime reflection or assembly scanning
