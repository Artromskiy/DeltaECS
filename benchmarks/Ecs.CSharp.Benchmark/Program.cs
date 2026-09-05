#pragma warning disable CA1852 // Seal internal types

using System;
using System.Collections.Generic;
using System.Globalization;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Running;
using Ecs.CSharp.Benchmark;

internal static class Program
{
    private const string AmountOption = "--amount";
    private const string AmountsOption = "--amounts";
    private const string EntityCountOption = "--entity-count";
    private const string PaddingOption = "--padding";
    private const string EntityPaddingOption = "--entity-padding";
    private const string EntityCountEnvironmentVariable = "ECS_CSHARP_BENCH_ENTITY_COUNT";
    private const string EntityPaddingEnvironmentVariable = "ECS_CSHARP_BENCH_ENTITY_PADDING";

    private static readonly Type[] BenchmarkTypes =
    [
        typeof(CreateEntityWithOneComponent),
        typeof(CreateEntityWithTwoComponents),
        typeof(CreateEntityWithThreeComponents),
        typeof(SystemWithOneComponent),
        typeof(SystemWithTwoComponents),
        typeof(SystemWithThreeComponents),
        typeof(SystemWithTwoComponentsMultipleComposition)
    ];

    internal static void Main(string[] args)
    {
        CultureInfo cultureInfo = new("en-US");
        CultureInfo.CurrentCulture = cultureInfo;
        CultureInfo.CurrentUICulture = cultureInfo;
        CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
        CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

        if (args.Length > 0 && string.Equals(args[0], "contract-smoke", StringComparison.OrdinalIgnoreCase))
        {
            DeltaEcsSmoke.Run();
            return;
        }

        List<int> amounts = ParseAmounts(args, out string[] benchmarkArgs);
        int padding = ParseSingleValue(args, PaddingOption, BenchmarkConfiguration.EntityPadding);
        BenchmarkConfiguration.EntityPadding = padding;
        Environment.SetEnvironmentVariable(EntityPaddingEnvironmentVariable, padding.ToString(CultureInfo.InvariantCulture));

        IConfig configuration = DefaultConfig.Instance
            .WithOptions(ConfigOptions.DisableOptimizationsValidator)
            .WithOrderer(new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest));

        foreach (int amount in amounts)
        {
            BenchmarkConfiguration.EntityCount = amount;
            Environment.SetEnvironmentVariable(EntityCountEnvironmentVariable, amount.ToString(CultureInfo.InvariantCulture));
            BenchmarkSwitcher benchmark = BenchmarkSwitcher.FromTypes(BenchmarkTypes);
            if (benchmarkArgs.Length == 0)
            {
                benchmark.RunAll(configuration);
            }
            else
            {
                benchmark.Run(benchmarkArgs, configuration);
            }
        }
    }

    private static List<int> ParseAmounts(string[] args, out string[] benchmarkArgs)
    {
        List<int> amounts = [];
        List<string> remaining = [];

        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            if (string.Equals(argument, AmountOption, StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, EntityCountOption, StringComparison.OrdinalIgnoreCase))
            {
                amounts.Add(ParseValue(args, ref index, argument));
            }
            else if (string.Equals(argument, AmountsOption, StringComparison.OrdinalIgnoreCase))
            {
                string value = NextValue(args, ref index, argument);
                foreach (string item in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!int.TryParse(item, out int amount) || amount <= 0)
                    {
                        throw new ArgumentException($"Invalid benchmark amount '{item}'.", nameof(args));
                    }

                    amounts.Add(amount);
                }
            }
            else if (string.Equals(argument, PaddingOption, StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, EntityPaddingOption, StringComparison.OrdinalIgnoreCase))
            {
                index++;
            }
            else
            {
                remaining.Add(argument);
            }
        }

        if (amounts.Count == 0)
        {
            amounts.Add(BenchmarkConfiguration.EntityCount);
        }

        benchmarkArgs = [.. remaining];
        return amounts;
    }

    private static int ParseSingleValue(string[] args, string option, int fallback)
    {
        for (int index = 0; index < args.Length; index++)
        {
            if (string.Equals(args[index], option, StringComparison.OrdinalIgnoreCase)
                || (option == PaddingOption && string.Equals(args[index], EntityPaddingOption, StringComparison.OrdinalIgnoreCase)))
            {
                string value = NextValue(args, ref index, args[index]);
                if (!int.TryParse(value, out int result) || result < 0)
                {
                    throw new ArgumentException($"Invalid value '{value}' for {option}.", nameof(args));
                }

                return result;
            }
        }

        return fallback;
    }

    private static int ParseValue(string[] args, ref int index, string option)
    {
        string value = NextValue(args, ref index, option);
        if (!int.TryParse(value, out int result) || result <= 0)
        {
            throw new ArgumentException($"Invalid value '{value}' for {option}.", nameof(args));
        }

        return result;
    }

    private static string NextValue(string[] args, ref int index, string option)
    {
        if (++index >= args.Length)
        {
            throw new ArgumentException($"Missing value for {option}.", nameof(args));
        }

        return args[index];
    }
}

namespace Ecs.CSharp.Benchmark
{
    internal static class BenchmarkConfiguration
    {
        internal static int EntityCount { get; set; } = ReadEnvironmentValue("ECS_CSHARP_BENCH_ENTITY_COUNT", 100_000);

        internal static int EntityPadding { get; set; } = ReadEnvironmentValue("ECS_CSHARP_BENCH_ENTITY_PADDING", 0);

        private static int ReadEnvironmentValue(string name, int fallback)
            => int.TryParse(Environment.GetEnvironmentVariable(name), out int value) && value > 0
                ? value
                : fallback;
    }
}
