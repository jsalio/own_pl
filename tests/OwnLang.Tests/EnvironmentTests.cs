using NUnit.Framework;
using Environment = Own_Lang.Internal.Environment;

namespace OwnLang.Tests;

/// <summary>
/// Regression tests for the runtime variable store.
/// (Aliased because the type name collides with <c>System.Environment</c>.)
/// </summary>
[TestFixture]
public class EnvironmentTests
{
    [Test]
    public void DefineThenGetReturnsValue()
    {
        var env = new Environment();
        env.Define("x", 42);

        Assert.That(env.Get("x"), Is.EqualTo(42));
    }

    [Test]
    public void DefineOverwritesExistingValue()
    {
        var env = new Environment();
        env.Define("x", 1);
        env.Define("x", 2);

        Assert.That(env.Get("x"), Is.EqualTo(2));
    }

    [Test]
    public void GetUndefinedVariableThrows()
    {
        var env = new Environment();

        Assert.That(() => env.Get("noExiste"), Throws.Exception);
    }
}
