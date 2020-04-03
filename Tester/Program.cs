﻿using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Running;
using LogicScript;
using LogicScript.Data;
using LogicScript.Parsing;
using LogicScript.Parsing.Structures;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace Tester
{
    class Program
    {
        static void Main(string[] args)
        {
#if RELEASE
            BenchmarkRunner.Run<Benchmarks>(ManualConfig.Create(DefaultConfig.Instance).With(MemoryDiagnoser.Default));
#else

            const string script = @"
when 0 = 0
    out[2] = 1' + 2'
    out[2] = and(1, in[2])

    out = 1010
    out = add(10', 3')
end
";

            var result = Script.Compile(script);

            if (!result.Success)
            {
                foreach (var item in result.Errors)
                {
                    Console.WriteLine(item);
                }
                Console.ReadKey(true);
                return;
            }

            var engine = new LogicRunner(result.Script);
            var machine = new Machine();
            engine.DoUpdate(machine);

            Console.ReadKey(true);
#endif
        }
    }

    public class Machine : IMachine
    {
        public int InputCount => Inputs.Length;
        public int OutputCount => Outputs.Length;

        private bool[] Inputs = new[] { true, false, true, false };
        private bool[] Outputs = new[] { true, false, true, false };

        public bool GetInput(int i)
        {
            var v = Inputs[i];

            Console.WriteLine($"Read input {i}: {v}");
            return v;
        }

        public BitsValue GetInputs()
        {
            Console.WriteLine("Read inputs");

            return new BitsValue(Inputs);
        }

        public void SetOutput(int i, bool on)
        {
            Outputs[i] = on;

            Console.WriteLine($"Set output {i} to {on}");
        }

        public void SetOutputs(BitsValue values)
        {
            Array.Copy(values.Bits, Outputs, OutputCount);

            Console.WriteLine($"Set outputs to {values.Number} ({values})");
        }
    }
}
