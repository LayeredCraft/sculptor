# Native AOT Compatibility — Implementation Plan

Companion to `research/2026-09-11-native-aot-compatibility.md`. One cohesive effort, single PR unless review says otherwise — no architectural reason found to split it.

Do not start until research doc is reviewed and open questions (§12 of research doc) are answered.

## Step 1 — Plumb closed type arguments through the pipeline

- `Model/DecoratorToIntercept.cs` / `Providers/ClosedGenericRegistrationProvider.cs`: keep `TypeDefId`-based caching keys as-is (don't disturb incremental-cache identity), but carry the service's resolved closed type-argument FQNs (`string[]`) alongside each `ClosedGenericRegistration`, sourced from the same `ServiceFqn`-producing code path (`svc.ToDisplayString(...)` already has the info; need the individual `TypeArguments[i].ToDisplayString(FullyQualifiedFormat)` list, not just the joined string).
- `InterceptorEmitter.EmitClosedGenericInterceptors`: when building `decorators` for a registration, for each `TypeDefId` with `Arity > 0`, substitute the registration's own type-argument FQNs positionally into the decorator name (mirrors today's runtime `MakeGenericType(serviceType.GetGenericArguments())` semantics — same positional mapping, just done at generator time). Arity-0 decorators unaffected.
- Verify against multi-type-param case (`[DecoratedBy<CachingKeyValueStore<,>>]`, test case docs example) — positional substitution must handle arity 2+.

## Step 2 — Replace `DecoratorFactory` indirection with direct emission

- `Templates/DecoWeaverInterceptors.scriban`: delete `DecoratorKeys`'s neighbor `DecoratorFactory`/`CloseIfNeeded` static class entirely.
- Replace every `current = ({{ reg.service_fqn }})DecoratorFactory.Create(sp, typeof({{ reg.service_fqn }}), typeof({{ decorator }}), current);` line with a direct call using the now-closed `{{ decorator }}` (no more `<,,>` — always a fully closed or non-generic FQN):
  ```
  current = ({{ reg.service_fqn }})ActivatorUtilities.CreateInstance(sp, typeof({{ decorator }}), current)!;
  ```
- Confirm `typeof(...)` stays a syntactic literal directly in the `ActivatorUtilities.CreateInstance` argument position (not assigned to an intermediate local first) — that's what makes the trim analyzer resolve it without annotation.

## Step 3 — Regenerate and update snapshots

- `dotnet run --project test/LayeredCraft.DecoWeaver.Generator.Tests/... --framework net8.0` (and net9.0/net10.0) with Verify's accept mode; review every diffed snapshot by hand — expect changes in every case with `has_decorators` true (roughly cases 001–021, 023–051 per the earlier directory listing).
- Confirm no snapshot still contains `MakeGenericType`, `CloseIfNeeded`, or `DecoratorFactory`.
- Add new case `052_AotFriendly_DecoratorConstruction` (or reuse an existing open-generic case) with an explicit assertion/snapshot check that the generated decorator lines are direct `ActivatorUtilities.CreateInstance(sp, typeof(Closed<Args>), current)` calls — pins the fix at the fast test layer.

## Step 4 — Update the sample and docs

- `samples/DecoWeaver.Sample`: no code changes required (decorator usage unchanged); rebuild to confirm generated output matches the new shape.
- `docs/usage/open-generics.md` "How It Works" section (~lines 384–420): rewrite to describe generator-time closing instead of `MakeGenericType`/`Activator.CreateInstance` at runtime.
- `CLAUDE.md` "Decorator Application Logic" step 4: update "closed at runtime via `MakeGenericType`" → "closed at generator time."
- New docs page (location/nav per your open question #3 resolution): Native AOT support statement.

## Step 5 — Add the AOT validation project

- `test/LayeredCraft.DecoWeaver.AotValidation/LayeredCraft.DecoWeaver.AotValidation.csproj` — console app, shape modeled on `samples/DecoWeaver.Sample.csproj` (ProjectReference generator as Analyzer + Attributes, `Microsoft.Extensions.DependencyInjection`), plus:
  ```xml
  <PublishAot>true</PublishAot>
  <TrimmerRootAssembly Include="LayeredCraft.DecoWeaver.AotValidation" />
  ```
- `Program.cs`: build representative registrations per the coverage matrix (research doc §9) — basic decoration, multiple ordered decorators, open-generic decorator over closed registration (the issue #61 shape), multi-param generic, all three lifetimes, keyed, factory-delegate, instance registration. Resolve each, invoke a method, assert against an expected marker (e.g. ordered log string), `Environment.Exit(1)` with a clear message on any failure.
- Add solution reference in `LayeredCraft.DecoWeaver.sln.slnx` if the tooling requires explicit inclusion (check existing pattern for `test/`/`samples/` entries first).

## Step 6 — CI

- New `.github/workflows/native-aot-validation.yaml`, `ubuntu-latest`/`linux-x64` only (decided — no cross-platform matrix): build + `dotnet publish -r linux-x64 -c Release -p:PublishAot=true` the validation project, fail on any `IL2`/`IL3` warning in the publish log, then execute the produced binary and fail on non-zero exit. Trigger on PR (path-filtered to `src/**`, `test/LayeredCraft.DecoWeaver.AotValidation/**`) plus push to `main`, matching `docs.yml`'s trigger convention.
- Confirm SDK version pin matches `global.json`.

## Step 7 — `IsAotCompatible`

- **`Generators`**: no TFM change. Stays `netstandard2.0` only — it's packed to `analyzers/dotnet/cs`, never ships to a consumer's runtime/publish output, and never appears in a consumer's `deps.json` (confirmed by inspecting the csproj pack targets and the sample's build output). No `IsAotCompatible` property added to it — it would describe the analyzer's own compiler-host execution, not consumer-application AOT safety, and would be a meaningless flag.
- **`Attributes`**: multi-target `netstandard2.0;net8.0` (not `net10.0` — no independent reason found). Confirmed via `Generators.csproj`'s pack targets (packed to `lib/netstandard2.0`, a real runtime reference) and by building `samples/DecoWeaver.Sample`: `LayeredCraft.DecoWeaver.Attributes.dll` ships in `bin/`, and appears in `DecoWeaver.Sample.deps.json` as a runtime library — it's a genuine, ordinary runtime dependency today, not manufactured. Its only source file is five `[Conditional]`-guarded, logic-free `Attribute` subclasses — trivially AOT-safe.
- Set `<IsAotCompatible Condition="$([MSBuild]::IsTargetFrameworkCompatible('$(TargetFramework)', 'net8.0'))">true</IsAotCompatible>` on `Attributes` only, on the `net8.0` target only, after Steps 1-6 land and the native-executed validator (Step 5/6) passes clean — this is a claim backed by that evidence, not asserted ahead of it.
- Check `netstandard2.0`-only consumer paths (Polyfill-dependent code, any `#if`/conditional compilation) in `Attributes` aren't broken by adding the second TFM — build both TFMs after the change.

## Step 8 — Full verification pass

- `dotnet build` full solution, all TFMs, zero new warnings.
- `dotnet run --project test/... --framework net8.0/net9.0/net10.0` — full JIT suite green.
- `dotnet publish` the AOT validation project and execute the binary — exit 0.
- Manually re-read `docs/usage/open-generics.md` and `CLAUDE.md` diffs for accuracy against the actual final generated output (copy-paste an actual generated snippet, don't hand-write the doc example).

## Out of scope / explicitly not doing

- No public API changes (none were found necessary).
- No suppression-based fixes (`[UnconditionalSuppressMessage]`) — elimination fully covers the known surface.
- No cross-platform (win-x64/osx-arm64) native CI execution unless you ask for it in answering §12 Q2.
- No changes to `ClosedGenericRegistrationProvider`'s validation logic (which registration shapes are supported) — orthogonal to this effort.
