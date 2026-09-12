# Native AOT Compatibility — Research

Date: 2026-09-11
Author: Claude Code (research/design phase, per user request)
Status: Draft — awaiting review before implementation

## 1. Source issue

[#61](https://github.com/layeredcraft/decoweaver/issues/61) — *Native AOT/trimming warnings (IL2055/IL3050/IL2072) in generated `DecoratorFactory.CloseIfNeeded`/`Create`*.

Filed from real consumer dogfooding (trivia-platform) building with `-p:EnableAotAnalyzer=true -p:EnableTrimAnalyzer=true`. Not originally verified against a live `PublishAot=true` binary.

## 2. Local reproduction

Reproduced against `samples/DecoWeaver.Sample` (net10.0), which exercises both a closed concrete decorator (`[DecoratedBy<UserLoggingDecorator>]`) and open-generic decorators (`[DecoratedBy(typeof(CachingRepository<>), 1)]`):

```
dotnet build -p:EnableAotAnalyzer=true -p:EnableTrimAnalyzer=true -p:EnableSingleFileAnalyzer=true -c Release
```

Output (verbatim, trimmed to the DecoWeaver-owned warnings):

```
…/LayeredCraft.DecoWeaver.Interceptors.ClosedGenerics.g.cs(221,24): warning IL2072:
  'instanceType' argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicConstructors' in call to
  'ActivatorUtilities.CreateInstance(IServiceProvider, Type, params Object[])'. The return value of method
  'DecoratorFactory.CloseIfNeeded(Type, Type)' does not have matching annotations.

…/LayeredCraft.DecoWeaver.Interceptors.ClosedGenerics.g.cs(228,24): warning IL2055:
  Call to 'System.Type.MakeGenericType(params Type[])' can not be statically analyzed.

…/LayeredCraft.DecoWeaver.Interceptors.ClosedGenerics.g.cs(228,24): warning IL3050:
  Using member 'System.Type.MakeGenericType(params Type[])' which has 'RequiresDynamicCodeAttribute'
  can break functionality when AOT compiling.
```

Exact match to the issue's reported warnings, same shared helper (`DecoratorFactory.Create`/`CloseIfNeeded`), same generated file. Build succeeds (warnings only) — a `PublishAot=true` run was not yet performed (no existing AOT-published validation target in the repo; see §7).

## 3. Root cause

Source: `src/LayeredCraft.DecoWeaver.Generators/Templates/DecoWeaverInterceptors.scriban`, emitted verbatim into every consumer's `LayeredCraft.DecoWeaver.Interceptors.ClosedGenerics.g.cs`:

```csharp
private static class DecoratorFactory
{
    public static object Create(IServiceProvider sp, Type serviceType, Type decoratorOpenOrClosed, object inner)
    {
        var closedType = CloseIfNeeded(decoratorOpenOrClosed, serviceType);
        return ActivatorUtilities.CreateInstance(sp, closedType, inner)!;
    }

    private static Type CloseIfNeeded(Type t, Type serviceType)
    {
        if (!t.IsGenericTypeDefinition) return t;
        var args = serviceType.IsGenericType ? serviceType.GetGenericArguments() : Type.EmptyTypes;
        return t.MakeGenericType(args);
    }
}
```

Every decorator application, at every call site, routes through this one shared, generic, reflection-typed helper via:

```csharp
current = (TService)DecoratorFactory.Create(sp, typeof(TService), typeof(TDecorator), current);
```

`typeof(TDecorator)` is always a compile-time literal at the call site — either a concrete closed type (`typeof(global::UserLoggingDecorator)`) or an *open* generic definition (`typeof(global::CachingRepository<>)`, emitted by `InterceptorEmitter.ToFqn`, which always strips type arguments and re-adds bare `<,,>` for any decorator with `Arity > 0` — see `src/LayeredCraft.DecoWeaver.Generators/Emit/InterceptorEmitter.cs:57-63`). Neither the literal-ness nor the "already closed vs. needs closing" distinction is preserved past the call site — both go through the same untyped `Type decoratorOpenOrClosed` parameter, so the trim analyzer can't statically resolve either case:

- **`IL2072`** — `CloseIfNeeded`'s return type isn't annotated `[DynamicallyAccessedMembers(PublicConstructors)]`, so `ActivatorUtilities.CreateInstance`'s requirement isn't satisfied, *even for concrete decorators where the analyzer could otherwise resolve the literal directly if it weren't routed through an unannotated helper method*.
- **`IL2055`/`IL3050`** — `t.MakeGenericType(args)` is a genuine runtime reflection call. It is reachable and does execute for every open-generic-decorator resolution (confirmed above: the sample's `CachingRepository<>`/`LoggingRepository<>` decorators hit this path on every DI resolution).

### Why this is eliminable, not just annotatable

DecoWeaver's own documented constraint (`docs/usage/open-generics.md`) is that **only closed generic service registrations are intercepted** (`AddScoped<IRepository<User>, Repository<User>>()` — never the open `AddScoped(typeof(IRepository<>), typeof(Repository<>))`). This means:

- `ClosedGenericRegistrationProvider` (`src/LayeredCraft.DecoWeaver.Generators/Providers/ClosedGenericRegistrationProvider.cs`) only ever discovers registrations where the service's type arguments are fully known symbols at generator time (`ServiceFqn` is already emitted as a fully closed string, e.g. `global::DecoWeaver.Sample.IRepository<global::DecoWeaver.Sample.Customer>`).
- Open-generic decorators (`[DecoratedBy<CachingRepository<>>]` / `[DecoratedBy(typeof(CachingRepository<>), order)]`) are matched to implementations positionally by generic arity — the runtime `CloseIfNeeded` call literally does `t.MakeGenericType(serviceType.GetGenericArguments())`, i.e. "take the service's own closed type arguments and apply them to the decorator's type parameters, in order." The generator already has that exact same argument list in hand (it's the `TypeArgs` on the service's resolved `TypeId`/symbol) at the point it decides which decorators apply to which registration.
- Today, however, the pipeline **throws that information away before it reaches template rendering**: `DecoratorToIntercept.DecoratorDef` and `ClosedGenericRegistration.ServiceDef` are both `TypeDefId` — definition-only, no type arguments (`TypeDefId` intentionally excludes them, by design, for stable incremental caching — see `Model/TypeDefId.cs`). `InterceptorEmitter.ToFqn` then re-derives an *open* `<,,>` string from the definition alone, with no path back to the service's actual closed arguments. The `ActivatorUtilities`/`MakeGenericType` runtime path exists to reconstruct information the generator itself discarded, not because the information was ever unavailable.

**Conclusion: this is not a one-off defect confined to `DecoratorFactory` — it is the single, correctly-identified choke point for the whole architecture.** Every decorator (concrete or open-generic) funnels through exactly one shared runtime helper, and the fix is available at the same layer for every case: close the decorator type name at generator time (string substitution against the already-known, already-closed service type arguments) and emit a direct, non-reflective construction call per call site, matching each decorator's own constructor shape.

## 4. Full reflection/dynamic-code inventory

Audited per the skill's checklist against `src/`, `samples/`, `test/`:

| Site | Location | Phase | Verdict |
|---|---|---|---|
| `Type.MakeGenericType` | Emitted `DecoratorFactory.CloseIfNeeded` (every consumer's generated file) | Runtime, consumer assembly | **Fix at generator** — eliminable (§3) |
| `ActivatorUtilities.CreateInstance` | Emitted `DecoratorFactory.Create` (same) | Runtime, consumer assembly | **Fix at generator, no residual** — remains a runtime call (constructor/DI-dependency resolution is inherently runtime work), but emitting it as a direct literal `ActivatorUtilities.CreateInstance(sp, typeof(ClosedDecorator), current)` per call site, instead of through the current unannotated `Type`-typed helper, satisfies the trim analyzer with zero annotations and zero warnings for every currently-supported decorator shape (§5) — no JIT-only carve-out needed |
| `GetGenericArguments()` | Emitted `CloseIfNeeded` | Runtime | Removed together with `MakeGenericType` |
| Everything else in `Providers/`, `Roslyn/` (`GetSymbolInfo`, `ToDisplayString`, `INamedTypeSymbol` walks, etc.) | `src/LayeredCraft.DecoWeaver.Generators/**` | **Generator-time only** (Roslyn analysis during a consumer's own build) | Not a consumer runtime AOT concern — this code never ships into or runs inside the consumer's published binary; it runs inside the Roslyn compiler process during the consumer's `dotnet build`/`dotnet publish`. Excluded from the audit's runtime scope. |
| Scriban template load/render (`TemplateHelper.cs`, `Assembly.GetManifestResourceNames`, `Assembly.GetExecutingAssembly`) | `src/LayeredCraft.DecoWeaver.Generators/Emit/TemplateHelper.cs` | **Generator-time only** | Same as above — executes inside the analyzer/generator host process, not the AOT-published consumer binary. |
| `Activator.CreateInstance`, `Type.GetType`, `MakeGenericMethod`, `Reflection.Emit`, `Expression.Compile()`, reflection-based serialization, reflection-based config binding, `[DllImport]` | — | — | **Not present anywhere in the emitted runtime surface or the generator project.** No serializer, no config binder, no interop in this library. |
| `RequiresUnreferencedCode`/`RequiresDynamicCode`/DAM annotations | — | — | **None exist today.** Neither `Attributes` nor `Generators` project declares any trim/AOT annotations; `IsAotCompatible`/`IsTrimmable` are unset on both shipped csproj files (confirmed by direct read — the `IsAotCompatible` line that appeared in the sample's build output was the SDK's own suggested-fix example text from `NETSDK1210`, not a property actually set in this repo). |
| Code hidden from analyzers because it's source-generated | The interceptor `.g.cs` file itself | Runtime, consumer assembly | This *is* the finding — analyzers only saw it because the sample happens to build with `EnableAotAnalyzer` on. A consumer who doesn't opt into analyzers would get the same runtime `RequiresDynamicCode` exposure with **zero warning at all**, since DecoWeaver ships no annotations of its own. |

No un-annotated third-party dependency caps this: DecoWeaver's only runtime consumer-facing dependency is `Microsoft.Extensions.DependencyInjection` (via `ActivatorUtilities`, itself fully AOT-annotated in modern SDKs) and, on `Attributes`, `Polyfill` (compile-time only, no runtime footprint — attributes are `[Conditional("DECOWEAVER_EMIT_ATTRIBUTE_METADATA")]` and don't exist at runtime).

## 5. Isolated defect or architectural pattern?

**Single choke point, not scattered defects.** Every decorator composition in every registration shape (parameterless, factory delegate, keyed, instance, all three lifetimes — confirmed by reading every branch of the `.scriban` template) calls the exact same `DecoratorFactory.Create(sp, typeof(TService), typeof(TDecorator), current)` line. There is exactly one runtime helper to fix, and no other DecoWeaver-generated code path touches reflection or dynamic code at all — registration wiring itself (`services.AddScoped<…>(…)`, `GetRequiredKeyedService<…>`) is fully static generic method calls, not reflection.

### Residual case: is there anything that must stay dynamic?

Walking every documented decorator shape (`docs/usage/open-generics.md`, `docs/usage/multiple-decorators.md`, generator test Cases 001–051):

- **Concrete (closed) decorator** (`[DecoratedBy<UserLoggingDecorator>]`): decorator type is already fully closed at generator time. No reason to route through `MakeGenericType` at all today — and it doesn't (`IsGenericTypeDefinition` guard short-circuits) — but it still incurs `ActivatorUtilities.CreateInstance` with an unannotated `Type`, which is the source of `IL2072`.
- **Open-generic decorator on a closed-generic registration** (`[DecoratedBy<CachingRepository<>>]` applied where the service registration is `AddScoped<IRepository<User>, Repository<User>>()`): per §3, the closing type argument (`User`) is known at generator time. **No residual dynamic requirement** — this can be fully closed at generation time.
- **Nested/multi-arg generics** (`[DecoratedBy<CachingKeyValueStore<,>>]`): same reasoning, positionally, for each type parameter — all resolvable from the service's already-known closed `TypeId.TypeArgs`.

No scenario was found (across the documented API surface and all 51 generator test cases) where the decorator's closing type arguments are **not** knowable at generator time — this is a direct consequence of DecoWeaver's own hard registration constraint (closed generic *service* registrations only). **There is no genuine JIT-only residual for decorator type closing.**

The one thing that *does* remain dynamic even after fixing type closing is `ActivatorUtilities.CreateInstance` itself: it resolves the decorator's constructor and its DI-satisfiable parameters at runtime via reflection, because the generator does not (and reasonably should not) attempt to parse and replicate arbitrary constructor-injection logic. This is inherent to *any* runtime-constructed type with DI-resolved dependencies, not specific to DecoWeaver's generic-closing problem — and it's exactly the shape `ActivatorUtilities.CreateInstance` was designed to be called with a **statically known, literal `Type`** for (see `references/common-fixes.md` pattern: a direct `typeof(X)` literal argument, not routed through an intermediate unannotated method, satisfies the trim analyzer without any suppression, because the analyzer can resolve the constant type and validate/preserve its constructors directly). Once the decorator's type name is closed at generator time and passed as a literal directly to `ActivatorUtilities.CreateInstance` at each call site (not through the current shared `Type`-typed helper), this becomes fully AOT-safe with zero annotations and zero suppressions — no `[RequiresDynamicCode]`, no JIT-only carve-out needed.

## 6. Proposed remediation (generator-side, no public API change)

Preferred direction per the skill's "eliminate over annotate" preference and matching the issue's own suggested direction:

1. **Plumb closed type arguments through the pipeline.** Extend `ClosedGenericRegistration`/`DecoratorToIntercept`/the `decoratorsByImplementation` map (or a companion structure built at emit time) to carry the service's resolved `TypeId.TypeArgs` (as FQN strings) alongside each `DecoratorDef`, not just the bare `TypeDefId`.
2. **Close decorator type names at generator time.** In `InterceptorEmitter`, when a decorator's `Arity > 0`, substitute the service's *own* closed type argument FQNs positionally (matching today's runtime `MakeGenericType(serviceType.GetGenericArguments())` semantics exactly, so behavior is unchanged) instead of emitting bare `<,,>`. Concrete (arity-0) decorators are unaffected.
3. **Emit a direct construction call per decorator, inline at each call site**, replacing the shared `DecoratorFactory.Create`/`CloseIfNeeded` indirection entirely:
   ```csharp
   current = (TService)ActivatorUtilities.CreateInstance(sp, typeof(global::CachingRepository<global::User>), current)!;
   ```
   Because `typeof(...)` is a literal directly at the call site (no intermediate `Type`-typed helper), this satisfies `ActivatorUtilities.CreateInstance`'s `[DynamicallyAccessedMembers(PublicConstructors)]` requirement with **zero warnings and zero annotations** — this is the standard, documented trim-analyzer-friendly pattern for this exact API.
4. **Delete `DecoratorFactory`/`CloseIfNeeded`/`MakeGenericType` from the template entirely.** There is no remaining caller once (3) is done.
5. No public API changes. This is entirely internal to `InterceptorEmitter`/`DecoWeaverInterceptors.scriban` and the generated output shape (which is already an implementation detail — consumers never reference the generated interceptor class directly).

This resolves all three reported warnings (`IL2055`, `IL3050`, `IL2072`) by elimination, not suppression, for 100% of currently-supported decorator shapes — no documented capability needs to be dropped, degraded, or declared JIT-only.

## 7. Validation strategy

- **New AOT validation executable**: `test/LayeredCraft.DecoWeaver.AotValidation` (console app, `net10.0`, `OutputType=Exe`), modeled directly on `samples/DecoWeaver.Sample`'s csproj shape (ProjectReference to `Generators` as `OutputItemType=Analyzer` + `Attributes`, `Microsoft.Extensions.DependencyInjection`). Adds:
  - `<PublishAot>true</PublishAot>`, `<IsAotCompatible>true</IsAotCompatible>` (validation-only; not the shipped library — see §8), `<TrimmerRootAssembly>` pointing at itself so every reachable member is analyzed.
  - A `Program.cs` that builds a real `ServiceCollection`, registers representative scenarios (see coverage matrix, §9), resolves each service through `IServiceProvider`, **invokes a method on the resolved instance to prove the decorator chain actually ran** (e.g. asserts a marker/log-order value), and returns a non-zero exit code on any mismatch or exception.
  - Exercises the exact scenario from issue #61: a closed concrete decorator and an open-generic decorator over a closed-generic registration.
- **Publish + execute, not just publish**: `dotnet publish -r <rid> -c Release` then run the produced native binary directly and check its exit code — a clean `PublishAot` alone is explicitly insufficient per the skill and per this task's instructions.
- **Existing JIT test suite stays primary** and unmodified in role; add one targeted regression case (new numbered case, e.g. `052_AotFriendly_DecoratorConstruction`) to the generator snapshot suite asserting the generated code no longer references `MakeGenericType`/`ActivatorUtilities`-via-helper, so the fix is pinned at the fast layer too.

## 8. `IsAotCompatible` recommendation

**Decision (user, 2026-09-11 — supersedes the earlier "multi-target both projects" call):** the two shipped projects have fundamentally different runtime presence, so they're evaluated and targeted independently.

### `LayeredCraft.DecoWeaver.Generators` — stays `netstandard2.0` only

This project is the Roslyn analyzer/source generator. Confirmed from its own csproj (`src/LayeredCraft.DecoWeaver.Generators/LayeredCraft.DecoWeaver.Generators.csproj`):

```xml
<None Include="$(OutputPath)\$(AssemblyName).dll" Pack="true" PackagePath="analyzers/dotnet/cs" Visible="false" />
```

It is packed exclusively under `analyzers/dotnet/cs` — the NuGet convention for compiler-host-only assemblies. It never ships to a consumer's `bin`/publish output and is never part of a consumer's runtime dependency graph or `deps.json`. It executes inside the Roslyn compiler process during the *consumer's own build*, on whatever TFM/runtime hosts their build (which has nothing to do with the *consumer application's* target framework or its AOT publish).

Consequently:
- No `net8.0` TFM is added to `Generators` solely to carry `IsAotCompatible`. `netstandard2.0` remains its target unless an independent reason (e.g. a Roslyn API requiring it) emerges.
- `IsAotCompatible` metadata on this assembly would prove nothing about consumer-application AOT safety anyway — it would only describe the analyzer's own (irrelevant) execution environment. Setting it would be a meaningless flag, not a stronger guarantee.
- The real, meaningful compatibility proof for everything this project *produces* is: generated consumer code is clean under AOT/trim analyzers (§6), the AOT validation app publishes with `PublishAot=true` and its native binary actually executes the generated decoration paths successfully (§7), enforced continuously in CI (§10). That is the load-bearing claim, not a package-level flag on the generator DLL.

### `LayeredCraft.DecoWeaver.Attributes` — multi-target `netstandard2.0;net8.0`, `IsAotCompatible=true` conditioned to `net8.0+`

Checked whether this project is actually present in a consumer's runtime graph, rather than assuming:

- `Generators.csproj` packs it as an ordinary runtime reference, not an analyzer: `<None Include="...LayeredCraft.DecoWeaver.Attributes.dll" Pack="true" PackagePath="lib/netstandard2.0" Visible="true" />`.
- Built `samples/DecoWeaver.Sample` and inspected its output: `LayeredCraft.DecoWeaver.Attributes.dll` is present in `bin/Release/net10.0/`, and `DecoWeaver.Sample.deps.json` lists `"LayeredCraft.DecoWeaver.Attributes/1.0.1"` as a runtime library dependency alongside the sample itself. **It is a real, ordinary runtime dependency in the consumer's default (untrimmed) output — not compile-time-only in practice, regardless of the `[Conditional]` attributes described below.**
- Read its full source (`DecoratedByAttribute.cs`, the project's only source file): five sealed `Attribute` subclasses (`DecoratedByAttribute<TDecorator>`, `DecoratedByAttribute`, `DecorateServiceAttribute`, `SkipAssemblyDecorationAttribute`, `DoNotDecorateAttribute`), every one marked `[Conditional("DECOWEAVER_EMIT_ATTRIBUTE_METADATA")]`. No logic beyond auto-property backing fields and constructors — no reflection, no dynamic code, nothing AOT-sensitive at all. `[Conditional]` means the compiler omits emitting the attribute *application* into a consumer's IL unless that symbol is defined (matching `CLAUDE.md`'s "zero metadata footprint" claim about attribute *usages*, which trimming can then remove from a published, trimmed binary) — but it does not stop the assembly itself from being a normal compile+runtime reference and shipping in default (non-trimmed) builds today, as confirmed above.

Given it is an ordinary runtime dependency by current packaging (not manufactured), and is trivially, provably AOT-safe on inspection: multi-target `Attributes` as `netstandard2.0;net8.0` (not `net10.0` — no independent reason found to need it), keeping `netstandard2.0` for existing consumers, and set `<IsAotCompatible Condition="$([MSBuild]::IsTargetFrameworkCompatible('$(TargetFramework)', 'net8.0'))">true</IsAotCompatible>` on the `net8.0` target only, once §6/§7 land and the validator passes. This is a real, tool-enforced signal for a real runtime asset — not a manufactured flag.

## 9. Coverage matrix

| Capability | JIT unit/integration | Source-generator snapshot test | AOT/trim analyzer | Native AOT publish | Native AOT **executed** |
|---|---|---|---|---|---|
| Basic (single) decoration | ✅ (existing) | ✅ (existing) | to add | to add | to add |
| Multiple decorators, ordered | ✅ | ✅ | to add | to add | to add |
| Open-generic decorator, closed registration | ✅ | ✅ | to add | to add | ✅ (issue #61 repro) |
| Multi-type-param open generic decorator | ✅ | ✅ | to add | — | — (represented by single-param case; same mechanism) |
| Closed generic registration (required path) | ✅ | ✅ | to add | to add | to add |
| Singleton / Scoped / Transient | ✅ | ✅ | — | — | ✅ (one representative each) |
| Constructor injection into decorator | ✅ | ✅ | — | — | ✅ |
| Keyed service decoration | ✅ | ✅ | — | — | ✅ (one case) |
| Factory-delegate registration | ✅ | ✅ | — | — | one case |
| Instance registration (singleton) | ✅ | ✅ | — | — | one case |
| Open generic *registration* (unsupported/fallback) | ✅ (asserts NOT intercepted) | ✅ | n/a | n/a | n/a — no DecoWeaver-owned dynamic code involved, not a supported path |

Not every permutation gets a native-executed case — only one representative per distinct implementation mechanism, per the task's own guidance, plus the exact issue #61 shape.

## 10. CI enforcement

Repo's PR build already delegates to a shared reusable workflow (`LayeredCraft/devops-templates/.github/workflows/pr-build.yaml@v10.5`) that this repo doesn't own — cannot add AOT publish/execute steps inside it. Proposed: a **new, repo-owned workflow** (`.github/workflows/native-aot-validation.yaml`), matching this repo's own convention for repo-specific workflows (see `docs.yml`: triggered on `pull_request`/`push` to `main`, path-filtered), that:
1. Sets up the pinned SDK (`global.json` versions already in use).
2. `dotnet publish test/LayeredCraft.DecoWeaver.AotValidation -c Release -r <rid> -p:PublishAot=true` and fails the job on any build/publish warning (`-warnaserror` scoped to the validation project, or a script that greps for `IL2`/`IL3` codes in the publish log — matching `references/test-app-validation.md`'s recommended pattern).
3. Executes the produced native binary directly; non-zero exit fails the job.
4. Runs on `ubuntu-latest` (linux-x64) only. **Decision (user, 2026-09-11):** linux-x64 is sufficient — the gate's purpose is proving analyzer-clean + publish + execute of representative paths, not platform-specific native behavior; no cross-platform RID matrix unless a platform-specific issue is found.

## 11. Documentation impact

- `docs/usage/open-generics.md` §"How It Works" (lines ~384–420) currently *documents the reflection-based mechanism as the implementation* (`typeof(CachingRepository<>).MakeGenericType(typeof(User))`, `Activator.CreateInstance(decoratorType, ...)`). This needs to be rewritten to reflect the closed-at-generator-time mechanism post-fix — this is public-facing "how it works" content, not internal mechanics, so it's in scope per the task's documentation instructions.
- A new `docs/advanced/` (or similar) page: Native AOT support statement — supported TFMs, "no special consumer action required," and the one legitimate boundary (constructor-injected dependencies inside decorators are still resolved via `ActivatorUtilities` at runtime — this is standard .NET DI behavior applying to *any* AOT-published DI consumer, not a DecoWeaver-specific limitation, and needs no annotation on the consumer's part).
- `CLAUDE.md` **does** need a change: the "Decorator Application Logic" section's step 4 ("Open generic decorators are closed at runtime via `MakeGenericType`") becomes factually incorrect once this fix lands and must be updated to describe generator-time closing instead.
- No `evals.json` exists in this repo for the `dotnet-aot-library-validation` skill itself, so no eval-update step applies.

## 12. Open questions — resolved

1. **`IsAotCompatible` scope** — resolved, per-project: `LayeredCraft.DecoWeaver.Generators` remains `netstandard2.0` only, no `IsAotCompatible` (analyzer, never in a consumer's runtime graph). `LayeredCraft.DecoWeaver.Attributes` targets `netstandard2.0;net8.0`, with `IsAotCompatible=true` conditioned to `net8.0+` (a real runtime dependency, trivially AOT-safe). See §8.
2. **CI RID scope** — resolved: `linux-x64` only. See §10.
3. `docs/usage/open-generics.md` "How It Works" rewrite — no objection raised; proceeding as planned in §11.

No public API change, no dropped capability, and no JIT-only carve-out were found to be necessary — so the corresponding stop-and-ask triggers in your instructions were not hit beyond the two items above, both now resolved.

## 13. ADR?

**Not recommended.** The fix is a generator-emission change (how the compiler emits already-known information) with no durable architectural decision a future maintainer needs a record of — it doesn't change the public API, the pipeline's data model in any consumer-visible way, or a load-bearing tradeoff future work must respect. It's documented sufficiently by this research doc plus normal PR history. Revisit only if implementation surfaces a genuine tradeoff (e.g., if plumbing type arguments through `TypeDefId`-shaped caching keys turns out to threaten incremental-generator cache-hit rates in a way that needs a durable rationale).
