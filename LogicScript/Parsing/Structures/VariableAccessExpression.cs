﻿namespace LogicScript.Parsing.Structures
{
    internal class VariableAccessExpression : Expression
    {
        public override bool IsSingleBit => false;
        public override bool IsReadable => true;
        public override bool IsWriteable => true;

        public string Name { get; set; }

        public VariableAccessExpression(string name, SourceLocation location) : base(location)
        {
            this.Name = name;
        }
    }
}
