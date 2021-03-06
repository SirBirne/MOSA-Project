﻿// Copyright (c) MOSA Project. Licensed under the New BSD License.

using Mosa.Compiler.MosaTypeSystem;
using System;

namespace Mosa.Compiler.Framework.CIL
{
	/// <summary>
	///
	/// </summary>
	public sealed class NegInstruction : UnaryArithmeticInstruction
	{
		#region Data members

		/// <summary>
		/// Holds the typecode validation table from ISO/IEC 23271:2006 (E),
		/// Partition III, §1.5, Table 3.
		/// </summary>
		private static StackTypeCode[] typeCodes = new StackTypeCode[] {
			StackTypeCode.Unknown,
			StackTypeCode.Int32,
			StackTypeCode.Int64,
			StackTypeCode.N,
			StackTypeCode.F,
			StackTypeCode.Unknown,
			StackTypeCode.Unknown,
			StackTypeCode.Unknown
		};

		#endregion Data members

		#region Construction

		/// <summary>
		/// Initializes a new instance of the <see cref="NegInstruction"/> class.
		/// </summary>
		/// <param name="opcode">The opcode.</param>
		public NegInstruction(OpCode opcode)
			: base(opcode)
		{
		}

		#endregion Construction

		#region Methods

		/// <summary>
		/// Validates the instruction operands and creates a matching variable for the result.
		/// </summary>
		/// <param name="ctx"></param>
		/// <param name="compiler">The compiler.</param>
		public override void Resolve(Context ctx, BaseMethodCompiler compiler)
		{
			base.Resolve(ctx, compiler);

			// Validate the operand
			var result = typeCodes[(int)ctx.Operand1.Type.GetStackTypeCode()];
			if (StackTypeCode.Unknown == result)
				throw new InvalidOperationException(@"Invalid operand to Neg instruction [" + result + "]");

			ctx.Result = compiler.CreateVirtualRegister(ctx.Operand1.Type);
		}

		#endregion Methods
	}
}
