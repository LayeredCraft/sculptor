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

## Step 7 — `IsAotCompatible` (superseded 2026-09-12 — see below)

> **Correction, post-review:** a `net8.0` multi-target + `IsAotCompatible=true` for `Attributes` was implemented per the original Step 7 (quoted below for the record), then **reverted** after PR review. A reviewer correctly identified that packing `Generators` from a clean checkout doesn't reliably produce the `net8.0` `Attributes` asset the new pack items referenced (MSBuild only builds the TFM(s) the referencing `netstandard2.0` project actually needs), and would have required extra cross-project build-ordering complexity to fix — exactly the kind of complexity the original design brief said not to add "merely to preserve the `net8.0` Attributes asset unless there is a concrete technical reason that asset is necessary." No such reason held up: `Attributes` is five `[Conditional]`-guarded marker classes with no logic; `IsAotCompatible` on it would only certify analyzer-cleanliness for a metadata-only assembly, not add to the real compatibility proof (the executed validator in Step 5/6). **Final state: both `Generators` and `Attributes` stay `netstandard2.0`-only, no `IsAotCompatible` on either.**
>
> A separate, unrelated CI failure was found and fixed in the same pass: `Generators.csproj`'s `Microsoft.CodeAnalysis.CSharp` reference (`5.9.0`, from prior Dependabot bumps) had drifted ahead of the Roslyn compiler bundled in the SDK pinned by `global.json` (`11.0.100-preview.3.26207.106`, bundling Roslyn `5.7.0`). `csc` silently refused to load the analyzer (`CS9057`), so DecoWeaverGenerator produced zero interceptors in CI — every decorator scenario resolved undecorated. Fixed by bumping `global.json` to `11.0.100-rc.1.26425.128` (confirmed to bundle a compatible Roslyn build). This fix is independent of the `Attributes` reversion; see `research/2026-09-11-native-aot-compatibility.md` §14 for full detail.

Original Step 7 (implemented, then reverted — kept for the record):

- ~~**`Generators`**: no TFM change. Stays `netstandard2.0` only...~~ (this part was correct and unchanged by the correction)
- ~~**`Attributes`**: multi-target `netstandard2.0;net8.0`... Set `<IsAotCompatible ...>true</IsAotCompatible>` on `Attributes` only, on the `net8.0` target only...~~ (reverted — see correction note above)

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
