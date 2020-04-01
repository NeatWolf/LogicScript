﻿using LogicScript.Data;
using LogicScript.Parsing.Structures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogicScript
{
    public sealed class LogicRunner
    {
        private readonly Script Script;

        public LogicRunner(Script script)
        {
            this.Script = script ?? throw new ArgumentNullException(nameof(script));
        }

        public void DoUpdate(IMachine machine)
        {
            foreach (var item in Script.Cases)
            {
                UpdateCase(machine, item);
            }
        }

        private static void UpdateCase(IMachine machine, Case c)
        {
            if (c.Statements == null)
                return;

            var value = GetBitsValue(machine, c.InputsValue);
            bool match = false;

            if (c.InputSpec is CompoundInputSpec compound)
            {
                if (value.Length != compound.Indices.Length)
                    throw new LogicEngineException("Mismatched input count", c);

                match = AreInputsMatched(machine, compound.Indices, value);
            }
            else if (c.InputSpec is WholeInputSpec)
            {
                if (value.Length != machine.InputCount)
                    throw new LogicEngineException("Mismatched input count", c);

                match = AreInputsMatched(machine, value);
            }

            if (match)
                RunStatements(machine, c.Statements);
        }

        private static bool AreInputsMatched(IMachine machine, int[] inputIndices, BitsValue bits)
        {
            bool match = true;

            for (int i = 0; i < inputIndices.Length; i++)
            {
                bool inputValue = machine.GetInput(i);
                bool requiredValue = bits[i];

                if (inputValue != requiredValue)
                {
                    match = false;
                    break;
                }
            }

            return match;
        }
        
        private static bool AreInputsMatched(IMachine machine, BitsValue bits)
        {
            bool match = true;

            for (int i = 0; i < machine.InputCount; i++)
            {
                bool inputValue = machine.GetInput(i);
                bool requiredValue = bits[i];

                if (inputValue != requiredValue)
                {
                    match = false;
                    break;
                }
            }

            return match;
        }

        private static void RunStatements(IMachine machine, IEnumerable<Statement> statements)
        {
            foreach (var stmt in statements)
            {
                RunStatement(machine, stmt);
            }
        }

        private static void RunStatement(IMachine machine, Statement stmt)
        {
            if (stmt is SetSingleOutputStatement setsingle)
            {
                machine.SetOutput(setsingle.Output, GetBitValue(machine, setsingle.Value));
            }
            else if (stmt is SetOutputStatement setout)
            {
                var value = GetBitsValue(machine, setout.Value);

                if (value.Length != machine.OutputCount)
                    throw new LogicEngineException("Mismatched output count", stmt);

                machine.SetOutputs(value);
            }
        }

        private static bool GetBitValue(IMachine machine, Expression expr)
        {
            switch (expr)
            {
                case NumberLiteralExpression num when num.Length == 1:
                    return num.Value == 1;
                case InputExpression input:
                    return machine.GetInput(input.InputIndex);

                default:
                    throw new LogicEngineException("Expected single-bit value", expr);
            }
        }

        private static BitsValue GetBitsValue(IMachine machine, Expression expr)
        {
            switch (expr)
            {
                case NumberLiteralExpression num:
                    return new BitsValue(num.Value, num.Length);
                case ListExpression list:
                    return GetListValue(list);

                default:
                    throw new LogicEngineException("Expected multi-bit value", expr);
            }

            BitsValue GetListValue(ListExpression list)
            {
                var items = new bool[list.Expressions.Length];

                for (int i = 0; i < list.Expressions.Length; i++)
                {
                    items[i] = GetBitValue(machine, list.Expressions[i]);
                }

                return new BitsValue(items);
            }
        }
    }
}