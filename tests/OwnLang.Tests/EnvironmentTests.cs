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

    [Test]
    public void GetFallsThroughToEnclosingScope()
    {
        var outer = new Environment();
        outer.Define("x", 1);
        var inner = new Environment(outer);

        Assert.That(inner.Get("x"), Is.EqualTo(1));
    }

    [Test]
    public void DefineInChildShadowsButDoesNotTouchParent()
    {
        var outer = new Environment();
        outer.Define("x", 1);
        var inner = new Environment(outer);
        inner.Define("x", 2); // binding local, no toca al padre

        Assert.That(inner.Get("x"), Is.EqualTo(2));
        Assert.That(outer.Get("x"), Is.EqualTo(1));
    }

    [Test]
    public void AssignMutatesBindingInEnclosingScope()
    {
        var outer = new Environment();
        outer.Define("x", 1);
        var inner = new Environment(outer);
        inner.Assign("x", 9); // sube la cadena y muta donde vive

        Assert.That(outer.Get("x"), Is.EqualTo(9));
    }

    [Test]
    public void AssignUndefinedVariableThrows()
    {
        var env = new Environment();

        Assert.That(() => env.Assign("noExiste", 5), Throws.Exception);
    }
}
