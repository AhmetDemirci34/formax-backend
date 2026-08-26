using System.Globalization;

namespace Formax.TeamStrength.Tests;

/// <summary>
/// Dependency-free test runner. No NuGet package is referenced anywhere in this project, so the
/// tool builds and tests offline. A failing assertion throws; the runner reports and sets the exit
/// code. Nothing is reported as PASS unless the assertion actually ran and held.
/// </summary>
public sealed class TestRunner
{
    private readonly List<(string name, Action body)> _tests = new();
    public int Passed { get; private set; }
    public int Failed { get; private set; }

    public void Add(string name, Action body) => _tests.Add((name, body));

    public int Run()
    {
        Console.WriteLine("== TEAM STRENGTH TESTS ==");
        foreach (var (name, body) in _tests)
        {
            try
            {
                body();
                Passed++;
                Console.WriteLine($"  PASS  {name}");
            }
            catch (Exception ex)
            {
                Failed++;
                Console.WriteLine($"  FAIL  {name}");
                Console.WriteLine($"        {ex.Message}");
            }
        }
        Console.WriteLine($"-- {Passed} passed, {Failed} failed, {_tests.Count} total");
        return Failed == 0 ? 0 : 1;
    }

    public static void True(bool condition, string message)
    {
        if (!condition) throw new Exception("expected true: " + message);
    }

    public static void Equal(double expected, double actual, double tolerance, string message)
    {
        if (Math.Abs(expected - actual) > tolerance)
            throw new Exception($"{message}: expected {expected.ToString("0.########", CultureInfo.InvariantCulture)} " +
                                $"but got {actual.ToString("0.########", CultureInfo.InvariantCulture)} " +
                                $"(tolerance {tolerance.ToString(CultureInfo.InvariantCulture)})");
    }

    public static void Equal(object? expected, object? actual, string message)
    {
        if (!Equals(expected, actual))
            throw new Exception($"{message}: expected '{expected}' but got '{actual}'");
    }

    public static void Greater(double bigger, double smaller, string message)
    {
        if (!(bigger > smaller))
            throw new Exception($"{message}: expected {bigger.ToString("0.######", CultureInfo.InvariantCulture)} > " +
                                $"{smaller.ToString("0.######", CultureInfo.InvariantCulture)}");
    }
}
