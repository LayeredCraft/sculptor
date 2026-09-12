// Native AOT validation entry point.
//
// Builds a real ServiceCollection, registers DecoWeaver-decorated services across the
// representative scenarios in Scenarios.cs, resolves each one through IServiceProvider, and
// invokes it — proving the generated decorator chain actually executes correctly in a published
// native binary. A clean `dotnet publish -p:PublishAot=true` alone would not prove this; this
// program must run, and every check must pass, for the Native AOT compatibility claim to hold.

using LayeredCraft.DecoWeaver.AotValidation;
using Microsoft.Extensions.DependencyInjection;

var failures = new List<string>();

void Check(string scenario, string actual, string expected)
{
    if (actual == expected)
    {
        Console.WriteLine($"[PASS] {scenario}: {actual}");
    }
    else
    {
        Console.WriteLine($"[FAIL] {scenario}: expected \"{expected}\", got \"{actual}\"");
        failures.Add(scenario);
    }
}

var services = new ServiceCollection();

// Scenario 1: basic single decoration (scoped)
services.AddScoped<IGreeter, Greeter>();

// Scenario 2: multiple ordered decorators + constructor-injected dependency (transient)
services.AddSingleton<ITag, Tag>();
services.AddTransient<ICounter, Counter>();

// Scenario 3: open-generic decorator over a closed generic registration (singleton) — issue #61
services.AddSingleton<IRepository<Widget>, Repository<Widget>>();

// Scenario 4: multi-type-parameter open-generic decorator over a closed registration (scoped)
services.AddScoped<IKeyValueStore<string, int>, KeyValueStore<string, int>>();

// Scenario 5: keyed service decoration (scoped)
services.AddKeyedScoped<IPricer, Pricer>("primary");

// Scenario 6: factory-delegate registration (scoped)
services.AddScoped<IClock, Clock>(_ => new Clock());

// Scenario 7: instance registration (singleton-only in .NET DI)
services.AddSingleton<INotifier>(new Notifier());

await using var provider = services.BuildServiceProvider();

Check("Basic decoration", provider.GetRequiredService<IGreeter>().Greet(), "HELLO");
Check("Multiple ordered decorators", provider.GetRequiredService<ICounter>().Trace(), "Logging(Metrics[T](Counter))");
Check("Open-generic decorator over closed registration (#61)",
    provider.GetRequiredService<IRepository<Widget>>().Describe(), "Cache(Repository<Widget>)");
Check("Multi-type-parameter open-generic decorator",
    provider.GetRequiredService<IKeyValueStore<string, int>>().Describe(), "Cache(Store<String,Int32>)");
Check("Keyed service decoration", provider.GetRequiredKeyedService<IPricer>("primary").Price(), "Audited(10)");
Check("Factory-delegate registration", provider.GetRequiredService<IClock>().Now(), "Trace(T0)");
Check("Instance registration", provider.GetRequiredService<INotifier>().Notify(), "Retry(sent)");

if (failures.Count > 0)
{
    Console.WriteLine($"\n{failures.Count} scenario(s) FAILED: {string.Join(", ", failures)}");
    return 1;
}

Console.WriteLine("\nAll Native AOT decorator scenarios passed.");
return 0;
