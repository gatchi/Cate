﻿using System;

namespace Inu.Cate.Mos6502
{
    class ByteShiftInstruction : Cate.ByteShiftInstruction
    {
        public ByteShiftInstruction(Function function, int operatorId, AssignableOperand destinationOperand, Operand leftOperand, Operand rightOperand) : base(function, operatorId, destinationOperand, leftOperand, rightOperand)
        { }

        protected override string Operation()
        {
            return OperatorId switch
            {
                Keyword.ShiftLeft => "asl",
                Keyword.ShiftRight when !((IntegerType)LeftOperand.Type).Signed => "lsr",
                _ => throw new NotImplementedException()
            };
        }


        protected override void ShiftConstant(int count)
        {
            if (OperatorId == Keyword.ShiftRight && ((IntegerType)LeftOperand.Type).Signed) {
                ByteOperation.UsingRegister(this, ByteRegister.Y,  () =>
                {
                    ByteRegister.Y.LoadConstant(this, count);
                    CallExternal("cate.ShiftRightSignedA");
                });
                return;
            }
            base.ShiftConstant(count);
        }

        protected override void ShiftVariable(Operand counterOperand)
        {
            string functionName = OperatorId switch
            {
                Keyword.ShiftLeft => "cate.ShiftLeftA",
                Keyword.ShiftRight => ((IntegerType)LeftOperand.Type).Signed
                    ? "cate.ShiftRightSignedA"
                    : "cate.ShiftRightA",
                _ => throw new NotImplementedException()
            };
            ByteOperation.UsingRegister(this, ByteRegister.Y,  () =>
            {
                ByteRegister.Y.Load(this, RightOperand);
                CallExternal(functionName);
            });
        }

        private void CallExternal(string functionName)
        {
            ByteOperation.UsingRegister(this, ByteRegister.A, () =>
            {
                ByteRegister.A.Load(this, LeftOperand);
                Compiler.CallExternal(this, functionName);
                RemoveVariableRegister(ByteRegister.A);
                ChangedRegisters.Add(ByteRegister.A);
                RemoveVariableRegister(ByteRegister.Y);
                ChangedRegisters.Add(ByteRegister.Y);
                ByteRegister.A.Store(this, DestinationOperand);
            });
        }
    }
}