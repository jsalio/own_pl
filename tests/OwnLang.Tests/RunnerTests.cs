using System;
using System.IO;
using NUnit.Framework;
using Own_Lang;

namespace OwnLang.Tests;

/// <summary>
/// Tests for the command-line runner: reading and running <c>.own</c> files,
/// argument/extension/existence validation, and clean error reporting.
/// </summary>
[TestFixture]
public class RunnerTests
{
    // Runs args through Runner.RunFile, capturing program output (Term.out writes
    // to Console) and error output separately. Returns (exitCode, output, error).
    private static (int code, string output, string error) RunFile(params string[] args)
    {
        TextWriter originalOut = Console.Out;
        var outBuffer = new StringWriter();
        var errBuffer = new StringWriter();
        Console.SetOut(outBuffer);
        try
        {
            int code = Runner.RunFile(args, outBuffer, errBuffer);
            return (code,
                outBuffer.ToString().Replace("\r\n", "\n").Trim(),
                errBuffer.ToString().Replace("\r\n", "\n").Trim());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    // Writes source to a uniquely-named temp file with the given extension,
    // runs it, and deletes the file afterwards.
    private static (int code, string output, string error) RunSourceFile(
        string source, string extension = ".own")
    {
        string path = Path.Combine(
            Path.GetTempPath(), $"ownlang_test_{Guid.NewGuid():N}{extension}");
        File.WriteAllText(path, source);
        try
        {
            return RunFile(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void RunsAValidFileAndPrintsItsOutput()
    {
        var (code, output, error) = RunSourceFile(
            @"def program { function empty Main() { Term.out(""hi""); } }");

        Assert.That(code, Is.EqualTo(0));
        Assert.That(output, Is.EqualTo("hi"));
        Assert.That(error, Is.Empty);
    }

    [Test]
    public void NoArgumentsPrintsUsageAndFails()
    {
        var (code, _, error) = RunFile();

        Assert.That(code, Is.EqualTo(1));
        Assert.That(error, Does.Contain("usage"));
    }

    [Test]
    public void NonOwnExtensionIsRejected()
    {
        var (code, _, error) = RunFile("script.txt");

        Assert.That(code, Is.EqualTo(1));
        Assert.That(error, Does.Contain(".own"));
    }

    [Test]
    public void MissingFileIsReported()
    {
        var (code, _, error) = RunFile("does_not_exist.own");

        Assert.That(code, Is.EqualTo(1));
        Assert.That(error, Does.Contain("file not found"));
    }

    [Test]
    public void SyntaxErrorInFileIsReportedCleanly()
    {
        // Missing ';' -> the parser throws; the runner must turn that into a
        // one-line "error: ..." message and a non-zero exit code, not a crash.
        var (code, _, error) = RunSourceFile(
            @"def program { function empty Main() { Term.out(""hi"") } }");

        Assert.That(code, Is.EqualTo(1));
        Assert.That(error, Does.StartWith("error:"));
    }

    [Test]
    public void RunExecutesSourceDirectlyWithoutAFile()
    {
        var outBuffer = new StringWriter();
        var errBuffer = new StringWriter();
        TextWriter originalOut = Console.Out;
        Console.SetOut(outBuffer);
        try
        {
            int code = Runner.Run(
                @"def program { function empty Main() { Term.out(21 + 21); } }",
                outBuffer, errBuffer);

            Assert.That(code, Is.EqualTo(0));
            Assert.That(outBuffer.ToString().Trim(), Is.EqualTo("42"));
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
