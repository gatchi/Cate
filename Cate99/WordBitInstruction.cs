﻿using System;
using System.Linq;

namespace Inu.Cate.Tms99
{
    internal class WordBitInstruction : BinomialInstruction
    {
        public WordBitInstruction(Function function, int operatorId, AssignableOperand destinationOperand, Operand leftOperand, Operand rightOperand) : base(function, operatorId, destinationOperand, leftOperand, rightOperand) { }

        public override void BuildAssembly()
        {
            switch (OperatorId) {
                case '|' when RightOperand is IntegerOperand integerOperand:
                    Tms99.WordOperation.OperateConstant(this, "ori", DestinationOperand, LeftOperand, integerOperand.IntegerValue);
                    return;
                case '|':
                    Tms99.WordOperation.Operate(this, "soc", DestinationOperand, LeftOperand, RightOperand);
                    return;
                case '&':
                    using (var destination = ByteOperation.ReserveAnyRegisterToChange(this, DestinationOperand, LeftOperand)) {
                        var destinationRegister = destination.ByteRegister;
                        var candidates = ByteOperation.Registers.Where(r => !Equals(r, destinationRegister)).ToList();
                        using (var source = ByteOperation.ReserveAnyRegister(this, candidates, null, RightOperand)) {
                            var sourceRegister = source.ByteRegister;
                            if (Equals(LeftOperand.Register, destinationRegister)) {
                                destinationRegister.Load(this, LeftOperand);
                                sourceRegister.Load(this, RightOperand);
                            }
                            else {
                                destinationRegister.Load(this, RightOperand);
                                sourceRegister.Load(this, LeftOperand);
                            }

                            WriteLine("\tinv\t" + sourceRegister.Name);
                            AddChanged(sourceRegister);
                            WriteLine("\tszc\t" + sourceRegister.Name + "," + destinationRegister.Name);
                            destinationRegister.Store(this, DestinationOperand);
                        }
                    }
                    break;
                case '^': {
                        void OperateRegister(WordRegister register)
                        {
                            register.Load(this, LeftOperand);
                            WriteLine("\txor\t" + Tms99.Compiler.OperandToString(this, RightOperand) + "," + register.Name);
                            register.Store(this, DestinationOperand);
                        }
                        if (DestinationOperand.Register is WordRegister destinationRegister) {
                            OperateRegister(destinationRegister);
                        }
                        else if (LeftOperand.Register is WordRegister leftRegister) {
                            OperateRegister(leftRegister);
                        }
                        else {
                            using var reservation = WordOperation.ReserveAnyRegister(this);
                            OperateRegister((WordRegister)reservation.WordRegister);
                        }

                        break;
                    }
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
