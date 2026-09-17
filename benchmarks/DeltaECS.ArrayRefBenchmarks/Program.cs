using System.Globalization;
using BenchmarkDotNet.Running;

namespace Delta.ECS.ArrayRefBenchmarks;

internal static class Program
{
    private const int DefaultAmount = 200_000;

    internal static int Amount { get; private set; } = DefaultAmount;

    private static void Main(string[] args)
    {
        string[] benchmarkArgs = ExtractAmount(args, out int? amount);
        Amount = amount ?? DefaultAmount;
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(benchmarkArgs);
    }

    private static string[] ExtractAmount(string[] args, out int? amount)
    {
        var benchmarkArgs = new List<string>(args.Length);
        amount = null;

        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            if (string.Equals(argument, "--amount", StringComparison.OrdinalIgnoreCase))
            {
                if (++index >= args.Length)
                {
                    throw new ArgumentException("Missing value for --amount.", nameof(args));
                }

                amount = ParseAmount(args[index]);
                continue;
            }

            const string prefix = "--amount=";
            if (argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                amount = ParseAmount(argument[prefix.Length..]);
                continue;
            }

            benchmarkArgs.Add(argument);
        }

        return benchmarkArgs.ToArray();
    }

    private static int ParseAmount(string value)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int amount)
            || amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Amount must be a positive integer.");
        }

        return amount;
    }
}
