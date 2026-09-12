# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

LayeredCraft.DecoWeaver is a .NET incremental source generator that enables **compile-time decorator registration** for dependency injection. It uses C# 11+ interceptors to automatically wrap service implementations with decorators at build time, eliminating runtime reflection and assembly scanning.

## Build Commands

```bash
# Restore NuGet packages
dotnet restore

# Build the entire solution
dotnet build

# Run all tests (requires framework specification for MTP)
dotnet run --project test/LayeredCraft.DecoWeaver.Generator.Tests/LayeredCraft.DecoWeaver.Generator.Tests.csproj --framework net8.0

# Run tests on specific framework
dotnet run --project test/LayeredCraft.DecoWeaver.Generator.Tests/LayeredCraft.DecoWeaver.Generator.Tests.csproj --framework net9.0
dotnet run --project test/LayeredCraft.DecoWeaver.Generator.Tests/LayeredCraft.DecoWeaver.Generator.Tests.csproj --framework net10.0

# Build in release mode
dotnet build -c Release

# Clean build artifacts
dotnet clean
```

**Note**: The test project uses Microsoft Testing Platform (MTP) and targets multiple frameworks (net8.0, net9.0, net10.0), so you must specify the `--framework` flag when running tests.

## Project Structure

The solution contains four main projects:

1. **src/LayeredCraft.DecoWeaver.Attributes** (netstandard2.0)
   - Defines `[DecoratedBy<T>]` and `[DecoratedBy(typeof(...))]` attributes
   - Consumer-facing API with minimal dependencies
   - Uses Polyfill for modern C# features on older targets

2. **src/LayeredCraft.DecoWeaver.Generators** (netstandard2.0)
   - The incremental source generator implementation
   - Depends on Microsoft.CodeAnalysis.CSharp 4.14.0
   - Emits interceptor code to `DecoWeaver.Interceptors.ClosedGenerics.g.cs`

3. **samples/DecoWeaver.Sample** (net10.0)
   - Demonstrates closed generic registration with open generic decorators
   - References both Attributes and Generators projects

4. **test/LayeredCraft.DecoWeaver.Generator.Tests** (net8.0)
   - XUnit v3 tests with snapshot verification infrastructure
   - Uses AutoFixture + NSubstitute for test data/mocking
   - Multi-framework BCL reference assemblies for accurate compilation testing

## High-Level Architecture

### Source Generator Pipeline

The generator follows an incremental generation pipeline with these key stages:

1. **Language Version Gate**: Validates C# 11+ required for interceptors
2. **Attribute Discovery**: Two parallel streams discover decorators:
   - `DecoratedByGenericProvider`: Handles `[DecoratedBy<TDecorator>]`
   - `DecoratedByNonGenericProvider`: Handles `[DecoratedBy(typeof(...))]`
3. **Registration Discovery**: `ClosedGenericRegistrationProvider` finds DI registrations like `AddScoped<IRepository<User>, Repository<User>>()`
4. **Data Combination**: Maps implementations to their decorators, ordered by priority
5. **Code Emission**: Generates interceptor methods with `[InterceptsLocation]` attributes

### Key Design Patterns

**Type Identity System**:
- `TypeDefId`: Definition-only identity (no type arguments) - stable across compilations
- `TypeId`: Full type with type arguments
- Handles open generics, nested types, and type parameters

**Incremental Optimizations**:
- Predicates use syntax-only checks (no semantic model)
- Transformers are pure functions with semantic analysis
- `EquatableArray<T>` for efficient change detection
- Language gate applied before expensive `Collect()` operations

**Interceptor Generation**:
- Generates methods that match the signature of `ServiceCollectionServiceExtensions.AddScoped/Transient/Singleton`
- Uses `[InterceptsLocation]` to redirect specific call sites
- Registers implementation with keyed service to prevent circular resolution
- Factory registration applies decorators in ascending order (by Order property)

### Decorator Application Logic

For each decorated implementation, the generator:
1. Registers the undecorated implementation as a keyed service
2. Registers a factory that resolves the keyed service and wraps it with decorators
3. Decorators are applied in ascending order (innermost to outermost)
4. Open generic decorators are closed by the generator at compile time (positionally, against the registration's own closed service type arguments) and emitted as a literal, closed `typeof(...)` — no runtime `MakeGenericType`/reflection-based generic closing, which keeps generated code Native AOT/trim-analyzer clean

## Testing Approach

### Test Case Organization

Tests live in `/test/Cases/{NNN}_{Description}/` directories:
- Each case contains multiple .cs files (e.g., Repository.cs, Program.cs)
- Cases are excluded from compilation but copied to output directory
- `GlobalUsings.g.cs` applied to all test cases

### Test Infrastructure

**GeneratorTestHelpers.cs**:
- `RunFromCases()`: Core harness that creates compilation, adds references, runs generator
- `RecompileWithGeneratedTrees()`: Re-parses generated code to validate correctness
- Multi-framework BCL references (Net80/90/100) for accurate testing

**Test Patterns**:
- Use `[GeneratorAutoData]` attribute for parameterized tests
- Call `VerifyGlue.VerifySourcesAsync()` to run generator and assert no compilation errors
- Snapshot testing infrastructure present (currently validates via compilation success)

### Adding New Test Cases

1. Create directory `/test/Cases/{NextNumber}_{Description}/`
2. Add source files demonstrating the scenario
3. Create xunit test method with `[GeneratorAutoData]`
4. Call `VerifyGlue.VerifySourcesAsync(generator, casePaths)`

## Development Workflow

### Working on Generator Code

1. Modify files in `/src/LayeredCraft.DecoWeaver.Generators/`
2. Key files to understand:
   - `DecoWeaverGenerator.cs`: Main pipeline orchestration
   - `Providers/`: Syntax/semantic analysis for discovery
   - `Emit/InterceptorEmitter.cs`: Code generation logic
   - `Model/`: Data structures passed through pipeline
3. Run tests to validate changes
4. Debug builds emit generated files to `obj/Debug/netstandard2.0/generated/`

### Code Organization

- `/Providers/`: Discovery logic (predicates + transformers)
- `/Model/`: Immutable data structures for pipeline
- `/Emit/`: Code generation
- `/Roslyn/`: Roslyn API adapters and type system handling
- `/Util/`: Shared utilities (EquatableArray)

### Debugging

- Attach debugger to test process
- Use `TrackingNames` constants to identify pipeline stages in debugger
- Check `obj/` output directory for generated files when debugging generator itself
- Set breakpoints in provider transformers to inspect semantic analysis

## Important Technical Details

### C# Interceptors Feature

- Requires C# 11+ (experimental feature)
- `[InterceptsLocation(version: 1, data: "file|start|length")]` redirects specific call sites
- File-scoped types (`file sealed class`) prevent collisions
- Multiple `[InterceptsLocation]` attributes on one method intercept multiple call sites

### Keyed Services Strategy

- Keyed services (NET 8+) prevent infinite recursion during resolution
- Key format: `"{ServiceAssemblyQualifiedName}|{ImplAssemblyQualifiedName}"`
- Undecorated implementation registered with key
- Factory registration wraps keyed service with decorators

### Generic Type Decoration

- **IMPORTANT**: Generator ONLY intercepts **closed generic registrations** using the `AddScoped<TService, TImplementation>()` syntax
- Open generic registrations using `AddScoped(typeof(IRepo<>), typeof(SqlRepo<>))` are **NOT intercepted**
- Decorator types CAN be open generics: `[DecoratedBy<CachingRepo<>>]`
- Open generic decorators are closed at runtime via `MakeGenericType` when the service is resolved
- Type arguments extracted from the service type being resolved

**Supported Registration Patterns**:
```csharp
// ✅ Closed generic registration (parameterless) - INTERCEPTED by DecoWeaver
services.AddScoped<IRepository<User>, Repository<User>>();
services.AddScoped<IRepository<Product>, Repository<Product>>();

// ✅ Closed generic registration with factory delegate - INTERCEPTED by DecoWeaver (v1.0.2+)
services.AddScoped<IRepository<User>, Repository<User>>(sp => new Repository<User>());
services.AddScoped<IRepository<User>>(sp => new Repository<User>());

// ✅ Keyed service registration - INTERCEPTED by DecoWeaver (v1.0.3+)
services.AddKeyedScoped<IRepository<User>, Repository<User>>("sql");
services.AddKeyedScoped<IRepository<User>, Repository<User>>("sql", (sp, key) => new Repository<User>());

// ✅ Instance registration (singleton only) - INTERCEPTED by DecoWeaver (v1.0.4+)
services.AddSingleton<IRepository<User>>(new Repository<User>());

// ❌ Open generic registration - NOT intercepted
services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
```

### Factory Delegate Support (v1.0.2+)

DecoWeaver supports factory delegate registrations for all three lifetimes:

**Two-parameter generic factory**:
```csharp
services.AddScoped<IRepository<T>, Repository<T>>(sp => new Repository<T>(...));
services.AddTransient<IRepository<T>, Repository<T>>(sp => new Repository<T>(...));
services.AddSingleton<IRepository<T>, Repository<T>>(sp => new Repository<T>(...));
```

**Single-parameter generic factory**:
```csharp
services.AddScoped<IRepository<T>>(sp => new Repository<T>(...));
```

**Complex dependencies** are supported - factories can resolve dependencies from `IServiceProvider`:
```csharp
services.AddScoped<IRepository<User>, Repository<User>>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Repository<User>>>();
    var config = sp.GetRequiredService<IConfiguration>();
    return new Repository<User>(logger, config);
});
```

Decorators are applied around the factory result, and the factory logic is preserved.

### Keyed Service Support (v1.0.3+)

DecoWeaver supports keyed service registrations for all three lifetimes. Keyed services allow multiple implementations of the same service type to be registered with different keys.

**Keyed parameterless registration**:
```csharp
services.AddKeyedScoped<IRepository<User>, SqlRepository<User>>("sql");
services.AddKeyedScoped<IRepository<User>, CosmosRepository<User>>("cosmos");
services.AddKeyedTransient<ICache<T>, RedisCache<T>>("redis");
services.AddKeyedSingleton<ILogger<T>, FileLogger<T>>("file");
```

**Keyed with factory delegate**:
```csharp
// Two-parameter keyed factory: Func<IServiceProvider, object?, TService>
services.AddKeyedScoped<IRepository<User>, SqlRepository<User>>(
    "sql",
    (sp, key) => new SqlRepository<User>("Server=sql01;Database=Users")
);

// Single-parameter keyed factory: Func<IServiceProvider, object?, TService>
services.AddKeyedScoped<IRepository<User>>(
    "sql",
    (sp, key) => new SqlRepository<User>("Server=sql01;Database=Users")
);
```

**Nested key strategy**:
- User's original key is preserved for resolution via `GetRequiredKeyedService(userKey)`
- Internally, DecoWeaver creates a nested key: `"{userKey}|{ServiceAQN}|{ImplAQN}"`
- Undecorated implementation registered with nested key to prevent circular resolution
- Decorated factory registered with user's original key
- Each key gets independent decorator chain - no sharing between keys

**Example resolution**:
```csharp
// User code (exactly as before)
var sqlRepo = serviceProvider.GetRequiredKeyedService<IRepository<User>>("sql");

// What DecoWeaver generates internally:
// 1. Register undecorated: AddKeyedScoped<...>("sql|IRepository`1|SqlRepository`1")
// 2. Register decorated: AddKeyedScoped<...>("sql", (sp, key) => {
//       var current = sp.GetRequiredKeyedService<...>("sql|IRepository`1|SqlRepository`1");
//       current = DecoratorFactory.Create(sp, typeof(...), typeof(LoggingDecorator<>), current);
//       return current;
//    })
```

**Key type support**:
- All key types supported: `string`, `int`, `enum`, custom objects
- Multiple keys for same service type work independently
- Each keyed registration is intercepted separately

### Instance Registration Support (v1.0.4+)

DecoWeaver supports singleton instance registrations. This allows decorators to be applied to pre-created instances.

**Instance registration**:
```csharp
// ✅ Supported - AddSingleton with instance
var instance = new SqlRepository<Customer>();
services.AddSingleton<IRepository<Customer>>(instance);

// ❌ NOT supported - AddScoped/AddTransient don't have instance overloads in .NET DI
services.AddScoped<IRepository<Customer>>(instance); // Compiler error
services.AddTransient<IRepository<Customer>>(instance); // Compiler error
```

**How it works**:
- Instance type is extracted from the actual argument expression using `SemanticModel.GetTypeInfo(instanceArg).Type`
- Instance is registered directly as a keyed service (preserves disposal semantics)
- Decorators are applied around the instance just like other registration types

**Generated code example**:
```csharp
// User code:
services.AddSingleton<IRepository<Customer>>(new SqlRepository<Customer>());

// What DecoWeaver generates:
var key = DecoratorKeys.For(typeof(IRepository<Customer>), typeof(SqlRepository<Customer>));
var capturedInstance = (IRepository<Customer>)(object)implementationInstance;
services.AddKeyedSingleton<IRepository<Customer>>(key, capturedInstance);

services.AddSingleton<IRepository<Customer>>(sp =>
{
    var current = sp.GetRequiredKeyedService<IRepository<Customer>>(key);
    current = (IRepository<Customer>)DecoratorFactory.Create(sp, typeof(IRepository<Customer>), typeof(LoggingRepository<>), current);
    return current;
});
```

**Limitations**:
- Only `AddSingleton` is supported (instance registrations don't exist for Scoped/Transient in .NET DI)
- The instance must be created before registration (can't use DI for instance construction)
- All resolutions return decorators wrapping the same singleton instance

### Attribute Compilation

- Attributes marked with `[Conditional("DECOWEAVER_EMIT_ATTRIBUTE_METADATA")]`
- Don't exist at runtime - zero metadata footprint
- Only affect compile-time behavior

## Requirements

- .NET SDK with C# 11+ support
- Consuming projects must set `<LangVersion>` to 11, latest, or preview
- Keyed services feature requires .NET 8+ runtime
- Test project requires .NET 8 SDK

## Naming Conventions

- Diagnostics: `DECOW###` prefix
- Tracking names: `Category_Subcategory_Action` format
- Type identity: `TypeDefId` (definition), `TypeId` (with args)
- Error handling: Return `null` from transformers on invalid input