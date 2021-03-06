// Copyright (c) MOSA Project. Licensed under the New BSD License.

using Mosa.Compiler.Common;
using Mosa.Compiler.Framework;
using Mosa.Compiler.Framework.IR;
using System;
using System.Diagnostics;

namespace Mosa.Platform.x86.Stages
{
	/// <summary>
	/// Transforms 64-bit arithmetic to 32-bit operations.
	/// </summary>
	/// <remarks>
	/// This stage translates all 64-bit operations to appropriate 32-bit operations on
	/// architectures without appropriate 64-bit integral operations.
	/// </remarks>
	public sealed class LongOperandTransformationStage : BaseTransformationStage
	{
		private Operand ConstantFour;

		protected override void PopulateVisitationDictionary()
		{
			visitationDictionary[IRInstruction.AddSigned] = AddSigned;
			visitationDictionary[IRInstruction.AddUnsigned] = AddUnsigned;
			visitationDictionary[IRInstruction.ArithmeticShiftRight] = ArithmeticShiftRight;
			visitationDictionary[IRInstruction.Call] = Call;
			visitationDictionary[IRInstruction.CompareInteger] = CompareInteger;
			visitationDictionary[IRInstruction.CompareIntegerBranch] = CompareIntegerBranch;
			visitationDictionary[IRInstruction.DivSigned] = DivSigned;
			visitationDictionary[IRInstruction.DivUnsigned] = DivUnsigned;
			visitationDictionary[IRInstruction.LoadInteger] = LoadInteger;
			visitationDictionary[IRInstruction.LoadSignExtended] = LoadSignExtended;
			visitationDictionary[IRInstruction.LoadZeroExtended] = LoadZeroExtended;
			visitationDictionary[IRInstruction.LoadParameterInteger] = LoadParameterInteger;
			visitationDictionary[IRInstruction.LoadParameterSignExtended] = LoadParameterSignExtended;
			visitationDictionary[IRInstruction.LoadParameterZeroExtended] = LoadParameterZeroExtended;
			visitationDictionary[IRInstruction.LogicalAnd] = LogicalAnd;
			visitationDictionary[IRInstruction.LogicalNot] = LogicalNot;
			visitationDictionary[IRInstruction.LogicalOr] = LogicalOr;
			visitationDictionary[IRInstruction.LogicalXor] = LogicalXor;
			visitationDictionary[IRInstruction.MoveInteger] = MoveInteger;
			visitationDictionary[IRInstruction.MoveSignExtended] = MoveSignExtended;
			visitationDictionary[IRInstruction.MoveZeroExtended] = MoveZeroExtended;
			visitationDictionary[IRInstruction.MulSigned] = MulSigned;
			visitationDictionary[IRInstruction.MulUnsigned] = MulUnsigned;
			visitationDictionary[IRInstruction.RemSigned] = RemSigned;
			visitationDictionary[IRInstruction.RemUnsigned] = RemUnsigned;
			visitationDictionary[IRInstruction.Return] = Return;
			visitationDictionary[IRInstruction.ShiftLeft] = ShiftLeft;
			visitationDictionary[IRInstruction.ShiftRight] = ShiftRight;
			visitationDictionary[IRInstruction.StoreInteger] = StoreInteger;
			visitationDictionary[IRInstruction.StoreParameterInteger] = StoreParameterInteger;
			visitationDictionary[IRInstruction.SubSigned] = SubSigned;
			visitationDictionary[IRInstruction.SubUnsigned] = SubUnsigned;
		}

		protected override void Setup()
		{
			base.Setup();

			ConstantFour = Operand.CreateConstant(MethodCompiler.TypeSystem, 4);
		}

		#region Visitation Methods

		/// <summary>
		/// Visitation function for AddSigned.
		/// </summary>
		/// <param name="context">The context.</param>
		private void AddSigned(Context context)
		{
			if (Any64Bit(context))
			{
				ExpandAdd(context);
			}
		}

		/// <summary>
		/// Visitation function for AddUnsigned.
		/// </summary>
		/// <param name="context">The context.</param>
		private void AddUnsigned(Context context)
		{
			if (Any64Bit(context))
			{
				ExpandAdd(context);
			}
		}

		/// <summary>
		/// Visitation function for ArithmeticShiftRightInstruction.
		/// </summary>
		/// <param name="context">The context.</param>
		private void ArithmeticShiftRight(Context context)
		{
			if (context.Operand1.Is64BitInteger)
			{
				ExpandArithmeticShiftRight(context);
			}
		}

		/// <summary>
		/// Visitation function for CallInstruction.
		/// </summary>
		/// <param name="context">The context.</param>
		private void Call(Context context)
		{
			Operand op0L, op0H;

			if (context.Result != null && context.Result.Is64BitInteger)
			{
				SplitLongOperand(context.Result, out op0L, out op0H);
			}

			foreach (var operand in context.Operands)
			{
				if (operand.Is64BitInteger)
				{
					SplitLongOperand(operand, out op0L, out op0H);
				}
			}
		}

		/// <summary>
		/// Visitation function for IntegerCompare.
		/// </summary>
		/// <param name="context">The context.</param>
		private void CompareInteger(Context context)
		{
			if (context.Operand1.Is64BitInteger)
			{
				ExpandComparison(context);
			}
		}

		/// <summary>
		/// Visitation function for IntegerCompareInstruction.
		/// </summary>
		/// <param name="context">The context.</param>
		private void CompareIntegerBranch(Context context)
		{
			if (context.Operand1.Is64BitInteger || context.Operand2.Is64BitInteger)
			{
				ExpandBinaryBranch(context);
			}
		}

		/// <summary>
		/// Visitation function for DivSInstruction.
		/// </summary>
		/// <param name="context">The context.</param>
		private void DivSigned(Context context)
		{
			if (Any64Bit(context))
			{
				ExpandDiv(context);
			}
		}

		/// <summary>
		/// Visitation function for DivUInstruction.
		/// </summary>
		/// <param name="context">The context.</param>
		private void DivUnsigned(Context context)
		{
			if (Any64Bit(context))
			{
				ExpandUDiv(context);
			}
		}

		/// <summary>
		/// Visitation function for Load.
		/// </summary>
		/// <param name="context">The context.</param>
		private void LoadInteger(Context context)
		{
			if (context.Operand1.Is64BitInteger || context.Result.Is64BitInteger)
			{
				ExpandLoad(context);
			}
		}

		private void LoadParameterInteger(Context context)
		{
			Debug.Assert(!context.Result.IsR4);
			Debug.Assert(!context.Result.IsR8);

			if (context.Operand1.Is64BitInteger || context.Result.Is64BitInteger)
			{
				ExpandLoadParameter(context);
			}
		}

		private void LoadParameterSignExtended(Context context)
		{
			Debug.Assert(!Any64Bit(context));
		}

		private void LoadParameterZeroExtended(Context context)
		{
			Debug.Assert(!Any64Bit(context));
		}

		/// <summary>
		/// Visitation function for Load Sign Extended.
		/// </summary>
		/// <param name="context">The context.</param>
		private void LoadSignExtended(Context context)
		{
			Debug.Assert(!Any64Bit(context));
		}

		/// <summary>
		/// Visitation function for Load Zero Extended.
		/// </summary>
		/// <param name="context">The context.</param>
		private void LoadZeroExtended(Context context)
		{
			Debug.Assert(!Any64Bit(context));
		}

		/// <summary>
		/// Visitation function for LogicalAndInstruction.
		/// </summary>
		/// <param name="context">The context.</param>
		private void LogicalAnd(Context context)
		{
			if (Any64Bit(context))
			{
				ExpandAnd(context);
			}
		}

		/// <summary>
		/// Visitation function for LogicalNotInstruction.
		/// </summary>
		/// <param name="context">The context.</param>
		private void LogicalNot(Context context)
		{
			if (Any64Bit(context))
			{
				ExpandNot(context);
			}
		}

		/// <summary>
		/// Visitation function for LogicalOrInstruction.
		/// </summary>
		/// <param name="context">The context.</param>
		private void LogicalOr(Context context)
		{
			if (Any64Bit(context))
			{
				ExpandOr(context);
			}
		}

		/// <summary>
		/// Visitation function for LogicalXorInstruction.
		/// </summary>
		/// <param name="context">The context.</param>
		private void LogicalXor(Context context)
		{
			if (Any64Bit(context))
			{
				ExpandXor(context);
			}
		}

		/// <summary>
		/// Visitation function for MoveInstruction.
		/// </summary>
		/// <param name="context">The context.</param>
		private void MoveInteger(Context context)
		{
			if (Any64Bit(context))
			{
				ExpandMoveInteger(context);
			}
		}

		/// <summary>
		/// Visitation function for SignExtendedMoveInstruction.
		/// </summary>
		/// <param name="context">The context.</param>
		private void MoveSignExtended(Context context)
		{
			if (Any64Bit(context))
			{
				ExpandSignedMove(context);
			}
		}

		/// <summary>
		/// Zeroes the extended move instruction.
		/// </summary>
		/// <param name="context">The context.</param>
		private void MoveZeroExtended(Context context)
		{
			if (Any64Bit(context))
			{
				ExpandUnsignedMove(context);
			}
		}

		/// <summary>
		/// Visitation function for MulSInstruction.
		/// </summary>
		/// <param name="context">The context.</param>
		private void MulSigned(Context context)
		{
			if (Any64Bit(context))
			{
				ExpandMul(context);
			}
		}

		/// <summary>
		/// Visitation function for MulUInstruction.
		/// </summary>
		/// <param name="context">The context.</param>
		private void MulUnsigned(Context context)
		{
			if (Any64Bit(context))
			{
				ExpandMul(context);
			}
		}

		/// <summary>
		/// Visitation function for RemSigned.
		/// </summary>
		/// <param name="context">The context.</param>
		private void RemSigned(Context context)
		{
			if (Any64Bit(context))
			{
				ExpandRem(context);
			}
		}

		/// <summary>
		/// Visitation function for RemUInstruction.
		/// </summary>
		/// <param name="context">The context.</param>
		private void RemUnsigned(Context context)
		{
			if (Any64Bit(context))
			{
				ExpandURem(context);
			}
		}

		/// <summary>
		/// Visitation function for ReturnInstruction.
		/// </summary>
		/// <param name="context">The context.</param>
		private void Return(Context context)
		{
			Operand op0L, op0H;

			if (context.Result != null && context.Result.Is64BitInteger)
			{
				SplitLongOperand(context.Result, out op0L, out op0H);
			}

			foreach (var operand in context.Operands)
			{
				if (operand.Is64BitInteger)
				{
					SplitLongOperand(operand, out op0L, out op0H);
				}
			}
		}

		/// <summary>
		/// Visitation function for ShiftLeftInstruction.
		/// </summary>
		/// <param name="context">The context.</param>
		private void ShiftLeft(Context context)
		{
			if (Any64Bit(context))
			{
				ExpandShiftLeft(context);
			}
		}

		/// <summary>
		/// Visitation function for ShiftRightInstruction.
		/// </summary>
		/// <param name="context">The context.</param>
		private void ShiftRight(Context context)
		{
			if (Any64Bit(context))
			{
				ExpandShiftRight(context);
			}
		}

		/// <summary>
		/// Visitation function for StoreInstruction.
		/// </summary>
		/// <param name="context">The context.</param>
		private void StoreInteger(Context context)
		{
			if (context.Size == InstructionSize.Size64)
			{
				ExpandStore(context);
			}
		}

		/// <summary>
		/// Visitation function for StoreInstruction.
		/// </summary>
		/// <param name="context">The context.</param>
		private void StoreParameterInteger(Context context)
		{
			if (context.Size == InstructionSize.Size64)
			{
				ExpandStoreParameter(context);
			}
		}

		/// <summary>
		/// Visitation function for SubSInstruction.
		/// </summary>
		/// <param name="context">The context.</param>
		private void SubSigned(Context context)
		{
			if (Any64Bit(context))
			{
				ExpandSub(context);
			}
		}

		/// <summary>
		/// Visitation function for SubUInstruction.
		/// </summary>
		/// <param name="context">The context.</param>
		private void SubUnsigned(Context context)
		{
			if (Any64Bit(context))
			{
				ExpandSub(context);
			}
		}

		#endregion Visitation Methods

		#region Utility Methods

		/// <summary>
		/// Ares the any64 bit.
		/// </summary>
		/// <param name="context">The context.</param>
		/// <returns></returns>
		public static bool Any64Bit(Context context)
		{
			if (context.Result.Is64BitInteger)
				return true;

			foreach (var operand in context.Operands)
				if (operand.Is64BitInteger)
					return true;

			return false;
		}

		public static void SplitLongOperand(BaseMethodCompiler methodCompiler, Operand operand, out Operand operandLow, out Operand operandHigh)
		{
			if (operand.Is64BitInteger)
			{
				methodCompiler.VirtualRegisters.SplitLongOperand(methodCompiler.TypeSystem, operand);
				operandLow = operand.Low;
				operandHigh = operand.High;
				return;
			}
			else
			{
				operandLow = operand;
				operandHigh = methodCompiler.ConstantZero;
				return;
			}
		}

		/// <summary>
		/// Expands the add instruction for 64-bit operands.
		/// </summary>
		/// <param name="context">The context.</param>
		private void ExpandAdd(Context context)
		{
			Operand op0H, op1H, op2H, op0L, op1L, op2L;
			SplitLongOperand(context.Result, out op0L, out op0H);
			SplitLongOperand(context.Operand1, out op1L, out op1H);
			SplitLongOperand(context.Operand2, out op2L, out op2H);

			Operand v1 = AllocateVirtualRegister(TypeSystem.BuiltIn.I4);
			Operand v2 = AllocateVirtualRegister(TypeSystem.BuiltIn.U4);

			context.SetInstruction(X86.Mov, v2, op1L);
			context.AppendInstruction(X86.Add, v2, v2, op2L);
			context.AppendInstruction(X86.Mov, op0L, v2);
			context.AppendInstruction(X86.Mov, v1, op1H);
			context.AppendInstruction(X86.Adc, v1, v1, op2H);
			if (!op0H.IsConstantZero)
				context.AppendInstruction(X86.Mov, op0H, v1);
		}

		/// <summary>
		/// Expands the and instruction for 64-bits.
		/// </summary>
		/// <param name="context">The context.</param>
		private void ExpandAnd(Context context)
		{
			Operand op0H, op1H, op2H, op0L, op1L, op2L;
			SplitLongOperand(context.Result, out op0L, out op0H);
			SplitLongOperand(context.Operand1, out op1L, out op1H);
			SplitLongOperand(context.Operand2, out op2L, out op2H);

			if (!context.Result.Is64BitInteger)
			{
				context.SetInstruction(X86.Mov, op0L, op1L);
				context.AppendInstruction(X86.And, op0L, op0L, op2L);
			}
			else
			{
				context.SetInstruction(X86.Mov, op0H, op1H);
				context.AppendInstruction(X86.Mov, op0L, op1L);
				context.AppendInstruction(X86.And, op0H, op0H, op2H);
				context.AppendInstruction(X86.And, op0L, op0L, op2L);
			}
		}

		/// <summary>
		/// Expands the arithmetic shift right instruction for 64-bits.
		/// </summary>
		/// <param name="context">The context.</param>
		private void ExpandArithmeticShiftRight(Context context)
		{
			Operand count = context.Operand2;

			Operand op0H, op1H, op0L, op1L;
			SplitLongOperand(context.Result, out op0L, out op0H);
			SplitLongOperand(context.Operand1, out op1L, out op1H);

			Operand eax = AllocateVirtualRegister(TypeSystem.BuiltIn.I4);
			Operand edx = AllocateVirtualRegister(TypeSystem.BuiltIn.I4);
			Operand ecx = AllocateVirtualRegister(TypeSystem.BuiltIn.I4);

			Context[] newBlocks = CreateNewBlockContexts(6);
			Context nextBlock = Split(context);

			context.SetInstruction(X86.Jmp, newBlocks[0].Block);

			newBlocks[0].AppendInstruction(X86.Mov, ecx, count);
			newBlocks[0].AppendInstruction(X86.Mov, edx, op1H);
			newBlocks[0].AppendInstruction(X86.Mov, eax, op1L);
			newBlocks[0].AppendInstruction(X86.Cmp, null, ecx, Operand.CreateConstant(TypeSystem, 64));
			newBlocks[0].AppendInstruction(X86.Branch, ConditionCode.UnsignedGreaterOrEqual, newBlocks[4].Block);
			newBlocks[0].AppendInstruction(X86.Jmp, newBlocks[1].Block);

			newBlocks[1].AppendInstruction(X86.Cmp, null, ecx, Operand.CreateConstant(TypeSystem, 32));
			newBlocks[1].AppendInstruction(X86.Branch, ConditionCode.UnsignedGreaterOrEqual, newBlocks[3].Block);
			newBlocks[1].AppendInstruction(X86.Jmp, newBlocks[2].Block);

			newBlocks[2].AppendInstruction(X86.Shrd, eax, eax, edx, ecx);
			newBlocks[2].AppendInstruction(X86.Sar, edx, edx, ecx);
			newBlocks[2].AppendInstruction(X86.Jmp, newBlocks[5].Block);

			newBlocks[3].AppendInstruction(X86.Mov, eax, edx);
			newBlocks[3].AppendInstruction(X86.Sar, edx, edx, Operand.CreateConstant(TypeSystem, 0x1F));
			newBlocks[3].AppendInstruction(X86.And, ecx, ecx, Operand.CreateConstant(TypeSystem, 0x1F));
			newBlocks[3].AppendInstruction(X86.Sar, eax, eax, ecx);
			newBlocks[3].AppendInstruction(X86.Jmp, newBlocks[5].Block);

			newBlocks[4].AppendInstruction(X86.Sar, edx, edx, Operand.CreateConstant(TypeSystem, 0x1F));
			newBlocks[4].AppendInstruction(X86.Mov, eax, edx);
			newBlocks[4].AppendInstruction(X86.Jmp, newBlocks[5].Block);

			newBlocks[5].AppendInstruction(X86.Mov, op0H, edx);
			newBlocks[5].AppendInstruction(X86.Mov, op0L, eax);
			newBlocks[5].AppendInstruction(X86.Jmp, nextBlock.Block);
		}

		/// <summary>
		/// Expands the binary branch instruction for 64-bits.
		/// </summary>
		/// <param name="context">The context.</param>
		private void ExpandBinaryBranch(Context context)
		{
			//Debug.Assert(context.BranchTargets.Length == 1);

			Operand op1L, op1H, op2L, op2H;
			SplitLongOperand(context.Operand1, out op1L, out op1H);
			SplitLongOperand(context.Operand2, out op2L, out op2H);

			BasicBlock target = context.BranchTargets[0];
			ConditionCode conditionCode = context.ConditionCode;

			Context nextBlock = Split(context);
			Context[] newBlocks = CreateNewBlockContexts(2);

			// FIXME: If the conditional branch and unconditional branch are the same, this could cause a problem
			target.PreviousBlocks.Remove(context.Block);

			// The block is being split on the condition, so the new next block has one too many next blocks!
			nextBlock.Block.NextBlocks.Remove(target);

			// Compare high dwords
			context.SetInstruction(X86.Cmp, null, op1H, op2H);
			context.AppendInstruction(X86.Branch, ConditionCode.Equal, newBlocks[1].Block);
			context.AppendInstruction(X86.Jmp, newBlocks[0].Block);

			// Branch if check already gave results
			newBlocks[0].AppendInstruction(X86.Branch, conditionCode, target);
			newBlocks[0].AppendInstruction(X86.Jmp, nextBlock.Block);

			// Compare low dwords
			newBlocks[1].AppendInstruction(X86.Cmp, null, op1L, op2L);
			newBlocks[1].AppendInstruction(X86.Branch, conditionCode.GetUnsigned(), target);
			newBlocks[1].AppendInstruction(X86.Jmp, nextBlock.Block);
		}

		/// <summary>
		/// Expands the binary comparison instruction for 64-bits.
		/// </summary>
		/// <param name="context">The context.</param>
		private void ExpandComparison(Context context)
		{
			Operand op0 = context.Result;
			Operand op1 = context.Operand1;
			Operand op2 = context.Operand2;

			Debug.Assert(op1 != null && op2 != null, @"IntegerCompareInstruction operand not memory!");
			Debug.Assert(op0.IsVirtualRegister, @"IntegerCompareInstruction result not memory and not register!");

			Operand op1L, op1H, op2L, op2H;
			SplitLongOperand(op1, out op1L, out op1H);
			SplitLongOperand(op2, out op2L, out op2H);

			ConditionCode conditionCode = context.ConditionCode;

			Context nextBlock = Split(context);
			Context[] newBlocks = CreateNewBlockContexts(4);

			// Compare high dwords
			context.SetInstruction(X86.Cmp, null, op1H, op2H);
			context.AppendInstruction(X86.Branch, ConditionCode.Equal, newBlocks[1].Block);
			context.AppendInstruction(X86.Jmp, newBlocks[0].Block);

			// Branch if check already gave results
			newBlocks[0].AppendInstruction(X86.Branch, conditionCode, newBlocks[2].Block);
			newBlocks[0].AppendInstruction(X86.Jmp, newBlocks[3].Block);

			// Compare low dwords
			newBlocks[1].AppendInstruction(X86.Cmp, null, op1L, op2L);
			newBlocks[1].AppendInstruction(X86.Branch, conditionCode.GetUnsigned(), newBlocks[2].Block);
			newBlocks[1].AppendInstruction(X86.Jmp, newBlocks[3].Block);

			// Success
			newBlocks[2].AppendInstruction(X86.Mov, op0, Operand.CreateConstant(TypeSystem, 1));
			newBlocks[2].AppendInstruction(X86.Jmp, nextBlock.Block);

			// Failed
			newBlocks[3].AppendInstruction(X86.Mov, op0, ConstantZero);
			newBlocks[3].AppendInstruction(X86.Jmp, nextBlock.Block);
		}

		/// <summary>
		/// Expands the div.
		/// </summary>
		/// <param name="context">The context.</param>
		private void ExpandDiv(Context context)
		{
			Operand op0H, op1H, op2H, op0L, op1L, op2L;
			var result = context.Result;
			var op1 = context.Operand1;
			var op2 = context.Operand2;

			SplitLongOperand(result, out op0L, out op0H);
			SplitLongOperand(op1, out op1L, out op1H);
			SplitLongOperand(op2, out op2L, out op2H);

			ReplaceWithDivisionCall(context, "sdiv64");
			context.Result = result;
			context.Operand2 = op1;
			context.Operand3 = op2;
			context.OperandCount = 3;
			context.ResultCount = 1;
		}

		/// <summary>
		/// Expands the load instruction for 64-bits.
		/// </summary>
		/// <param name="context">The context.</param>
		private void ExpandLoadParameter(Context context)
		{
			Operand op0L, op0H;
			SplitLongOperand(context.Result, out op0L, out op0H);

			Operand op1L, op1H;
			SplitLongOperand(context.Operand1, out op1L, out op1H);

			context.SetInstruction(X86.MovLoad, InstructionSize.Size32, op0L, StackFrame, op1L);
			context.AppendInstruction(X86.MovLoad, InstructionSize.Size32, op0H, StackFrame, op1H);
		}

		/// <summary>
		/// Expands the load instruction for 64-bits.
		/// </summary>
		/// <param name="context">The context.</param>
		private void ExpandLoad(Context context)
		{
			Operand address = context.Operand1;
			Operand offset = context.Operand2;

			Operand op0L, op0H;
			SplitLongOperand(context.Result, out op0L, out op0H);

			context.SetInstruction(X86.MovLoad, InstructionSize.Size32, op0L, address, offset);

			if (offset.IsResolvedConstant)
			{
				var offset2 = offset.IsConstantZero ? ConstantFour : Operand.CreateConstant(TypeSystem, offset.Offset + NativePointerSize);
				context.AppendInstruction(X86.MovLoad, InstructionSize.Size32, op0H, address, offset2);
				return;
			}

			Operand op2L, op2H;
			SplitLongOperand(offset, out op2L, out op2H);

			Operand v1 = AllocateVirtualRegister(TypeSystem.BuiltIn.U4);

			context.AppendInstruction(X86.Add, InstructionSize.Size32, v1, op2L, ConstantFour);
			context.AppendInstruction(X86.MovLoad, InstructionSize.Size32, op0H, address, v1);
		}

		/// <summary>
		/// Expands the move instruction for 64-bits.
		/// </summary>
		/// <param name="context">The context.</param>
		private void ExpandMoveInteger(Context context)
		{
			Operand op0L, op0H, op1L, op1H;

			SplitLongOperand(context.Operand1, out op1L, out op1H);

			if (context.Result.Is64BitInteger)
			{
				SplitLongOperand(context.Result, out op0L, out op0H);

				context.SetInstruction(X86.Mov, InstructionSize.Size32, op0L, op1L);
				context.AppendInstruction(X86.Mov, InstructionSize.Size32, op0H, op1H);
			}
			else
			{
				context.SetInstruction(X86.Mov, InstructionSize.Size32, context.Result, op1L);
			}
		}

		/// <summary>
		/// Expands the mul instruction for 64-bit operands.
		/// </summary>
		/// <param name="context">The context.</param>
		private void ExpandMul(Context context)
		{
			Operand op0H, op1H, op2H, op0L, op1L, op2L;
			SplitLongOperand(context.Result, out op0L, out op0H);
			SplitLongOperand(context.Operand1, out op1L, out op1H);
			SplitLongOperand(context.Operand2, out op2L, out op2H);

			Operand eax = AllocateVirtualRegister(TypeSystem.BuiltIn.I4);
			Operand edx = AllocateVirtualRegister(TypeSystem.BuiltIn.I4);
			Operand ebx = AllocateVirtualRegister(TypeSystem.BuiltIn.I4);

			Operand v20 = AllocateVirtualRegister(TypeSystem.BuiltIn.I4);
			Operand v12 = AllocateVirtualRegister(TypeSystem.BuiltIn.I4);

			// unoptimized
			context.SetInstruction(X86.Mov, eax, op2L);
			context.AppendInstruction(X86.Mov, v20, eax);
			context.AppendInstruction(X86.Mov, eax, v20);
			context.AppendInstruction2(X86.Mul, edx, eax, eax, op1L);
			context.AppendInstruction(X86.Mov, op0L, eax);

			if (!op0H.IsConstantZero)
			{
				context.AppendInstruction(X86.Mov, v12, edx);
				context.AppendInstruction(X86.Mov, eax, op1L);
				context.AppendInstruction(X86.Mov, ebx, eax);
				context.AppendInstruction(X86.IMul, ebx, ebx, op2H);
				context.AppendInstruction(X86.Mov, eax, v12);
				context.AppendInstruction(X86.Add, eax, eax, ebx);
				context.AppendInstruction(X86.Mov, ebx, op2L);
				context.AppendInstruction(X86.IMul, ebx, ebx, op1H);
				context.AppendInstruction(X86.Add, eax, eax, ebx);
				context.AppendInstruction(X86.Mov, v12, eax);
				context.AppendInstruction(X86.Mov, op0H, v12);
			}
		}

		/// <summary>
		/// Expands the neg instruction for 64-bits.
		/// </summary>
		/// <param name="context">The context.</param>
		private void ExpandNeg(Context context)
		{
			throw new NotSupportedException();
		}

		/// <summary>
		/// Expands the not instruction for 64-bits.
		/// </summary>
		/// <param name="context">The context.</param>
		private void ExpandNot(Context context)
		{
			Operand op0H, op1H, op0L, op1L;
			SplitLongOperand(context.Result, out op0L, out op0H);
			SplitLongOperand(context.Operand1, out op1L, out op1H);

			Operand eax = AllocateVirtualRegister(TypeSystem.BuiltIn.U4);

			context.SetInstruction(X86.Mov, eax, op1H);
			context.AppendInstruction(X86.Not, eax, eax);
			context.AppendInstruction(X86.Mov, op0H, eax);

			context.AppendInstruction(X86.Mov, eax, op1L);
			context.AppendInstruction(X86.Not, eax, eax);
			context.AppendInstruction(X86.Mov, op0L, eax);
		}

		/// <summary>
		/// Expands the or instruction for 64-bits.
		/// </summary>
		/// <param name="context">The context.</param>
		private void ExpandOr(Context context)
		{
			Operand op0H, op1H, op2H, op0L, op1L, op2L;
			SplitLongOperand(context.Result, out op0L, out op0H);
			SplitLongOperand(context.Operand1, out op1L, out op1H);
			SplitLongOperand(context.Operand2, out op2L, out op2H);

			context.SetInstruction(X86.Mov, op0H, op1H);
			context.AppendInstruction(X86.Mov, op0L, op1L);
			context.AppendInstruction(X86.Or, op0H, op0H, op2H);
			context.AppendInstruction(X86.Or, op0L, op0L, op2L);
		}

		/// <summary>
		/// Expands the rem.
		/// </summary>
		/// <param name="context">The context.</param>
		private void ExpandRem(Context context)
		{
			Operand op0H, op1H, op2H, op0L, op1L, op2L;
			var result = context.Result;
			var op1 = context.Operand1;
			var op2 = context.Operand2;

			SplitLongOperand(result, out op0L, out op0H);
			SplitLongOperand(op1, out op1L, out op1H);
			SplitLongOperand(op2, out op2L, out op2H);

			ReplaceWithDivisionCall(context, "smod64");
			context.Result = result;
			context.Operand2 = op1;
			context.Operand3 = op2;
			context.OperandCount = 3;
			context.ResultCount = 1;
		}

		/// <summary>
		/// Expands the shift left instruction for 64-bits.
		/// </summary>
		/// <param name="context">The context.</param>
		private void ExpandShiftLeft(Context context)
		{
			Operand count = context.Operand2;

			Operand op0H, op1H, op0L, op1L;
			SplitLongOperand(context.Result, out op0L, out op0H);
			SplitLongOperand(context.Operand1, out op1L, out op1H);

			Operand eax = AllocateVirtualRegister(TypeSystem.BuiltIn.I4);
			Operand edx = AllocateVirtualRegister(TypeSystem.BuiltIn.I4);
			Operand ecx = AllocateVirtualRegister(TypeSystem.BuiltIn.I4);

			Context nextBlock = Split(context);
			Context[] newBlocks = CreateNewBlockContexts(6);

			context.SetInstruction(X86.Jmp, newBlocks[0].Block);

			newBlocks[0].AppendInstruction(X86.Mov, ecx, count);
			newBlocks[0].AppendInstruction(X86.Mov, edx, op1H);
			newBlocks[0].AppendInstruction(X86.Mov, eax, op1L);
			newBlocks[0].AppendInstruction(X86.Cmp, null, ecx, Operand.CreateConstant(TypeSystem, 64));
			newBlocks[0].AppendInstruction(X86.Branch, ConditionCode.UnsignedGreaterOrEqual, newBlocks[4].Block);
			newBlocks[0].AppendInstruction(X86.Jmp, newBlocks[1].Block);

			newBlocks[1].AppendInstruction(X86.Cmp, null, ecx, Operand.CreateConstant(TypeSystem, 32));
			newBlocks[1].AppendInstruction(X86.Branch, ConditionCode.UnsignedGreaterOrEqual, newBlocks[3].Block);
			newBlocks[1].AppendInstruction(X86.Jmp, newBlocks[2].Block);

			newBlocks[2].AppendInstruction(X86.Shld, edx, edx, eax, ecx);
			newBlocks[2].AppendInstruction(X86.Shl, eax, eax, ecx);
			newBlocks[2].AppendInstruction(X86.Jmp, newBlocks[5].Block);

			newBlocks[3].AppendInstruction(X86.Mov, edx, eax);
			newBlocks[3].AppendInstruction(X86.Mov, eax, ConstantZero);
			newBlocks[3].AppendInstruction(X86.And, ecx, ecx, Operand.CreateConstant(TypeSystem, 0x1F));
			newBlocks[3].AppendInstruction(X86.Shl, edx, edx, ecx);
			newBlocks[3].AppendInstruction(X86.Jmp, newBlocks[5].Block);

			newBlocks[4].AppendInstruction(X86.Mov, eax, ConstantZero);
			newBlocks[4].AppendInstruction(X86.Mov, edx, ConstantZero);
			newBlocks[4].AppendInstruction(X86.Jmp, newBlocks[5].Block);

			newBlocks[5].AppendInstruction(X86.Mov, op0H, edx);
			newBlocks[5].AppendInstruction(X86.Mov, op0L, eax);
			newBlocks[5].AppendInstruction(X86.Jmp, nextBlock.Block);
		}

		/// <summary>
		/// Expands the shift right instruction for 64-bits.
		/// </summary>
		/// <param name="context">The context.</param>
		private void ExpandShiftRight(Context context)
		{
			Operand count = context.Operand2;

			Operand op0H, op1H, op0L, op1L;
			SplitLongOperand(context.Result, out op0L, out op0H);
			SplitLongOperand(context.Operand1, out op1L, out op1H);

			Operand ecx = AllocateVirtualRegister(TypeSystem.BuiltIn.I4);
			Operand v1 = AllocateVirtualRegister(TypeSystem.BuiltIn.I4);

			Context nextBlock = Split(context);
			Context[] newBlocks = CreateNewBlockContexts(4);

			context.SetInstruction(X86.Mov, ecx, count);
			context.AppendInstruction(X86.Cmp, null, ecx, Operand.CreateConstant(TypeSystem, 64));
			context.AppendInstruction(X86.Branch, ConditionCode.UnsignedGreaterOrEqual, newBlocks[3].Block);
			context.AppendInstruction(X86.Jmp, newBlocks[0].Block);

			newBlocks[0].AppendInstruction(X86.Cmp, null, ecx, Operand.CreateConstant(TypeSystem, 32));
			newBlocks[0].AppendInstruction(X86.Branch, ConditionCode.UnsignedGreaterOrEqual, newBlocks[2].Block);
			newBlocks[0].AppendInstruction(X86.Jmp, newBlocks[1].Block);

			newBlocks[1].AppendInstruction(X86.Mov, v1, op1H);
			newBlocks[1].AppendInstruction(X86.Mov, op0L, op1L);
			newBlocks[1].AppendInstruction(X86.Shrd, op0L, op0L, v1, ecx);
			newBlocks[1].AppendInstruction(X86.Shr, v1, v1, ecx);
			if (!op0H.IsConstantZero)
				newBlocks[1].AppendInstruction(X86.Mov, op0H, v1);
			newBlocks[1].AppendInstruction(X86.Jmp, nextBlock.Block);

			newBlocks[2].AppendInstruction(X86.Mov, op0L, op1H);
			if (!op0H.IsConstantZero)
				newBlocks[2].AppendInstruction(X86.Mov, op0H, ConstantZero);
			newBlocks[2].AppendInstruction(X86.And, ecx, ecx, Operand.CreateConstant(TypeSystem, 0x1F));
			newBlocks[2].AppendInstruction(X86.Sar, op0L, op0L, ecx);
			newBlocks[2].AppendInstruction(X86.Jmp, nextBlock.Block);

			newBlocks[3].AppendInstruction(X86.Mov, op0L, op0H);
			if (!op0H.IsConstantZero)
				newBlocks[3].AppendInstruction(X86.Mov, op0H, ConstantZero);
			newBlocks[3].AppendInstruction(X86.Jmp, nextBlock.Block);
		}

		/// <summary>
		/// Expands the signed move instruction for 64-bits.
		/// </summary>
		/// <param name="context">The context.</param>
		private void ExpandSignedMove(Context context)
		{
			Operand op0 = context.Result;
			Operand op1 = context.Operand1;
			Debug.Assert(op0 != null, @"I8 not in a memory operand!");

			Operand op0L, op0H;
			SplitLongOperand(op0, out op0L, out op0H);

			if (op1.IsBoolean)
			{
				context.SetInstruction(X86.Movzx, InstructionSize.Size8, op0L, op1);
				context.AppendInstruction(X86.Mov, op0H, ConstantZero);
			}
			else if (op1.IsI1 || op1.IsI2)
			{
				Operand v1 = AllocateVirtualRegister(TypeSystem.BuiltIn.I4);
				Operand v2 = AllocateVirtualRegister(TypeSystem.BuiltIn.U4);
				Operand v3 = AllocateVirtualRegister(TypeSystem.BuiltIn.U4);

				InstructionSize size = op1.IsI1 ? InstructionSize.Size8 : InstructionSize.Size16;

				context.SetInstruction(X86.Movsx, size, v1, op1);
				context.AppendInstruction2(X86.Cdq, v3, v2, v1);
				context.AppendInstruction(X86.Mov, op0L, v2);
				context.AppendInstruction(X86.Mov, op0H, v3);
			}
			else if (op1.IsI4 || op1.IsU4 || op1.IsPointer || op1.IsI || op1.IsU)
			{
				Operand v1 = AllocateVirtualRegister(TypeSystem.BuiltIn.I4);
				Operand v2 = AllocateVirtualRegister(TypeSystem.BuiltIn.U4);
				Operand v3 = AllocateVirtualRegister(TypeSystem.BuiltIn.U4);

				context.SetInstruction(X86.Mov, v1, op1);
				context.AppendInstruction2(X86.Cdq, v3, v2, v1);
				context.AppendInstruction(X86.Mov, op0L, v2);
				context.AppendInstruction(X86.Mov, op0H, v3);
			}
			else if (op1.IsI8)
			{
				context.SetInstruction(X86.Mov, op0, op1);
			}
			else if (op1.IsU1 || op1.IsU2)
			{
				Operand v1 = AllocateVirtualRegister(TypeSystem.BuiltIn.U4);
				Operand v2 = AllocateVirtualRegister(TypeSystem.BuiltIn.U4);
				Operand v3 = AllocateVirtualRegister(TypeSystem.BuiltIn.U4);

				InstructionSize size = op1.IsI1 ? InstructionSize.Size8 : InstructionSize.Size16;

				context.SetInstruction(X86.Movzx, size, v1, op1);
				context.AppendInstruction2(X86.Cdq, v3, v2, v1);
				context.AppendInstruction(X86.Mov, op0L, v2);
				context.AppendInstruction(X86.Mov, op0H, ConstantZero);
			}
			else
			{
				throw new InvalidCompilerException();
			}
		}

		/// <summary>
		/// Expands the store instruction for 64-bits.
		/// </summary>
		/// <param name="context">The context.</param>
		private void ExpandStore(Context context)
		{
			Operand address = context.Operand1;
			Operand offset = context.Operand2;

			Operand op3L, op3H;
			SplitLongOperand(context.Operand3, out op3L, out op3H);

			context.SetInstruction(X86.MovStore, InstructionSize.Size32, null, address, offset, op3L);

			if (offset.IsResolvedConstant)
			{
				var offset2 = offset.IsConstantZero ? ConstantFour : Operand.CreateConstant(TypeSystem, offset.Offset + NativePointerSize);
				context.AppendInstruction(X86.MovStore, InstructionSize.Size32, null, address, offset2, op3H);
				return;
			}

			Operand op2L, op2H;
			SplitLongOperand(offset, out op2L, out op2H);

			Operand v1 = AllocateVirtualRegister(TypeSystem.BuiltIn.U4);

			context.AppendInstruction(X86.Add, InstructionSize.Size32, v1, op2L, ConstantFour);
			context.AppendInstruction(X86.MovStore, InstructionSize.Size32, null, address, v1, op3H);
		}

		/// <summary>
		/// Expands the store instruction for 64-bits.
		/// </summary>
		/// <param name="context">The context.</param>
		private void ExpandStoreParameter(Context context)
		{
			Operand op0L, op0H;
			SplitLongOperand(context.Operand1, out op0L, out op0H);

			Operand op1L, op1H;
			SplitLongOperand(context.Operand2, out op1L, out op1H);

			context.SetInstruction(X86.MovStore, InstructionSize.Size32, null, StackFrame, op0L, op1L);
			context.AppendInstruction(X86.MovStore, InstructionSize.Size32, null, StackFrame, op0H, op1H);
		}

		/// <summary>
		/// Expands the sub instruction for 64-bit operands.
		/// </summary>
		/// <param name="context">The context.</param>
		private void ExpandSub(Context context)
		{
			Operand op0H, op1H, op2H, op0L, op1L, op2L;
			SplitLongOperand(context.Result, out op0L, out op0H);
			SplitLongOperand(context.Operand1, out op1L, out op1H);
			SplitLongOperand(context.Operand2, out op2L, out op2H);

			Operand v1 = AllocateVirtualRegister(TypeSystem.BuiltIn.I4);
			Operand v2 = AllocateVirtualRegister(TypeSystem.BuiltIn.U4);

			context.SetInstruction(X86.Mov, v2, op1L);
			context.AppendInstruction(X86.Sub, v2, v2, op2L);
			context.AppendInstruction(X86.Mov, op0L, v2);
			context.AppendInstruction(X86.Mov, v1, op1H);
			context.AppendInstruction(X86.Sbb, v1, v1, op2H);
			if (!op0H.IsConstantZero)
				context.AppendInstruction(X86.Mov, op0H, v1);
		}

		/// <summary>
		/// Expands the udiv instruction.
		/// </summary>
		/// <param name="context">The context.</param>
		private void ExpandUDiv(Context context)
		{
			Operand op0H, op1H, op2H, op0L, op1L, op2L;
			var result = context.Result;
			var op1 = context.Operand1;
			var op2 = context.Operand2;

			SplitLongOperand(result, out op0L, out op0H);
			SplitLongOperand(op1, out op1L, out op1H);
			SplitLongOperand(op2, out op2L, out op2H);

			ReplaceWithDivisionCall(context, "udiv64");
			context.Result = result;
			context.Operand2 = op1;
			context.Operand3 = op2;
			context.OperandCount = 3;
			context.ResultCount = 1;
		}

		/// <summary>
		/// Expands the unsigned move instruction for 64-bits.
		/// </summary>
		/// <param name="context">The context.</param>
		private void ExpandUnsignedMove(Context context)
		{
			Operand op0 = context.Result;
			Operand op1 = context.Operand1;

			Operand op0L, op0H, op1L, op1H;
			SplitLongOperand(op0, out op0L, out op0H);
			SplitLongOperand(op1, out op1L, out op1H);

			if (op1.IsInt)
			{
				context.SetInstruction(X86.Mov, op0L, op1L);
				context.AppendInstruction(X86.Mov, op0H, ConstantZero);
			}
			else if (op1.IsBoolean || op1.IsChar || op1.IsU1 || op1.IsU2)
			{
				InstructionSize size = (op1.IsU1 || op1.IsBoolean) ? InstructionSize.Size8 : InstructionSize.Size16;

				context.SetInstruction(X86.Movzx, size, op0L, op1L);
				context.AppendInstruction(X86.Mov, op0H, ConstantZero);
			}
			else if (op1.IsU8)
			{
				context.SetInstruction(X86.Mov, op0L, op1L);
				context.AppendInstruction(X86.Mov, op0H, op1H);
			}
			else
			{
				throw new NotSupportedException();
			}
		}

		/// <summary>
		/// Expands the urem instruction.
		/// </summary>
		/// <param name="context">The context.</param>
		private void ExpandURem(Context context)
		{
			Operand op0H, op1H, op2H, op0L, op1L, op2L;
			var result = context.Result;
			var op1 = context.Operand1;
			var op2 = context.Operand2;

			SplitLongOperand(result, out op0L, out op0H);
			SplitLongOperand(op1, out op1L, out op1H);
			SplitLongOperand(op2, out op2L, out op2H);

			ReplaceWithDivisionCall(context, "umod64");
			context.Result = result;
			context.Operand2 = op1;
			context.Operand3 = op2;
			context.OperandCount = 3;
			context.ResultCount = 1;
		}

		/// <summary>
		/// Expands the neg instruction for 64-bits.
		/// </summary>
		/// <param name="context">The context.</param>
		private void ExpandXor(Context context)
		{
			Operand op0H, op1H, op2H, op0L, op1L, op2L;
			SplitLongOperand(context.Result, out op0L, out op0H);
			SplitLongOperand(context.Operand1, out op1L, out op1H);
			SplitLongOperand(context.Operand2, out op2L, out op2H);

			context.SetInstruction(X86.Mov, op0H, op1H);
			context.AppendInstruction(X86.Mov, op0L, op1L);
			context.AppendInstruction(X86.Xor, op0H, op0H, op2H);
			context.AppendInstruction(X86.Xor, op0L, op0L, op2L);
		}

		private void ReplaceWithDivisionCall(Context context, string methodName)
		{
			var type = TypeSystem.GetTypeByName("Mosa.Runtime.x86", "Division");

			Debug.Assert(type != null, "Cannot find type: Mosa.Runtime.x86.Division type");

			var method = type.FindMethodByName(methodName);

			Debug.Assert(method != null, "Cannot find method: " + methodName);

			context.ReplaceInstructionOnly(IRInstruction.Call);
			context.SetOperand(0, Operand.CreateSymbolFromMethod(TypeSystem, method));
			context.OperandCount = 1;
			context.InvokeMethod = method;
		}

		private void SplitLongOperand(Operand operand, out Operand operandLow, out Operand operandHigh)
		{
			SplitLongOperand(MethodCompiler, operand, out operandLow, out operandHigh);
		}

		#endregion Utility Methods
	}
}
