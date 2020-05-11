﻿using LogicScript.Data;
using LogicScript.Parsing;
using LogicScript.Parsing.Structures;
using System;
using System.Collections.Generic;

namespace LogicScript
{
    public ref struct LogicRunner
    {
        internal ref struct CaseContext
        {
            public readonly IMachine Machine;
            public readonly IDictionary<string, BitsValue> Variables;

            public CaseContext(IMachine machine)
            {
                this.Machine = machine;
                this.Variables = new Dictionary<string, BitsValue>();
            }

            public void Set(string name, BitsValue val) => Variables[name] = val;

            public void Unset(string name) => Variables.Remove(name);

            public BitsValue Get(string name, ICodeNode node)
            {
                if (!Variables.TryGetValue(name, out var val))
                    throw new LogicEngineException($"variable \"{name}\" not defined", node);

                return val;
            }
        }

        public void RunScript(Script script, IMachine machine, bool isFirstUpdate = false)
        {
            int len = script.TopLevelNodes.Count;
            for (int i = 0; i < len; i++)
            {
                var node = script.TopLevelNodes[i];

                if (node is Case @case)
                    UpdateCase(machine, @case, isFirstUpdate);
            }
        }

        private void UpdateCase(IMachine machine, Case c, bool firstUpdate)
        {
            if (c.Statements == null)
                return;

            var ctx = new CaseContext(machine);
            bool run = false;

            switch (c)
            {
                case ConditionalCase cond:
                    run = IsTruthy(GetValue(ctx, cond.Condition));
                    break;
                case UnconditionalCase _:
                    run = true;
                    break;
                case OnceCase _:
                    run = firstUpdate;
                    break;
            }

            if (run)
                RunStatements(ctx, c.Statements);
        }

        private void RunStatements(CaseContext ctx, IReadOnlyList<Statement> statements)
        {
            for (int i = 0; i < statements.Count; i++)
            {
                RunStatement(ctx, statements[i]);
            }
        }

        private void RunStatement(CaseContext ctx, Statement stmt)
        {
            switch (stmt)
            {
                case AssignStatement assign:
                    RunStatement(ctx, assign);
                    break;
                case IfStatement @if:
                    RunStatement(ctx, @if);
                    break;
                case QueueUpdateStatement queueStmt:
                    RunStatement(ctx, queueStmt);
                    break;
                case ForStatement forStatement:
                    RunStatement(ctx, forStatement);
                    break;
            }
        }

        private bool IsTruthy(BitsValue value) => value.Number > 0;

        private void RunStatement(CaseContext ctx, AssignStatement stmt)
        {
            if (!stmt.LeftSide.IsWriteable)
                throw new LogicEngineException("Expected a writeable expression", stmt);

            if (!stmt.RightSide.IsReadable)
                throw new LogicEngineException("Expected a readable expression", stmt);

            var lhs = stmt.LeftSide;

            var value = GetValue(ctx, stmt.RightSide);
            BitRange range;

            if (lhs is IndexerExpression indexer)
            {
                range = indexer.Range;
                lhs = indexer.Operand;
            }
            else
            {
                range = new BitRange(0, value.Length);
            }

            if (!range.HasEnd)
                range = new BitRange(range.Start, range.Start + value.Length);

            if (value.Length != range.Length)
                throw new LogicEngineException("Range and value length mismatch", stmt);

            Span<bool> bits = stackalloc bool[value.Length];
            value.FillBits(bits);

            if (lhs is SlotExpression slot)
            {
                switch (slot.Slot)
                {
                    case Slots.Out:
                        if (range.Start + range.Length > ctx.Machine.OutputCount)
                            throw new LogicEngineException("Range out of bounds for outputs", stmt);

                        ctx.Machine.SetOutputs(range, bits);
                        break;

                    case Slots.Memory:
                        if (range.Start + range.Length > ctx.Machine.Memory.Capacity)
                            throw new LogicEngineException("Range out of bounds for memory", stmt);

                        ctx.Machine.Memory.Write(range, bits);
                        break;

                    default:
                        throw new LogicEngineException("Invalid slot on expression", stmt);
                }
            }
        }

        private void RunStatement(CaseContext ctx, IfStatement stmt)
        {
            var conditionValue = GetValue(ctx, stmt.Condition);

            if (IsTruthy(conditionValue))
            {
                RunStatements(ctx, stmt.Body);
            }
            else if (stmt.Else != null)
            {
                RunStatements(ctx, stmt.Else);
            }
        }

        private void RunStatement(CaseContext ctx, ForStatement stmt)
        {
            var from = GetValue(ctx, stmt.From);
            var to = GetValue(ctx, stmt.To);

            for (ulong i = from.Number; i < to.Number; i++)
            {
                ctx.Set(stmt.VarName, new BitsValue(i, to.Length));
                RunStatements(ctx, stmt.Body);
            }
        }

        private void RunStatement(CaseContext ctx, QueueUpdateStatement stmt)
        {
            if (!(ctx.Machine is IUpdatableMachine updatableMachine))
                throw new LogicEngineException("Update queueing is not supported by the machine", stmt);

            updatableMachine.QueueUpdate();
        }

        internal BitsValue GetValue(CaseContext ctx, Expression expr)
        {
            switch (expr)
            {
                case NumberLiteralExpression num:
                    return new BitsValue(num.Value, num.Length);

                case ListExpression list:
                    return DoListExpression(ctx, list);

                case OperatorExpression op:
                    return DoOperator(ctx, op);

                case UnaryOperatorExpression unary:
                    return DoUnaryOperator(ctx, unary);

                case IndexerExpression indexer when indexer.Operand is SlotExpression slot:
                    return DoSlotExpression(ctx, slot, indexer.Range);

                case IndexerExpression indexer:
                    return DoIndexerExpression(ctx, indexer);

                case SlotExpression slot:
                    return DoSlotExpression(ctx, slot, null);

                case FunctionCallExpression funcCall:
                    return DoFunctionCall(ctx, funcCall);

                case VariableAccessExpression varAccess:
                    return ctx.Get(varAccess.Name, varAccess);

                default:
                    throw new LogicEngineException("Expected multi-bit value", expr);
            }
        }

        private BitsValue DoIndexerExpression(CaseContext ctx, IndexerExpression indexer)
        {
            var value = GetValue(ctx, indexer.Operand);

            int end = indexer.Range.Length;
            if (!indexer.Range.HasEnd)
                end = value.Length;

            Span<bool> bits = stackalloc bool[end - indexer.Range.Start];
            value.FillBits(bits, indexer.Range.Start, end);

            return new BitsValue(bits);
        }

        private BitsValue DoFunctionCall(CaseContext ctx, FunctionCallExpression funcCall)
        {
            Span<BitsValue> values = stackalloc BitsValue[funcCall.Arguments.Count];

            for (int i = 0; i < funcCall.Arguments.Count; i++)
            {
                values[i] = GetValue(ctx, funcCall.Arguments[i]);
            }

            switch (funcCall.Name)
            {
                case "and":
                    if (values.Length != 1)
                        throw new LogicEngineException("Expected 1 argument on call to 'add'", funcCall);
                    return values[0].AreAllBitsSet;

                case "or":
                    if (values.Length != 1)
                        throw new LogicEngineException("Expected 1 argument on call to 'or'", funcCall);
                    return values[0].IsAnyBitSet;

                case "sum":
                    if (values.Length != 1)
                        throw new LogicEngineException("Expected 1 argument on call to 'sum'", funcCall);
                    return values[0].PopulationCount;

                case "trunc" when values.Length == 2:
                    return new BitsValue(values[0], (int)values[1].Number);

                case "trunc" when values.Length == 1:
                    return values[0].Truncated;

                case "trunc":
                    throw new LogicEngineException("Expected 1 or 2 arguments on call to 'trunc'", funcCall);

                default:
                    throw new LogicEngineException($"Unknown function '{funcCall.Name}'", funcCall);
            }
        }

        private BitsValue DoSlotExpression(CaseContext ctx, SlotExpression expr, BitRange? r)
        {
            var range = r ?? new BitRange(0, ctx.Machine.InputCount);
            if (!range.HasEnd)
                range = new BitRange(range.Start, ctx.Machine.InputCount);

            Span<bool> values = stackalloc bool[range.Length];

            switch (expr.Slot)
            {
                case Slots.In:
                    ctx.Machine.GetInputs(range, values);
                    break;
                case Slots.Memory:
                    ctx.Machine.Memory.Read(range, values);
                    break;
                default:
                    throw new LogicEngineException("Invalid slot on expression", expr);
            }

            return new BitsValue(values);
        }

        private BitsValue DoListExpression(CaseContext ctx, ListExpression list)
        {
            ulong n = 0;

            int len = list.Expressions.Length;
            for (int i = 0; i < len; i++)
            {
                var value = GetValue(ctx, list.Expressions[i]);

                if (!value.IsSingleBit)
                    throw new LogicEngineException("List expressions can only contain single-bit values", list.Expressions[i]);

                if (value.IsOne)
                    n |= 1UL << (len - 1 - i);
            }

            return new BitsValue(n, len);
        }

        private BitsValue DoUnaryOperator(CaseContext ctx, UnaryOperatorExpression op)
        {
            var value = GetValue(ctx, op.Operand);

            switch (op.Operator)
            {
                case Operator.Not:
                    return value.Negated;
            }

            throw new LogicEngineException();
        }

        private BitsValue DoOperator(CaseContext ctx, OperatorExpression op)
        {
            var left = GetValue(ctx, op.Left);
            var right = GetValue(ctx, op.Right);

            switch (op.Operator)
            {
                case Operator.Add:
                    return left + right;
                case Operator.Subtract:
                    return left - right;
                case Operator.Multiply:
                    return left * right;
                case Operator.Divide:
                    return left / right;
                case Operator.Modulo:
                    return left.Number % right.Number;

                case Operator.Equals:
                    return left == right;
                case Operator.Greater:
                    return left > right;
                case Operator.GreaterOrEqual:
                    return left >= right;
                case Operator.Lesser:
                    return left < right;
                case Operator.LesserOrEqual:
                    return left <= right;

                case Operator.And:
                    return new BitsValue(left & right, Math.Min(left.Length, right.Length));
                case Operator.Or:
                    return new BitsValue(left | right, Math.Min(left.Length, right.Length));
            }

            throw new LogicEngineException();
        }
    }
}
