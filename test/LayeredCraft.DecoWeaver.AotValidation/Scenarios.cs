// Representative DecoWeaver decorator scenarios for Native AOT validation.
// Each scenario is resolved through IServiceProvider and actually invoked — proving the generated
// decorator chain executes correctly in a published native binary, not just that it publishes.

using LayeredCraft.DecoWeaver.Attributes;

namespace LayeredCraft.DecoWeaver.AotValidation;

// --- Scenario 1: basic single decoration (scoped) --------------------------------------------

public interface IGreeter
{
    string Greet();
}

[DecoratedBy<UpperCaseGreeterDecorator>]
public sealed class Greeter : IGreeter
{
    public string Greet() => "hello";
}

public sealed class UpperCaseGreeterDecorator(IGreeter inner) : IGreeter
{
    public string Greet() => inner.Greet().ToUpperInvariant();
}

// --- Scenario 2: multiple ordered decorators + constructor-injected dependency (transient) ----

public interface ITag
{
    string Value { get; }
}

public sealed class Tag : ITag
{
    public string Value => "T";
}

public interface ICounter
{
    string Trace();
}

[DecoratedBy<MetricsCounterDecorator>(Order = 1)]
[DecoratedBy<LoggingCounterDecorator>(Order = 2)]
public sealed class Counter : ICounter
{
    public string Trace() => "Counter";
}

public sealed class MetricsCounterDecorator(ICounter inner, ITag tag) : ICounter
{
    public string Trace() => $"Metrics[{tag.Value}]({inner.Trace()})";
}

public sealed class LoggingCounterDecorator(ICounter inner) : ICounter
{
    public string Trace() => $"Logging({inner.Trace()})";
}

// --- Scenario 3: open-generic decorator over a closed generic registration (singleton) --------
// This is the exact shape reported in issue #61.

public sealed class Widget;

public interface IRepository<T>
{
    string Describe();
}

[DecoratedBy(typeof(CachingRepository<>))]
public sealed class Repository<T> : IRepository<T>
{
    public string Describe() => $"Repository<{typeof(T).Name}>";
}

public sealed class CachingRepository<T>(IRepository<T> inner) : IRepository<T>
{
    public string Describe() => $"Cache({inner.Describe()})";
}

// --- Scenario 4: multi-type-parameter open-generic decorator over a closed registration (scoped)
// Proves positional generic-argument substitution (ServiceTypeArgFqns -> ToClosedFqn) is correct
// for arity > 1, not just the single-type-parameter case in Scenario 3.

public interface IKeyValueStore<TKey, TValue>
{
    string Describe();
}

[DecoratedBy(typeof(CachingKeyValueStore<,>))] // note: comma for the second type parameter
public sealed class KeyValueStore<TKey, TValue> : IKeyValueStore<TKey, TValue>
{
    public string Describe() => $"Store<{typeof(TKey).Name},{typeof(TValue).Name}>";
}

public sealed class CachingKeyValueStore<TKey, TValue>(IKeyValueStore<TKey, TValue> inner) : IKeyValueStore<TKey, TValue>
{
    public string Describe() => $"Cache({inner.Describe()})";
}

// --- Scenario 5: keyed service decoration (scoped) ------------------------------------------

public interface IPricer
{
    string Price();
}

[DecoratedBy<AuditedPricerDecorator>]
public sealed class Pricer : IPricer
{
    public string Price() => "10";
}

public sealed class AuditedPricerDecorator(IPricer inner) : IPricer
{
    public string Price() => $"Audited({inner.Price()})";
}

// --- Scenario 6: factory-delegate registration (scoped) --------------------------------------

public interface IClock
{
    string Now();
}

[DecoratedBy<TracingClockDecorator>]
public sealed class Clock : IClock
{
    public string Now() => "T0";
}

public sealed class TracingClockDecorator(IClock inner) : IClock
{
    public string Now() => $"Trace({inner.Now()})";
}

// --- Scenario 7: instance registration (singleton-only in .NET DI) ---------------------------

public interface INotifier
{
    string Notify();
}

[DecoratedBy<RetryingNotifierDecorator>]
public sealed class Notifier : INotifier
{
    public string Notify() => "sent";
}

public sealed class RetryingNotifierDecorator(INotifier inner) : INotifier
{
    public string Notify() => $"Retry({inner.Notify()})";
}
