﻿/*
 * (c) 2008 MOSA - The Managed Operating System Alliance
 *
 * Licensed under the terms of the New BSD License.
 *
 * Authors:
 *  Michael Ruck (<mailto:sharpos@michaelruck.de>)
 */

using System;
using System.Collections.Generic;
using System.Text;

using Mosa.Runtime.CompilerFramework;
using IL = Mosa.Runtime.CompilerFramework.IL;
using Mosa.Runtime.Metadata.Signatures;
using Mosa.Runtime.Metadata;
using Mosa.Runtime.CompilerFramework.IR;
using System.Diagnostics;

namespace Mosa.Platforms.x86
{
    /// <summary>
    /// Implements the CIL default calling convention for x86.
    /// </summary>
    sealed class DefaultCallingConvention : ICallingConvention
    {
        #region Data members

        /// <summary>
        /// Holds the architecture of the calling convention.
        /// </summary>
        private IArchitecture architecture;

        #endregion // Data members

        #region Construction

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultCallingConvention"/>.
        /// </summary>
        /// <param name="architecture">The architecture of the calling convention.</param>
        public DefaultCallingConvention(IArchitecture architecture)
        {
            if (null == architecture)
                throw new ArgumentNullException(@"architecture");

            this.architecture = architecture;
        }

        #endregion // Construction

        #region ICallingConvention Members

        /// <summary>
        /// Expands the given invoke instruction to perform the method call.
        /// </summary>
        /// <param name="instruction">The invoke instruction to expand.</param>
        /// <returns>
        /// A single instruction or an array of instructions, which appropriately represent the method call.
        /// </returns>
        object ICallingConvention.Expand(IL.InvokeInstruction instruction)
        {
            /*
             * Calling convention is right-to-left, pushed on the stack. Return value in EAX for integral
             * types 4 bytes or less, XMM0 for floating point and EAX:EDX for 64-bit. If this is a method
             * of a type, the this argument is moved to ECX right before the call.
             * 
             */

            List<LegacyInstruction> instructions = new List<LegacyInstruction>();
            SigType I = new SigType(CilElementType.I);
            RegisterOperand esp = new RegisterOperand(I, GeneralPurposeRegister.ESP);
            bool moveThis = instruction.InvokeTarget.Signature.HasThis;
            int stackSize = CalculateStackSizeForParameters(instruction, moveThis);

            if (0 != stackSize)
            {
                instructions.Add(this.architecture.CreateInstruction(typeof(x86.Instructions.SubInstruction), esp, new ConstantOperand(I, stackSize)));
                instructions.Add(this.architecture.CreateInstruction(typeof(x86.Instructions.MoveInstruction), new RegisterOperand(architecture.NativeType, GeneralPurposeRegister.EDX), esp));

                Stack<Operand> operandStack = GetOperandStackFromInstruction(instruction, moveThis);

                int space = stackSize;
                CalculateRemainingSpace(instructions, operandStack, ref space);   
            }

            if (true == moveThis)
            {
                RegisterOperand ecx = new RegisterOperand(I, GeneralPurposeRegister.ECX);
                instructions.Add(this.architecture.CreateInstruction(typeof(Instructions.MoveInstruction), ecx, instruction.Operands[0]));
            }

            instructions.Add(this.architecture.CreateInstruction(typeof(CallInstruction), instruction.InvokeTarget));

            if (0 != stackSize)
            {
                instructions.Add(this.architecture.CreateInstruction(typeof(x86.Instructions.AddInstruction), esp, new ConstantOperand(I, stackSize)));
            }

            if (instruction.Results.Length > 0)
            {
                if (instruction.Results[0].StackType == StackTypeCode.Int64)
                {
                    MoveReturnValueTo64Bit(instruction.Results[0], instructions);
                }
                else
                {
                    MoveReturnValueTo32Bit(instruction.Results[0], instructions);
                }
            }

            return instructions;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="instruction"></param>
        /// <param name="moveThis"></param>
        private Stack<Operand> GetOperandStackFromInstruction(LegacyInstruction instruction, bool moveThis)
        {
            Stack<Operand> operandStack = new Stack<Operand>(instruction.Operands.Length);
            int thisArg = 1;

            foreach (Operand operand in instruction.Operands)
            {
                if (true == moveThis && 1 == thisArg)
                {
                    thisArg = 0;
                    continue;
                }
                operandStack.Push(operand);
            }

            return operandStack;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="instructionList"></param>
        /// <param name="operandStack"></param>
        /// <param name="space"></param>
        private void CalculateRemainingSpace(List<LegacyInstruction> instructionList, Stack<Operand> operandStack, ref int space)
        {
            while (0 != operandStack.Count)
            {
                Operand operand = operandStack.Pop();
                int size, alignment;

                this.architecture.GetTypeRequirements(operand.Type, out size, out alignment);
                space -= size;
                Push(instructionList, operand, space);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="resultOperand"></param>
        /// <param name="instructionList"></param>
        private void MoveReturnValueTo32Bit(Operand resultOperand, List<LegacyInstruction> instructionList)
        {
            RegisterOperand eax = new RegisterOperand(resultOperand.Type, GeneralPurposeRegister.EAX);

            instructionList.Add(new Instructions.MoveInstruction(resultOperand, eax));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="resultOperand"></param>
        /// <param name="instructionList"></param>
        private void MoveReturnValueTo64Bit(Operand resultOperand, List<LegacyInstruction> instructionList)
        {
            SigType I4 = new SigType(CilElementType.I4);
            SigType U4 = new SigType(CilElementType.U4);
            MemoryOperand memoryOperand = resultOperand as MemoryOperand;

            if (memoryOperand == null) return;

            MemoryOperand opL = new MemoryOperand(U4, memoryOperand.Base, memoryOperand.Offset);
            MemoryOperand opH = new MemoryOperand(I4, memoryOperand.Base, new IntPtr(memoryOperand.Offset.ToInt64() + 4));
            RegisterOperand eax = new RegisterOperand(U4, GeneralPurposeRegister.EAX);
            RegisterOperand edx = new RegisterOperand(I4, GeneralPurposeRegister.EDX);

            instructionList.AddRange(new LegacyInstruction[] {
                new Instructions.MoveInstruction(opL, eax),
                new Instructions.MoveInstruction(opH, edx)
            });
        }

        /// <summary>
        /// Pushes the specified instructions.
        /// </summary>
        /// <param name="instructions">The instructions.</param>
        /// <param name="op">The op.</param>
        /// <param name="stackSize">Size of the stack.</param>
        private void Push(List<LegacyInstruction> instructions, Operand op, int stackSize)
        {
            if (op is MemoryOperand)
            {
                RegisterOperand rop;
                switch (op.StackType)
                {
                    case StackTypeCode.O: goto case StackTypeCode.N;
                    case StackTypeCode.Ptr: goto case StackTypeCode.N;
                    case StackTypeCode.Int32: goto case StackTypeCode.N;
                    case StackTypeCode.N:
                        rop = new RegisterOperand(op.Type, GeneralPurposeRegister.EAX);
                        break;

                    case StackTypeCode.F:
                        rop = new RegisterOperand(op.Type, SSE2Register.XMM0);
                        break;

                    case StackTypeCode.Int64:
                        {
                            SigType I4 = new SigType(CilElementType.I4);
                            MemoryOperand mop = op as MemoryOperand;
                            Debug.Assert(null != mop, @"I8/U8 arg is not in a memory operand.");
                            RegisterOperand eax = new RegisterOperand(I4, GeneralPurposeRegister.EAX);
                            MemoryOperand opL = new MemoryOperand(I4, mop.Base, mop.Offset);
                            MemoryOperand opH = new MemoryOperand(I4, mop.Base, new IntPtr(mop.Offset.ToInt64() + 4));

                            instructions.AddRange(new LegacyInstruction[] {
                                new x86.Instructions.MoveInstruction(eax, opL),
                                new x86.Instructions.MoveInstruction(new MemoryOperand(op.Type, GeneralPurposeRegister.EDX, new IntPtr(stackSize)), eax),
                                new x86.Instructions.MoveInstruction(eax, opH),
                                new x86.Instructions.MoveInstruction(new MemoryOperand(op.Type, GeneralPurposeRegister.EDX, new IntPtr(stackSize+4)), eax),
                            });
                        }
                        return;
                        
                    default:
                        throw new NotSupportedException();
                }
                instructions.Add(this.architecture.CreateInstruction(typeof(Mosa.Runtime.CompilerFramework.IR.MoveInstruction), rop, op));
                op = rop;
            }
            else if (op is ConstantOperand && op.StackType == StackTypeCode.Int64)
            {
                Operand opL, opH;
                SigType I4 = new SigType(CilElementType.I4);
                RegisterOperand eax = new RegisterOperand(I4, GeneralPurposeRegister.EAX);
                LongOperandTransformationStage.SplitLongOperand(op, out opL, out opH);

                instructions.AddRange(new LegacyInstruction[] {
                    new x86.Instructions.MoveInstruction(eax, opL),
                    new x86.Instructions.MoveInstruction(new MemoryOperand(I4, GeneralPurposeRegister.EDX, new IntPtr(stackSize)), eax),
                    new x86.Instructions.MoveInstruction(eax, opH),
                    new x86.Instructions.MoveInstruction(new MemoryOperand(I4, GeneralPurposeRegister.EDX, new IntPtr(stackSize+4)), eax),
                });

                return;
            }

            instructions.Add(this.architecture.CreateInstruction(typeof(x86.Instructions.MoveInstruction), new MemoryOperand(op.Type, GeneralPurposeRegister.EDX, new IntPtr(stackSize)), op));
        }

        /// <summary>
        /// Calculates the stack size for parameters.
        /// </summary>
        /// <param name="instruction">The instruction.</param>
        /// <param name="hasThis">if set to <c>true</c> [has this].</param>
        /// <returns></returns>
        private int CalculateStackSizeForParameters(IL.InvokeInstruction instruction, bool hasThis)
        {
            int result = (hasThis ? -4 : 0);
            int size, alignment;
            
            foreach (Operand op in instruction.Operands)
            {
                this.architecture.GetTypeRequirements(op.Type, out size, out alignment);
                result += size;
            }
            return result;
        }

        /// <summary>
        /// Requests the calling convention to create an appropriate move instruction to populate the return
        /// value of a method.
        /// </summary>
        /// <param name="operand">The operand, that's holding the return value.</param>
        /// <returns>
        /// An instruction, which represents the appropriate move.
        /// </returns>
        LegacyInstruction[] ICallingConvention.MoveReturnValue(Operand operand)
        {
            int size, alignment;
            this.architecture.GetTypeRequirements(operand.Type, out size, out alignment);

            // FIXME: Do not issue a move, if the operand is already the destination register
            if (4 == size || 2 == size || 1 == size)
            {
                return new LegacyInstruction[] { this.architecture.CreateInstruction(typeof(Instructions.MoveInstruction), new RegisterOperand(operand.Type, GeneralPurposeRegister.EAX), operand) };
            }
            else if (8 == size && (operand.Type.Type == CilElementType.R4 || operand.Type.Type == CilElementType.R8))
            {
                return new LegacyInstruction[] { this.architecture.CreateInstruction(typeof(Instructions.MoveInstruction), new RegisterOperand(operand.Type, SSE2Register.XMM0), operand) };
            }
            else if (8 == size && (operand.Type.Type == CilElementType.I8 || operand.Type.Type == CilElementType.U8))
            {
                SigType I4 = new SigType(CilElementType.I4);
                SigType U4 = new SigType(CilElementType.U4);

                // Store if the operand is signed or unsigned by storing the type
                SigType HighType = operand.Type.Type == CilElementType.I8 ? new SigType(CilElementType.I4) : new SigType(CilElementType.U4);

                // Is it a constant operand?
                ConstantOperand cop = operand as ConstantOperand;
                Operand opL, opH;

                // If it's constant
                if (cop != null)
                {
                    long value = (long)cop.Value;
                    opL = new ConstantOperand(U4, (uint)(value & 0xFFFFFFFF));
                    if (HighType.Type == CilElementType.I8)
                        opH = new ConstantOperand(HighType, (int)((value >> 32) & 0xFFFFFFFF));
                    else
                        opH = new ConstantOperand(HighType, (uint)((value >> 32) & 0xFFFFFFFF));
                }
                else
                {
                    // No, could be a member or a plain memory operand
                    MemberOperand memberOp = operand as MemberOperand;
                    if (memberOp != null)
                    {
                        // We need to keep the member reference, otherwise the linker can't fixup
                        // the member address.
                        opL = new MemberOperand(memberOp.Member, U4, memberOp.Offset);
                        opH = new MemberOperand(memberOp.Member, HighType, new IntPtr(memberOp.Offset.ToInt64() + 4));
                    }
                    else
                    {
                        // Plain memory, we can handle it here
                        MemoryOperand mop = (MemoryOperand)operand;
                        opL = new MemoryOperand(U4, mop.Base, mop.Offset);
                        opH = new MemoryOperand(HighType, mop.Base, new IntPtr(mop.Offset.ToInt64() + 4));
                    }
                }

                // Like Win32: EDX:EAX
                return new LegacyInstruction[] { 
                    new Instructions.MoveInstruction(new RegisterOperand(U4, GeneralPurposeRegister.EAX), opL),
                    new Instructions.MoveInstruction(new RegisterOperand(I4, GeneralPurposeRegister.EDX), opH),
                };
            }
            else
            {
                throw new NotSupportedException();
            }

        }

        void ICallingConvention.GetStackRequirements(StackOperand stackOperand, out int size, out int alignment)
        {
            // Special treatment for some stack types
            // FIXME: Handle the size and alignment requirements of value types
            this.architecture.GetTypeRequirements(stackOperand.Type, out size, out alignment);
        }

        int ICallingConvention.OffsetOfFirstLocal
        {
            get 
            { 
                /*
                 * The first local variable is offset by 8 bytes from the start of
                 * the stack frame. [EBP-08h] (The first stack slot available for
                 * locals is [EBP], so we're reserving two 32-bit ints for
                 * system/compiler use as described below.
                 * 
                 * The first 4 bytes hold the method token, so that the GC can
                 * retrieve the method GC map and that we can do smart stack traces.
                 * 
                 * The second 4 bytes are used to hold the start of the method,
                 * so that we can embed floating point constants in our PIC.
                 * 
                 */
                return -8; 
            }
        }


        int ICallingConvention.OffsetOfFirstParameter
        {
            get 
            { 
                /*
                 * The first parameter is offset by 8 bytes from the start of
                 * the stack frame. [EBP+08h].
                 * 
                 * - [EBP+04h] holds the EDX register, which was pushed by the prologue instruction.
                 * - [EBP+08h] holds the return address, which was pushed by the call instruction.
                 * 
                 */
                return 8; 
            }
        }
        
        #endregion // ICallingConvention Members
    }
}
