using MyIPTV.Core;
using MyIPTV.Infrastructure;

namespace MyIPTV.Tests;

[TestClass]
public sealed class SolutionStructureTests
{
    [TestMethod]
    public void FoundationAssembliesAreLoadable()
    {
        Assert.AreEqual("MyIPTV.Core", typeof(CoreAssemblyMarker).Assembly.GetName().Name);
        Assert.AreEqual("MyIPTV.Infrastructure", typeof(InfrastructureAssemblyMarker).Assembly.GetName().Name);
    }
}
