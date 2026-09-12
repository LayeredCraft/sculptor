using LayeredCraft.DecoWeaver;
using LayeredCraft.DecoWeaver.Generator.Tests.TestKit.Attributes;

namespace LayeredCraft.DecoWeaver.Generator.Tests;

public class DecoWeaverGeneratorTests
{
    private static readonly Dictionary<string, string>? FeatureFlags = new()
    {
        ["InterceptorsPreviewNamespaces"] = "LayeredCraft.DecoWeaver.Generated",
        ["InterceptorsNamespaces"] = "LayeredCraft.DecoWeaver.Generated",
    };

    [Theory]
    [GeneratorAutoData]
    public async Task OpenGeneric_SingleDecorator_GeneratesCorrectInterceptor(DecoWeaverGenerator sut)
    {
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/001_OpenGeneric_SingleDecorator/Repository.cs",
                "Cases/001_OpenGeneric_SingleDecorator/Program.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task OpenGeneric_MultipleOrdered_GeneratesCorrectInterceptor(DecoWeaverGenerator sut)
    {
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/002_OpenGeneric_MultipleOrdered/Repository.cs",
                "Cases/002_OpenGeneric_MultipleOrdered/Program.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task OpenGeneric_ConstructorOrderSyntax_GeneratesCorrectInterceptor(DecoWeaverGenerator sut)
    {
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/003_OpenGeneric_ConstructorOrderSyntax/Repository.cs",
                "Cases/003_OpenGeneric_ConstructorOrderSyntax/Program.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task OpenGeneric_MultipleDefaultOrder_GeneratesCorrectInterceptor(DecoWeaverGenerator sut)
    {
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/004_OpenGeneric_MultipleDefaultOrder/Repository.cs",
                "Cases/004_OpenGeneric_MultipleDefaultOrder/Program.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task OpenGeneric_MixedOrderSyntax_GeneratesCorrectInterceptor(DecoWeaverGenerator sut)
    {
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/005_OpenGeneric_MixedOrderSyntax/Repository.cs",
                "Cases/005_OpenGeneric_MixedOrderSyntax/Program.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task OpenGeneric_NonGenericDecorator_GeneratesCorrectInterceptor(DecoWeaverGenerator sut)
    {
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/006_OpenGeneric_NonGenericDecorator/Repository.cs",
                "Cases/006_OpenGeneric_NonGenericDecorator/Program.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task OpenGeneric_IsInterceptableFalse_NoInterceptorGenerated(DecoWeaverGenerator sut)
    {
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/007_OpenGeneric_IsInterceptableFalse/Repository.cs",
                "Cases/007_OpenGeneric_IsInterceptableFalse/Program.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task OpenGeneric_NoDecorators_PassThrough(DecoWeaverGenerator sut)
    {
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/008_OpenGeneric_NoDecorators/Repository.cs",
                "Cases/008_OpenGeneric_NoDecorators/Program.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task OpenGeneric_ThreeDecorators_GeneratesCorrectNesting(DecoWeaverGenerator sut)
    {
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/009_OpenGeneric_ThreeDecorators/Repository.cs",
                "Cases/009_OpenGeneric_ThreeDecorators/Program.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task Generic_SingleDecorator_GeneratesCorrectInterceptor(DecoWeaverGenerator sut)
    {
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/010_Generic_SingleDecorator/Repository.cs",
                "Cases/010_Generic_SingleDecorator/Program.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task Generic_MultipleOrdered_GeneratesCorrectInterceptor(DecoWeaverGenerator sut)
    {
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/011_Generic_MultipleOrdered/Repository.cs",
                "Cases/011_Generic_MultipleOrdered/Program.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task Generic_MixedSyntax_GeneratesCorrectInterceptor(DecoWeaverGenerator sut)
    {
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/012_Generic_MixedSyntax/Repository.cs",
                "Cases/012_Generic_MixedSyntax/Program.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task Generic_IsInterceptableFalse_NoInterceptorGenerated(DecoWeaverGenerator sut)
    {
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/013_Generic_IsInterceptableFalse/Repository.cs",
                "Cases/013_Generic_IsInterceptableFalse/Program.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task Transient_SingleDecorator_GeneratesCorrectInterceptor(DecoWeaverGenerator sut)
    {
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/014_Transient_SingleDecorator/Repository.cs",
                "Cases/014_Transient_SingleDecorator/Program.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task Transient_MultipleDecorators_GeneratesCorrectInterceptor(DecoWeaverGenerator sut)
    {
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/015_Transient_MultipleDecorators/Repository.cs",
                "Cases/015_Transient_MultipleDecorators/Program.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task Singleton_SingleDecorator_GeneratesCorrectInterceptor(DecoWeaverGenerator sut)
    {
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/016_Singleton_SingleDecorator/Repository.cs",
                "Cases/016_Singleton_SingleDecorator/Program.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task Singleton_MultipleDecorators_GeneratesCorrectInterceptor(DecoWeaverGenerator sut)
    {
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/017_Singleton_MultipleDecorators/Repository.cs",
                "Cases/017_Singleton_MultipleDecorators/Program.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task Transient_Generic_SingleDecorator_GeneratesCorrectInterceptor(DecoWeaverGenerator sut)
    {
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/018_Transient_Generic_SingleDecorator/Repository.cs",
                "Cases/018_Transient_Generic_SingleDecorator/Program.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task Transient_Generic_MultipleDecorators_GeneratesCorrectInterceptor(DecoWeaverGenerator sut)
    {
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/019_Transient_Generic_MultipleDecorators/Repository.cs",
                "Cases/019_Transient_Generic_MultipleDecorators/Program.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task Singleton_Generic_SingleDecorator_GeneratesCorrectInterceptor(DecoWeaverGenerator sut)
    {
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/020_Singleton_Generic_SingleDecorator/Repository.cs",
                "Cases/020_Singleton_Generic_SingleDecorator/Program.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task Singleton_Generic_MultipleDecorators_GeneratesCorrectInterceptor(DecoWeaverGenerator sut)
    {
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/021_Singleton_Generic_MultipleDecorators/Repository.cs",
                "Cases/021_Singleton_Generic_MultipleDecorators/Program.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task FactoryDelegate_SingleDecorator(DecoWeaverGenerator sut)
    {
        // This test verifies that registrations with factory delegates ARE intercepted (Phase 1)
        // Factory delegate overloads are now supported alongside parameterless overloads
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/022_FactoryDelegate_SingleDecorator/Repository.cs",
                "Cases/022_FactoryDelegate_SingleDecorator/Program.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task ServiceDecorator_SingleDecorator_GeneratesCorrectInterceptor(DecoWeaverGenerator sut)
    {
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/023_ServiceDecorator_SingleDecorator/Repository.cs",
                "Cases/023_ServiceDecorator_SingleDecorator/Program.cs",
                "Cases/023_ServiceDecorator_SingleDecorator/Global.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task ServiceDecorator_MultipleOrdered_GeneratesCorrectInterceptor(DecoWeaverGenerator sut)
    {
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/024_ServiceDecorator_MultipleOrdered/Repository.cs",
                "Cases/024_ServiceDecorator_MultipleOrdered/Program.cs",
                "Cases/024_ServiceDecorator_MultipleOrdered/Global.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task ServiceDecorator_WithSkipAssembly_GeneratesCorrectInterceptor(DecoWeaverGenerator sut)
    {
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/025_ServiceDecorator_WithSkipAssembly/Repository.cs",
                "Cases/025_ServiceDecorator_WithSkipAssembly/Program.cs",
                "Cases/025_ServiceDecorator_WithSkipAssembly/Global.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task ServiceDecorator_SkipAssemblyWithClassLevel_GeneratesCorrectInterceptor(DecoWeaverGenerator sut)
    {
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/026_SkipAssemblyWithClassLevel/Repository.cs",
                "Cases/026_SkipAssemblyWithClassLevel/Program.cs",
                "Cases/026_SkipAssemblyWithClassLevel/Global.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task MergePrecedence_Deduplication_GeneratesCorrectInterceptor(DecoWeaverGenerator sut)
    {
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/027_MergePrecedence_Deduplication/Repository.cs",
                "Cases/027_MergePrecedence_Deduplication/Program.cs",
                "Cases/027_MergePrecedence_Deduplication/Global.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task MergePrecedence_SortOrder_GeneratesCorrectInterceptor(DecoWeaverGenerator sut)
    {
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/028_MergePrecedence_SortOrder/Repository.cs",
                "Cases/028_MergePrecedence_SortOrder/Program.cs",
                "Cases/028_MergePrecedence_SortOrder/Global.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task DoNotDecorate_RemovesAssemblyDecorator_GeneratesCorrectInterceptor(DecoWeaverGenerator sut)
    {
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/029_DoNotDecorate_RemovesAssemblyDecorator/Repository.cs",
                "Cases/029_DoNotDecorate_RemovesAssemblyDecorator/Program.cs",
                "Cases/029_DoNotDecorate_RemovesAssemblyDecorator/Global.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task DoNotDecorate_Multiple_GeneratesCorrectInterceptor(DecoWeaverGenerator sut)
    {
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/030_DoNotDecorate_Multiple/Repository.cs",
                "Cases/030_DoNotDecorate_Multiple/Program.cs",
                "Cases/030_DoNotDecorate_Multiple/Global.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task DoNotDecorate_OpenGenericMatching_GeneratesCorrectInterceptor(DecoWeaverGenerator sut)
    {
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/031_DoNotDecorate_OpenGenericMatching/Repository.cs",
                "Cases/031_DoNotDecorate_OpenGenericMatching/Program.cs",
                "Cases/031_DoNotDecorate_OpenGenericMatching/Global.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task DoNotDecorate_IsolationCheck_GeneratesCorrectInterceptor(DecoWeaverGenerator sut)
    {
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/032_DoNotDecorate_IsolationCheck/Repository.cs",
                "Cases/032_DoNotDecorate_IsolationCheck/Program.cs",
                "Cases/032_DoNotDecorate_IsolationCheck/Global.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task FactoryDelegate_SingleGenericParam(DecoWeaverGenerator sut)
    {
        // Test single type parameter factory: AddScoped<T>(factory)
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/033_FactoryDelegate_SingleGenericParam/Repository.cs",
                "Cases/033_FactoryDelegate_SingleGenericParam/Program.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task FactoryDelegate_MultipleDecorators(DecoWeaverGenerator sut)
    {
        // Test multiple decorators with factory delegate
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/034_FactoryDelegate_MultipleDecorators/Repository.cs",
                "Cases/034_FactoryDelegate_MultipleDecorators/Program.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task FactoryDelegate_NoDecorators(DecoWeaverGenerator sut)
    {
        // Test factory delegate without decorators (pass-through)
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/035_FactoryDelegate_NoDecorators/Repository.cs",
                "Cases/035_FactoryDelegate_NoDecorators/Program.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task FactoryDelegate_Transient(DecoWeaverGenerator sut)
    {
        // Test AddTransient with factory delegate
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/036_FactoryDelegate_Transient/Repository.cs",
                "Cases/036_FactoryDelegate_Transient/Program.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task FactoryDelegate_Singleton(DecoWeaverGenerator sut)
    {
        // Test AddSingleton with factory delegate
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/037_FactoryDelegate_Singleton/Repository.cs",
                "Cases/037_FactoryDelegate_Singleton/Program.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task FactoryDelegate_ComplexDependencies(DecoWeaverGenerator sut)
    {
        // Test factory delegate with complex dependencies from IServiceProvider
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/038_FactoryDelegate_ComplexDependencies/Repository.cs",
                "Cases/038_FactoryDelegate_ComplexDependencies/Program.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task KeyedService_SingleDecorator(DecoWeaverGenerator sut)
    {
        // Test keyed service with single decorator
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/039_KeyedService_SingleDecorator/Repository.cs",
                "Cases/039_KeyedService_SingleDecorator/Program.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task KeyedService_MultipleKeys(DecoWeaverGenerator sut)
    {
        // Test multiple keyed services with same interface but different keys
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/040_KeyedService_MultipleKeys/Repository.cs",
                "Cases/040_KeyedService_MultipleKeys/Program.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task KeyedService_MultipleDecorators(DecoWeaverGenerator sut)
    {
        // Test keyed service with multiple decorators in ascending order
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/041_KeyedService_MultipleDecorators/Repository.cs",
                "Cases/041_KeyedService_MultipleDecorators/Program.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task KeyedService_IntegerKey(DecoWeaverGenerator sut)
    {
        // Test keyed service with integer key (non-string key type)
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/042_KeyedService_IntegerKey/Repository.cs",
                "Cases/042_KeyedService_IntegerKey/Program.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task KeyedService_FactoryDelegate(DecoWeaverGenerator sut)
    {
        // Test keyed service with factory delegate - decorator should still be applied
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/043_KeyedService_FactoryDelegate/Repository.cs",
                "Cases/043_KeyedService_FactoryDelegate/Program.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task KeyedService_NoDecorators(DecoWeaverGenerator sut)
    {
        // Test keyed service without decorators - should pass through
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/044_KeyedService_NoDecorators/Repository.cs",
                "Cases/044_KeyedService_NoDecorators/Program.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task KeyedService_Transient(DecoWeaverGenerator sut)
    {
        // Test keyed service with Transient lifetime
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/045_KeyedService_Transient/Repository.cs",
                "Cases/045_KeyedService_Transient/Program.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task InstanceRegistration_SingleTypeParam(DecoWeaverGenerator sut)
    {
        // Test instance registration with single type parameter
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/047_InstanceRegistration_SingleTypeParam/Repository.cs",
                "Cases/047_InstanceRegistration_SingleTypeParam/Program.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task InstanceRegistration_MultipleDecorators(DecoWeaverGenerator sut)
    {
        // Test instance registration with multiple ordered decorators
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/048_InstanceRegistration_MultipleDecorators/Repository.cs",
                "Cases/048_InstanceRegistration_MultipleDecorators/Program.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task InstanceRegistration_NoDecorators(DecoWeaverGenerator sut)
    {
        // Test instance registration without decorators - should pass through
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/049_InstanceRegistration_NoDecorators/Repository.cs",
                "Cases/049_InstanceRegistration_NoDecorators/Program.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public async Task KeyedInstanceRegistration_SingleDecorator(DecoWeaverGenerator sut)
    {
        // Test keyed instance registration with single decorator
        await VerifyGlue.VerifySourcesAsync(sut,
            [
                "Cases/051_KeyedInstanceRegistration_SingleDecorator/Repository.cs",
                "Cases/051_KeyedInstanceRegistration_SingleDecorator/Program.cs"
            ],
            featureFlags: FeatureFlags);
    }

    [Theory]
    [GeneratorAutoData]
    public void GeneratedCode_NeverUsesReflectionBasedDecoratorConstruction(DecoWeaverGenerator sut)
    {
        // Regression test for issue #61 (Native AOT/trim warnings IL2055/IL3050/IL2072): decorator
        // construction must be a direct, literal `typeof(ClosedDecoratorType)` passed straight to
        // ActivatorUtilities.CreateInstance, never a runtime-closed Type via MakeGenericType /
        // the old DecoratorFactory.CloseIfNeeded helper. Exercises the open-generic-decorator-over
        // -closed-registration shape, the exact scenario reported in the issue.
        var (driver, _, _) = GeneratorTestHelpers.RunFromCases(sut,
            [
                "Cases/002_OpenGeneric_MultipleOrdered/Repository.cs",
                "Cases/002_OpenGeneric_MultipleOrdered/Program.cs"
            ],
            featureFlags: FeatureFlags);

        var generated = driver.GetRunResult().GeneratedTrees
            .Single(t => t.FilePath.EndsWith("ClosedGenerics.g.cs"))
            .GetText().ToString();

        generated.Should().NotContain("MakeGenericType");
        generated.Should().NotContain("DecoratorFactory");
        generated.Should().NotContain("CloseIfNeeded");
        generated.Should().Contain("ActivatorUtilities.CreateInstance");
    }

}