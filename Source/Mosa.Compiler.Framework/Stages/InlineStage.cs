﻿// Copyright (c) MOSA Project. Licensed under the New BSD License.

using Mosa.Compiler.Common;
using Mosa.Compiler.Framework.IR;
using System.Collections.Generic;
using System.Diagnostics;

namespace Mosa.Compiler.Framework.Stages
{
	/// <summary>
	///
	/// </summary>
	public class InlineStage : BaseMethodCompilerStage
	{
		protected override void Run()
		{
			if (HasProtectedRegions)
				return;

			if (MethodCompiler.Method.IsLinkerGenerated && MethodCompiler.Method.Name == TypeInitializerSchedulerStage.TypeInitializerName)
				return;

			MethodData.CompileCount++;
			MethodData.Calls.Clear();

			var nodes = new List<InstructionNode>();

			// find all call sites
			foreach (var block in BasicBlocks)
			{
				for (var node = block.First.Next; !node.IsBlockEndInstruction; node = node.Next)
				{
					if (node.IsEmpty)
						continue;

					if (node.Instruction != IRInstruction.Call)
						continue;

					nodes.Add(node);

					if (node.InvokeMethod == null)
						continue;

					Debug.Assert(node.InvokeMethod != null);

					var invoked = MethodCompiler.Compiler.CompilerData.GetCompilerMethodData(node.InvokeMethod);

					MethodData.Calls.AddIfNew(node.InvokeMethod);

					invoked.AddCalledBy(MethodCompiler.Method);
				}
			}

			if (nodes.Count == 0)
				return;

			var trace = CreateTraceLog("Inlined");

			foreach (var node in nodes)
			{
				if (node.InvokeMethod == null)
					continue;

				Debug.Assert(node.InvokeMethod != null);

				var invoked = MethodCompiler.Compiler.CompilerData.GetCompilerMethodData(node.InvokeMethod);

				if (!invoked.CanInline)
					continue;

				// don't inline self
				if (invoked.Method == MethodCompiler.Method)
					continue;

				var blocks = invoked.BasicBlocks;

				if (blocks == null)
					continue;

				if (trace.Active)
					trace.Log(invoked.Method.FullName);

				//System.Diagnostics.Debug.WriteLine(MethodCompiler.Method.FullName);
				//System.Diagnostics.Debug.WriteLine(" * " + invoked.Method.FullName);

				Inline(node, blocks);
			}
		}

		protected void Inline(InstructionNode callNode, BasicBlocks blocks)
		{
			var mapBlocks = new Dictionary<BasicBlock, BasicBlock>(blocks.Count);
			var map = new Dictionary<Operand, Operand>();

			var nextBlock = Split(callNode);

			// create basic blocks
			foreach (var block in blocks)
			{
				var newBlock = CreateNewBlock();
				mapBlocks.Add(block, newBlock);
			}

			// copy instructions
			foreach (var block in blocks)
			{
				var newBlock = mapBlocks[block];

				for (var node = block.First.Next; !node.IsBlockEndInstruction; node = node.Next)
				{
					if (node.IsEmpty)
						continue;

					if (node.Instruction == IRInstruction.Prologue)
						continue;

					if (node.Instruction == IRInstruction.Epilogue)
						continue;

					if (node.Instruction == IRInstruction.Return)
					{
						if (callNode.Result != null)
						{
							var newOp = Map(node.Operand1, map, callNode);
							var moveInsturction = GetMoveInstruction(callNode.Result.Type);

							var moveNode = new InstructionNode(moveInsturction, callNode.Result, newOp);

							newBlock.BeforeLast.Insert(moveNode);
						}
						newBlock.BeforeLast.Insert(new InstructionNode(IRInstruction.Jmp, nextBlock));

						continue;
					}

					var newNode = new InstructionNode(node.Instruction, node.OperandCount, node.ResultCount);
					newNode.Size = node.Size;
					newNode.ConditionCode = node.ConditionCode;

					if (node.BranchTargets != null)
					{
						// copy targets
						foreach (var target in node.BranchTargets)
						{
							newNode.AddBranchTarget(mapBlocks[target]);
						}
					}

					// copy results
					for (int i = 0; i < node.ResultCount; i++)
					{
						var op = node.GetResult(i);

						var newOp = Map(op, map, callNode);

						newNode.SetResult(i, newOp);
					}

					// copy operands
					for (int i = 0; i < node.OperandCount; i++)
					{
						var op = node.GetOperand(i);

						var newOp = Map(op, map, callNode);

						newNode.SetOperand(i, newOp);
					}

					// copy other
					if (node.MosaType != null)
						newNode.MosaType = node.MosaType;
					if (node.MosaField != null)
						newNode.MosaField = node.MosaField;
					if (node.InvokeMethod != null)
						newNode.InvokeMethod = node.InvokeMethod;

					UpdateParameterInstructions(newNode);

					newBlock.BeforeLast.Insert(newNode);
				}
			}

			callNode.SetInstruction(IRInstruction.Jmp, mapBlocks[blocks.PrologueBlock]);
		}

		private static void UpdateParameterInstructions(InstructionNode newNode)
		{
			if (newNode.Instruction == IRInstruction.LoadParameterFloatR4)
			{
				newNode.Instruction = IRInstruction.MoveFloatR4;
			}
			else if (newNode.Instruction == IRInstruction.LoadParameterFloatR8)
			{
				newNode.Instruction = IRInstruction.MoveFloatR8;
			}
			else if (newNode.Instruction == IRInstruction.LoadParameterInteger)
			{
				newNode.Instruction = IRInstruction.MoveInteger;
			}
			else if (newNode.Instruction == IRInstruction.LoadParameterSignExtended)
			{
				newNode.Instruction = IRInstruction.MoveSignExtended;
			}
			else if (newNode.Instruction == IRInstruction.LoadParameterZeroExtended)
			{
				newNode.Instruction = IRInstruction.MoveZeroExtended;
			}
			else if (newNode.Instruction == IRInstruction.StoreParameterInteger)
			{
				newNode.Instruction = IRInstruction.MoveInteger;
				newNode.Result = newNode.Operand1;
				newNode.ResultCount = 1;
				newNode.Operand1 = newNode.Operand2;
				newNode.Operand2 = null;
				newNode.OperandCount = 1;
			}
			else if (newNode.Instruction == IRInstruction.StoreParameterFloatR4)
			{
				newNode.Instruction = IRInstruction.MoveFloatR4;
				newNode.Result = newNode.Operand1;
				newNode.ResultCount = 1;
				newNode.Operand1 = newNode.Operand2;
				newNode.Operand2 = null;
				newNode.OperandCount = 1;
			}
			else if (newNode.Instruction == IRInstruction.StoreParameterFloatR8)
			{
				newNode.Instruction = IRInstruction.MoveFloatR8;
				newNode.Result = newNode.Operand1;
				newNode.ResultCount = 1;
				newNode.Operand1 = newNode.Operand2;
				newNode.Operand2 = null;
				newNode.OperandCount = 1;
			}
			else if (newNode.Instruction == IRInstruction.StoreParameterCompound)
			{
				newNode.Instruction = IRInstruction.MoveCompound;
			}
			else if (newNode.Instruction == IRInstruction.LoadParameterCompound)
			{
				newNode.Instruction = IRInstruction.MoveCompound;
			}
		}

		private Operand Map(Operand operand, Dictionary<Operand, Operand> map, InstructionNode callNode)
		{
			if (operand == null)
				return null;

			Operand mappedOperand;

			if (map.TryGetValue(operand, out mappedOperand))
			{
				return mappedOperand;
			}

			if (operand.IsSymbol)
			{
				if (operand.StringData != null)
				{
					mappedOperand = Operand.CreateStringSymbol(operand.Type.TypeSystem, operand.Name, operand.StringData);
				}
				else if (operand.Method != null)
				{
					mappedOperand = Operand.CreateSymbolFromMethod(operand.Type.TypeSystem, operand.Method);
				}
				else if (operand.Name != null)
				{
					mappedOperand = Operand.CreateManagedSymbol(operand.Type, operand.Name);
				}
			}
			else if (operand.IsParameter)
			{
				mappedOperand = callNode.GetOperand(operand.Index + 1);
			}
			else if (operand.IsStackLocal)
			{
				mappedOperand = MethodCompiler.AddStackLocal(operand.Type, operand.IsPinned);
			}
			else if (operand.IsVirtualRegister)
			{
				mappedOperand = AllocateVirtualRegister(operand.Type);
			}
			else if (operand.IsStaticField)
			{
				mappedOperand = Operand.CreateField(operand.Field);
			}
			else if (operand.IsCPURegister)
			{
				mappedOperand = operand;
			}
			else if (operand.IsConstant)
			{
				mappedOperand = operand;
			}

			Debug.Assert(mappedOperand != null);

			map.Add(operand, mappedOperand);

			return mappedOperand;
		}
	}
}
