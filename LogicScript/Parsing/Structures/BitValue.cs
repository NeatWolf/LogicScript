﻿namespace LogicScript.Parsing.Structures
{
    internal abstract class BitValue
    {
    }

    internal class LiteralBitValue : BitValue
    {
        public bool Value { get; }

        public LiteralBitValue(bool value)
        {
            this.Value = value;
        }

        public override string ToString() => Value ? "1" : "0";
    }

    internal class InputBitValue : BitValue
    {
        public int InputIndex { get; }

        public InputBitValue(int inputIndex)
        {
            this.InputIndex = inputIndex;
        }

        public override string ToString() => $"in[{InputIndex}]";
    }
}