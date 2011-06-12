﻿/*
 * (c) 2008 MOSA - The Managed Operating System Alliance
 *
 * Licensed under the terms of the New BSD License.
 *
 * Authors:
 *  Phil Garcia (tgiphil) <phil@thinkedge.com>
 */

using System;
using System.Collections.Generic;
using System.Text;

using Mosa.Runtime.TypeSystem;

namespace Mosa.Runtime.InternalLog
{
	public enum CompilerEvent { CompilingMethod, CompilingType, Linking, AssemblyStageStart, AssemblyStageEnd, DebugInfo, SchedulingType, SchedulingMethod, Error, Warning };

	public static class CompilerEventExtension
	{
		public static string ToText(this CompilerEvent stage)
		{
			switch (stage)
			{
				case CompilerEvent.CompilingMethod: return "Compiling Method";
				case CompilerEvent.CompilingType: return "Compiling Type";
				case CompilerEvent.SchedulingType: return "Scheduling Type";
				case CompilerEvent.SchedulingMethod: return "Scheduling Method";
				case CompilerEvent.Linking: return "Linking";
				case CompilerEvent.DebugInfo: return "DebugInfo";
				case CompilerEvent.AssemblyStageStart: return "Assembly Stage Started";
				case CompilerEvent.AssemblyStageEnd: return "Assembly Stage Ended";
				case CompilerEvent.Error: return "Error";
				case CompilerEvent.Warning: return "Warning";
				default: return stage.ToString();
			}
		}
	}
}
