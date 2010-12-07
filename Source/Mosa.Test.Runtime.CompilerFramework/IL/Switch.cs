﻿/*
 * (c) 2008 MOSA - The Managed Operating System Alliance
 *
 * Licensed under the terms of the New BSD License.
 *
 * Authors:
 *  Alex Lyman <mail.alex.lyman@gmail.com>
 *  Simon Wollwage (rootnode) <kintaro@think-in-co.de>
 *  
 */

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using MbUnit.Framework;

namespace Mosa.Test.Runtime.CompilerFramework.IL
{
	
	[TestFixture]
	public class Switch : CodeDomTestRunner
	{
		
		[Row(1)]
		[Row(23)]
		[Row(-1)]
		[Row(0)]
		// And reverse
		[Row(2)]
		[Row(-2)]
		[Test, Author("rootnode", "rootnode@mosa-project.org")]
		public void SwitchI1(sbyte a)
		{
			CodeSource = @"static class Test { 
				static sbyte SwitchI1(sbyte expect, sbyte a) { return Switch_Target(a); } 
				static sbyte Switch_Target(sbyte a)
				{
					switch(a)
					{
						case 0:
							return 0;
							break;
						case 1:
							return 1;
							break;
						case -1:
							return -1;
							break;
						case 2:
							return 2;
							break;
						case -2:
							return -2;
							break;
						case 23:
							return 23;
							break;
						case sbyte.MinValue:
							return sbyte.MinValue;
							break;
						case sbyte.MaxValue:
							return sbyte.MaxValue;
							break;
						default:
							return 42;
							break;
					}
				}
			}" + Code.AllTestCode;

			Assert.AreEqual(a, Run<sbyte>("", "Test", "SwitchI1", a, a));
		}

		[Row(1)]
		[Row(23)]
		[Row(0)]
		// And reverse
		[Row(2)]
		[Test, Author("rootnode", "rootnode@mosa-project.org")]
		public void SwitchU1(byte a)
		{
			CodeSource = @"static class Test { 
				static bool SwitchU1(byte expect, byte a) { return expect == Switch_Target(a); } 
				static byte Switch_Target(byte a)
				{
					switch(a)
					{
						case 0:
							return 0;
							break;
						case 1:
							return 1;
							break;
						case 2:
							return 2;
							break;
						case 23:
							return 23;
							break;
						case byte.MaxValue:
							return byte.MaxValue;
							break;
						default:
							return 42;
							break;
					}
				}
			}" + Code.AllTestCode;
			Assert.IsTrue(Run<bool>("", "Test", "SwitchU1", a, a));
		}

		[Row(1)]
		[Row(23)]
		[Row(-1)]
		[Row(0)]
		// And reverse
		[Row(2)]
		[Row(-2)]
		// (MinValue, X) Cases
		[Row(short.MinValue)]
		// (MaxValue, X) Cases
		[Row(short.MaxValue)]
		[Test, Author("rootnode", "rootnode@mosa-project.org")]
		public void SwitchI2(short a)
		{
			CodeSource = @"static class Test { 
				static bool SwitchI2(short expect, short a) { return expect == Switch_Target(a); } 
				static short Switch_Target(short a)
				{
					switch(a)
					{
						case 0:
							return 0;
							break;
						case 1:
							return 1;
							break;
						case -1:
							return -1;
							break;
						case 2:
							return 2;
							break;
						case -2:
							return -2;
							break;
						case 23:
							return 23;
							break;
						case short.MinValue:
							return short.MinValue;
							break;
						case short.MaxValue:
							return short.MaxValue;
							break;
						default:
							return 42;
							break;
					}
				}
			}" + Code.AllTestCode;
			Assert.IsTrue(Run<bool>("", "Test", "SwitchI2", a, a));
		}

		[Row(1)]
		[Row(23)]
		[Row(0)]
		// And reverse
		[Row(2)]
		// (MinValue, X) Cases
		[Row(ushort.MinValue)]
		// (MaxValue, X) Cases
		[Row(ushort.MaxValue)]
		[Test, Author("rootnode", "rootnode@mosa-project.org")]
		public void SwitchU2(ushort a)
		{
			CodeSource = @"static class Test { 
				static bool SwitchU2(ushort expect, ushort a) { return expect == Switch_Target(a); } 
				static ushort Switch_Target(ushort a)
				{
					switch(a)
					{
						case 0:
							return 0;
							break;
						case 1:
							return 1;
							break;
						case 2:
							return 2;
							break;
						case 23:
							return 23;
							break;
						case ushort.MaxValue:
							return ushort.MaxValue;
							break;
						default:
							return 42;
							break;
					}
				}
			}" + Code.AllTestCode;
			Assert.IsTrue(Run<bool>("", "Test", "SwitchU2", a, a));
		}

		[Row(1)]
		[Row(23)]
		[Row(-1)]
		[Row(0)]
		// And reverse
		[Row(2)]
		[Row(-2)]
		// (MinValue, X) Cases
		[Row(int.MinValue)]
		// (MaxValue, X) Cases
		[Row(int.MaxValue)]
		[Test, Author("rootnode", "rootnode@mosa-project.org")]
		public void SwitchI4(int a)
		{
			CodeSource = @"static class Test { 
				static bool SwitchI4(int expect, int a) { return expect == Switch_Target(a); } 
				static int Switch_Target(int a)
				{
					switch(a)
					{
						case 0:
							return 0;
							break;
						case 1:
							return 1;
							break;
						case -1:
							return -1;
							break;
						case 2:
							return 2;
							break;
						case -2:
							return -2;
							break;
						case 23:
							return 23;
							break;
						case int.MinValue:
							return int.MinValue;
							break;
						case int.MaxValue:
							return int.MaxValue;
							break;
						default:
							return 42;
							break;
					}
				}
			}" + Code.AllTestCode;
			Assert.IsTrue(Run<bool>("", "Test", "SwitchI4", a, a));
		}

		[Row(1)]
		[Row(23)]
		[Row(0)]
		// And reverse
		[Row(2)]
		// (MinValue, X) Cases
		[Row(uint.MinValue)]
		// (MaxValue, X) Cases
		[Row(uint.MaxValue)]
		[Test, Author("rootnode", "rootnode@mosa-project.org")]
		public void SwitchU4(uint a)
		{
			CodeSource = @"static class Test { 
				static bool SwitchU4(uint expect, uint a) { return expect == Switch_Target(a); } 
				static uint Switch_Target(uint a)
				{
					switch(a)
					{
						case 0:
							return 0;
							break;
						case 1:
							return 1;
							break;
						case 2:
							return 2;
							break;
						case 23:
							return 23;
							break;
						case uint.MaxValue:
							return uint.MaxValue;
							break;
						default:
							return 42;
							break;
					}
				}
			}" + Code.AllTestCode;
			Assert.IsTrue(Run<bool>("", "Test", "SwitchU4", a, a));
		}

		[Row(1)]
		[Row(23)]
		[Row(-1)]
		[Row(0)]
		// And reverse
		[Row(2)]
		[Row(-2)]
		// (MinValue, X) Cases
		[Row(long.MinValue)]
		// (MaxValue, X) Cases
		[Row(long.MaxValue)]
		[Test, Author("rootnode", "rootnode@mosa-project.org")]
		public void SwitchI8(long a)
		{
			CodeSource = @"static class Test { 
				static long SwitchI8(long expect, long a) { return Switch_Target(a); } 
				static long Switch_Target(long a)
				{
					switch(a)
					{
						case 0:
							return 0;
							break;
						case 1:
							return 1;
							break;
						case -1:
							return -1;
							break;
						case 2:
							return 2;
							break;
						case -2:
							return -2;
							break;
						case 23:
							return 23;
							break;
						case long.MinValue:
							return long.MinValue;
							break;
						case long.MaxValue:
							return long.MaxValue;
							break;
						default:
							return 42;
							break;
					}
				}
			}" + Code.AllTestCode;
			Assert.AreEqual(a, (long)Run<long>("", "Test", "SwitchI8", a, a));
		}

		[Row(1)]
		[Row(23)]
		[Row(0)]
		// And reverse
		[Row(2)]
		// (MinValue, X) Cases
		[Row(ulong.MinValue)]
		// (MaxValue, X) Cases
		[Row(ulong.MaxValue)]
		[Test, Author("rootnode", "rootnode@mosa-project.org")]
		public void SwitchU8(ulong a)
		{
			CodeSource = @"static class Test { 
				static bool SwitchU8(ulong expect, ulong a) { return expect == Switch_Target(a); } 
				static ulong Switch_Target(ulong a)
				{
					switch(a)
					{
						case 0:
							return 0;
							break;
						case 1:
							return 1;
							break;
						case 2:
							return 2;
							break;
						case 23:
							return 23;
							break;
						case ulong.MaxValue:
							return ulong.MaxValue;
							break;
						default:
							return 42;
							break;
					}
				}
			}" + Code.AllTestCode;
			Assert.IsTrue(Run<bool>("", "Test", "SwitchU8", a, a));
		}
	}
}