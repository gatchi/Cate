﻿using System;
using System.Diagnostics;
using System.Linq;

namespace Inu.Cate
{
    public abstract class ByteRegister : Register
    {
        protected ByteRegister(int id, string name) : base(id, 1, name) { }

        public virtual WordRegister? PairRegister => null;

        public override bool Conflicts(Register? register)
        {
            switch (register) {
                case WordRegister wordRegister:
                    if (wordRegister.Contains(this))
                        return true;
                    break;
                    //case ByteRegister byteRegister:
                    //    if (PairRegister != null && PairRegister.Contains(byteRegister))
                    //        return true;
                    //    break;
            }
            return base.Conflicts(register);
        }

        public override bool Matches(Register register)
        {
            if (register is WordRegister wordRegister && wordRegister.Contains(this)) {
                return true;
            }
            return base.Matches(register);
        }


        public abstract void LoadConstant(Instruction instruction, string value);

        public virtual void LoadConstant(Instruction instruction, int value)
        {
            if (instruction.IsConstantAssigned(this, value)) {
                instruction.AddChanged(this);
                return;
            }
            LoadConstant(instruction, value.ToString());
            instruction.SetRegisterConstant(this, value);
        }

        public abstract void LoadFromMemory(Instruction instruction, Variable variable, int offset);
        public abstract void StoreToMemory(Instruction instruction, Variable variable, int offset);
        public abstract void LoadIndirect(Instruction instruction, WordRegister pointerRegister, int offset);
        public abstract void StoreIndirect(Instruction instruction, WordRegister pointerRegister, int offset);

        public virtual void LoadIndirect(Instruction instruction, Variable pointer, int offset)
        {
            var wordOperation = Compiler.Instance.WordOperation;
            var candidates = wordOperation.PointerRegisters(offset).Where(r => !r.Conflicts(this)).ToList();
            if (candidates.Count == 0) {
                candidates = wordOperation.Registers.Where(r => !r.Conflicts(this)).ToList();
            }

            using var reservation = wordOperation.ReserveAnyRegister(instruction, candidates);
            reservation.WordRegister.LoadFromMemory(instruction, pointer, 0);
            LoadIndirect(instruction, reservation.WordRegister, offset);
        }

        protected virtual void StoreIndirect(Instruction instruction, Variable pointer, int offset)
        {
            var register = instruction.GetVariableRegister(pointer, 0, r => r is WordRegister wr && wr.IsPointer(offset)) ??
                           instruction.GetVariableRegister(pointer, 0, r => r is WordRegister wr && wr.IsPointer(0));
            if (register is WordRegister wordRegister && (Equals(wordRegister, pointer.Register) || wordRegister.IsPointer(offset))) {
                StoreIndirect(instruction, wordRegister, offset);
                return;
            }

            var pointerRegisters = Compiler.Instance.WordOperation.PointerRegisters(offset);
            if (pointerRegisters.Count == 0) {
                pointerRegisters = Compiler.Instance.WordOperation.Registers;
            }
            var reservation = Compiler.Instance.WordOperation.ReserveAnyRegister(instruction, pointerRegisters);
            reservation.WordRegister.LoadFromMemory(instruction, pointer, 0);
            StoreIndirect(instruction, reservation.WordRegister, offset);
        }


        public void Load(Instruction instruction, Operand sourceOperand)
        {
            switch (sourceOperand) {
                case IntegerOperand integerOperand:
                    LoadConstant(instruction, integerOperand.IntegerValue);
                    return;
                case StringOperand stringOperand:
                    LoadConstant(instruction, stringOperand.StringValue);
                    instruction.RemoveRegisterAssignment(this);
                    return;
                case VariableOperand variableOperand: {
                        var register = instruction.GetVariableRegister(variableOperand);
                        if (register is ByteRegister byteRegister) {
                            if (Equals(byteRegister, this)) {
                                instruction.RemoveRegisterAssignment(this);
                            }
                            else if (byteRegister.ByteCount == 1) {
                                CopyFrom(instruction, byteRegister);
                                instruction.AddChanged((this));
                            }
                            else {
                                LoadFromMemory(instruction, variableOperand.Variable, variableOperand.Offset);
                                instruction.AddChanged(this);
                            }
                        }
                        else {
                            LoadFromMemory(instruction, variableOperand.Variable, variableOperand.Offset);
                            instruction.AddChanged(this);
                            instruction.RemoveRegisterAssignment(this);
                        }
                        instruction.SetVariableRegister(variableOperand, this);
                        instruction.CancelOperandRegister(variableOperand);
                        return;
                    }
                case IndirectOperand sourceIndirectOperand: {
                        var pointer = sourceIndirectOperand.Variable;
                        var offset = sourceIndirectOperand.Offset;
                        var register = pointer.Register ?? instruction.GetVariableRegister(pointer, 0);
                        if (register is WordRegister pointerRegister) {
                            if (pointerRegister.IsPointer(0)) {
                                LoadIndirect(instruction, pointerRegister, offset);
                                instruction.AddChanged(this);
                                instruction.CancelOperandRegister(sourceIndirectOperand);
                                return;
                            }
                            var candidates = WordOperation.Registers.Where(r => r.IsPointer(offset)).ToList();
                            if (candidates.Any()) {
                                var reservation = WordOperation.ReserveAnyRegister(instruction, candidates);
                                reservation.WordRegister.CopyFrom(instruction, pointerRegister);
                                LoadIndirect(instruction, reservation.WordRegister, offset);
                                instruction.AddChanged(this);
                                instruction.CancelOperandRegister(sourceIndirectOperand);
                                return;
                            }
                        }
                        LoadIndirect(instruction, pointer, offset);
                        instruction.AddChanged(this);
                        instruction.CancelOperandRegister(sourceIndirectOperand);
                        return;
                    }
                case ByteRegisterOperand byteRegisterOperand:
                    byteRegisterOperand.CopyTo(instruction, this);
                    return;
            }
            throw new NotImplementedException();
        }




        public virtual void Store(Instruction instruction, Operand destinationOperand)
        {
            switch (destinationOperand) {
                case StringOperand stringOperand:
                    StoreToMemory(instruction, stringOperand.StringValue);
                    return;
                case VariableOperand variableOperand: {
                        var variable = variableOperand.Variable;
                        var offset = variableOperand.Offset;
                        if (variable.Register is ByteRegister register) {
                            Debug.Assert(offset == 0);
                            //var register = Compiler.Instance.ByteOperation.RegisterFromId(variable.Register.Value);
                            if (Equals(register, this)) {
                            }
                            else {
                                register.CopyFrom(instruction, this);
                                instruction.AddChanged(register);
                            }
                        }
                        else {
                            StoreToMemory(instruction, variable, offset);
                        }
                        instruction.SetVariableRegister(variableOperand, this);
                        return;
                    }
                case IndirectOperand destinationIndirectOperand: {
                        var pointer = destinationIndirectOperand.Variable;
                        var offset = destinationIndirectOperand.Offset;
                        //var register = instruction.GetVariableRegister(pointer, 0);
                        //if (register is WordRegister pointerRegister && pointerRegister.IsPointer(offset)) {
                        //    StoreIndirect(instruction, pointerRegister, offset);
                        //    return;
                        //}
                        StoreIndirect(instruction, pointer, offset);
                        return;
                    }
                case ByteRegisterOperand byteRegisterOperand:
                    byteRegisterOperand.CopyFrom(instruction, this);
                    return;
            }
            throw new NotImplementedException();
        }


        public abstract void LoadFromMemory(Instruction instruction, string label);

        public abstract void StoreToMemory(Instruction instruction, string label);


        public abstract void CopyFrom(Instruction instruction, ByteRegister sourceRegister);

        public abstract void Operate(Instruction instruction, string operation, bool change, int count);
        public abstract void Operate(Instruction instruction, string operation, bool change, Operand operand);
        public abstract void Operate(Instruction instruction, string operation, bool change, string operand);

        public virtual void Exchange(Instruction instruction, ByteRegister register)
        {
            Debug.Assert(!Equals(this, register));
            using var reservation = ByteOperation.ReserveAnyRegister(instruction, ByteOperation.Registers.Where(r => !Equals(r, this) && !Equals(r, register)).ToList());
            reservation.ByteRegister.CopyFrom(instruction, register);
            register.CopyFrom(instruction, this);
            CopyFrom(instruction, reservation.ByteRegister);
        }

        public bool Conflicts(Operand operand) =>
            operand switch
            {
                VariableOperand variableOperand when Conflicts(variableOperand.Register) => true,
                IndirectOperand indirectOperand when Conflicts(indirectOperand.Variable.Register) => true,
                _ => false
            };
    }
}