﻿using System;

namespace Inu.Cate.MuCom87
{
    class WordBinomialInstruction : Cate.BinomialInstruction
    {
        public WordBinomialInstruction(Function function, int operatorId, AssignableOperand destinationOperand, Operand leftOperand, Operand rightOperand) : base(function, operatorId, destinationOperand, leftOperand, rightOperand) { }

        public override void BuildAssembly()
        {
            string lowOperation, highOperation;
            switch (OperatorId) {
                case '|':
                    lowOperation = highOperation = "ora|ori";
                    break;
                case '^':
                    lowOperation = highOperation = "xra|xri";
                    break;
                case '&':
                    lowOperation = highOperation = "ana|ani";
                    break;
                case '+':
                    lowOperation = "add|adi";
                    highOperation = "adc|aci";
                    break;
                case '-':
                    lowOperation = "sub|sui";
                    highOperation = "sbb|sbi";
                    break;
                default:
                    throw new NotImplementedException();
            }

            using (ByteOperation.ReserveRegister(this, ByteRegister.A)) {
                ByteRegister.A.Load(this, Compiler.LowByteOperand(LeftOperand));
                ByteRegister.A.Operate(this, lowOperation, true, Compiler.LowByteOperand(RightOperand));
                ByteRegister.A.Store(this, Compiler.LowByteOperand(DestinationOperand));
                ByteRegister.A.Load(this, Compiler.HighByteOperand(LeftOperand));
                ByteRegister.A.Operate(this, highOperation, true, Compiler.HighByteOperand(RightOperand));
                ByteRegister.A.Store(this, Compiler.HighByteOperand(DestinationOperand));
            }
        }
    }
}
