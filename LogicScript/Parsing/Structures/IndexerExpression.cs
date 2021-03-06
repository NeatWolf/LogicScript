﻿using System;

namespace LogicScript.Parsing.Structures
{
    internal class IndexerExpression : Expression
    {
        public override bool IsSingleBit => true;
        public override ExpressionType Type => ExpressionType.Indexer;

        public override bool IsReadable => Operand.IsReadable;
        public override bool IsWriteable => Operand.IsWriteable;

        public Expression Operand { get; set; }
        public Index Index { get; set; }

        public IndexerExpression(Expression operand, Index index, SourceLocation location) : base(location)
        {
            this.Operand = operand ?? throw new ArgumentNullException(nameof(operand));
            this.Index = index;
        }

        public override string ToString() => $"{Operand}[{Index}]";
    }
}
