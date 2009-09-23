/*
 * (c) 2008 MOSA - The Managed Operating System Alliance
 *
 * Licensed under the terms of the New BSD License.
 *
 * Authors:
 *  Michael Ruck (<mailto:sharpos@michaelruck.de>)
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Mosa.Runtime.Vm;
using Mosa.Runtime.Metadata.Signatures;

namespace Mosa.Runtime.CompilerFramework
{
    /// <summary>
    /// Transforms the intermediate representation into a minimal static single assignment form.
    /// </summary>
    /// <remarks>
    /// The minimal form only inserts the really required PHI functions in order to reduce the 
    /// number of live registers used by register allocation.
    /// </remarks>
    public sealed class EnterSSA : IMethodCompilerStage
    {
        #region Tracing

        /// <summary>
        /// Controls the tracing of the <see cref="EnterSSA"/> method compiler stage.
        /// </summary>
        /// <remarks>
        /// The trace output happens at the TraceLevel.Info level.
        /// </remarks>
        public static readonly TraceSwitch TRACING = new TraceSwitch(@"Mosa.Runtime.CompilerFramework.EnterSSA", @"Controls tracing of the Mosa.Runtime.CompilerFramework.EnterSSA compilation stage.");

        #endregion // Tracing

        #region Types

        struct WorkItem
        {
            public WorkItem(BasicBlock block, BasicBlock caller, IDictionary<StackOperand, StackOperand> liveIn)
            {
                this.block = block;
                this.caller = caller;
                this.liveIn = liveIn;
            }

            public BasicBlock block;
            public BasicBlock caller;
            public IDictionary<StackOperand, StackOperand> liveIn;
        }

        #endregion // Types

        #region Data members

        /// <summary>
        /// The architecture to create the PhiInstructions.
        /// </summary>
        private IArchitecture _architecture;

        /// <summary>
        /// Holds the currently running method compiler.
        /// </summary>
        private IMethodCompiler _compiler;

        /// <summary>
        /// Holds the dominance frontier Blocks of the stage.
        /// </summary>
        private BasicBlock[] _dominanceFrontierBlocks;

        /// <summary>
        /// Holds the dominance provider.
        /// </summary>
        private IDominanceProvider _dominanceProvider;

        /// <summary>
        /// Holds the operand definitions after each block (array of block count length).
        /// </summary>
        private IDictionary<StackOperand, StackOperand>[] _liveness;

        /// <summary>
        /// Holds the version of the next SSA variable. (We use a single version number for all of them)
        /// </summary>
        private int _ssaVersion;

        #endregion // Data members

        #region IMethodCompilerStage Members

        /// <summary>
        /// Retrieves the name of the compilation stage.
        /// </summary>
        /// <value>The name of the compilation stage.</value>
        public string Name
        {
            get { return @"EnterSSA"; }
        }

        /// <summary>
        /// Performs stage specific processing on the compiler context.
        /// </summary>
        /// <param name="compiler">The compiler context to perform processing in.</param>
        public void Run(IMethodCompiler compiler)
        {
            // Retrieve the basic block provider
            IBasicBlockProvider blockProvider = (IBasicBlockProvider)compiler.GetPreviousStage(typeof(IBasicBlockProvider));
            if (null == blockProvider)
                throw new InvalidOperationException(@"SSA Conversion requires basic Blocks.");
            _dominanceProvider = (IDominanceProvider)compiler.GetPreviousStage(typeof(IDominanceProvider));
            Debug.Assert(null != _dominanceProvider, @"SSA Conversion requires a dominance provider.");
            if (null == _dominanceProvider)
                throw new InvalidOperationException(@"SSA Conversion requires a dominance provider.");
            _architecture = compiler.Architecture;
            _compiler = compiler;

            List<BasicBlock> blocks = blockProvider.Blocks;

            // Allocate space for live outs
            _liveness = new IDictionary<StackOperand,StackOperand>[blocks.Count];
            // Retrieve the dominance frontier Blocks
            _dominanceFrontierBlocks = _dominanceProvider.GetDominanceFrontier();

            // Add ref/out parameters to the epilogue block to have uses there...
            AddPhiFunctionsForOutParameters(compiler, blockProvider);

            // Transformation worklist 
            Queue<WorkItem> workList = new Queue<WorkItem>();

            /* Move parameter operands into the dictionary as version 0,
             * because they are live at entry and maybe referenced. Anyways, an
             * assignment to a parameter is also SSA related.
             */
            IDictionary<StackOperand, StackOperand> liveIn = new Dictionary<StackOperand, StackOperand>(s_comparer);
            int i = 0;
            if (compiler.Method.Signature.HasThis)
            {
                StackOperand param = (StackOperand)compiler.GetParameterOperand(0);
                liveIn.Add(param, param);
                i++;
            }
            for (int j = 0; j < compiler.Method.Parameters.Count; j++)
            {
                StackOperand param = (StackOperand)compiler.GetParameterOperand(i+j);
                liveIn.Add(param, param);
            }
            
            // Start with the very first block
            workList.Enqueue(new WorkItem(blocks[0], null, liveIn));

            // Iterate until the worklist is empty
            while (0 != workList.Count)
            {
                // Remove the block From the queue
                WorkItem workItem = workList.Dequeue();

                // Transform the block
                BasicBlock block = workItem.block;
                bool schedule = TransformToSsaForm(block, workItem.caller, workItem.liveIn, out liveIn);
                _liveness[block.Index] = liveIn;

                if (schedule)
                {
                    // Add all branch targets to the work list
                    foreach (BasicBlock next in block.NextBlocks)
                    {
                        // Only follow backward branches, if we've redefined a variable
                        // this may force us to reinsert a PHI function in a block we
                        // already have completed processing on.
                        workList.Enqueue(new WorkItem(next, block, liveIn));
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pipeline"></param>
        public void AddToPipeline(CompilerPipeline<IMethodCompilerStage> pipeline)
        {
            pipeline.InsertAfter<DominanceCalculationStage>(this);
        }

        /// <summary>
        /// Adds PHI functions for all ref/out parameters of the method being compiled.
        /// </summary>
        /// <param name="compiler">The method compiler.</param>
        /// <param name="blockProvider">The block provider.</param>
        private void AddPhiFunctionsForOutParameters(IMethodCompiler compiler, IBasicBlockProvider blockProvider)
        {
            Dictionary<StackOperand, StackOperand> liveIn = null;

            // Retrieve the well known epilogue block
            BasicBlock epilogue = blockProvider.FromLabel(Int32.MaxValue);
            Debug.Assert(null != epilogue, @"Method doesn't have epilogue block?");

            // Iterate all parameter definitions
            foreach (RuntimeParameter rp in compiler.Method.Parameters)
            {
                // Retrieve the stack operand for the parameter
                StackOperand paramOp = (StackOperand)compiler.GetParameterOperand(rp.Position-1);

                // Only add a PHI if the runtime parameter is out or ref...
                if (rp.IsOut || (paramOp.Type is RefSigType || paramOp.Type is PtrSigType))
                {
                    epilogue.Instructions.Insert(0, new IR.PhiInstruction(paramOp));

                    if (null == liveIn)
                        liveIn = new Dictionary<StackOperand, StackOperand>();

                    liveIn.Add(paramOp, paramOp);
                }
            }

            // Save the in versions to force a merge later
            if (null != liveIn)
                _liveness[epilogue.Index] = liveIn;
        }

        private bool TransformToSsaForm(BasicBlock block, BasicBlock caller, IDictionary<StackOperand, StackOperand> liveIn, out IDictionary<StackOperand, StackOperand> liveOut)
        {
            // Is this another entry for this block?
            IDictionary<StackOperand, StackOperand> liveOutPrev = _liveness[block.Index];
            if (null != liveOutPrev)
            {
                // FIXME: Merge PHIs with new incoming variables, add new incoming variables to out set
                // and schedule the remaining out nodes/quit
                MergePhiInstructions(block, caller, liveIn);                
                liveOut = liveOutPrev;

                return false;
            }
            // Is this a dominance frontier block?
            if (-1 != Array.IndexOf(_dominanceFrontierBlocks, block))
            {
                InsertPhiInstructions(block, caller, liveIn);
            }

            // Create a new live out dictionary
            if (null != liveIn)
                liveOut = new Dictionary<StackOperand, StackOperand>(liveIn, s_comparer);
            else
                liveOut = new Dictionary<StackOperand, StackOperand>(s_comparer);

            // Iterate each instruction in the block
            foreach (LegacyInstruction instruction in block.Instructions)
            {
                // Replace all operands with their current SSA version
                UpdateUses(instruction, liveOut);

                // Is this an instruction we ignore?
                if (false == instruction.Ignore)
                {
                    RenameStackOperands(instruction, liveOut);
                }
            }

            return true;
        }

        private void MergePhiInstructions(BasicBlock block, BasicBlock caller, IDictionary<StackOperand, StackOperand> liveIn)
        {
            foreach (LegacyInstruction instruction in block.Instructions)
            {
                IR.PhiInstruction phi = instruction as IR.PhiInstruction;
                if (null != phi && liveIn.ContainsKey(phi.Result))
                {
                    StackOperand value = liveIn[phi.Result];
                    if (false == phi.Contains(value) && phi.Result.Version != value.Version)
                        phi.AddValue(caller, value);
                }
            }
        }

        private void InsertPhiInstructions(BasicBlock block, BasicBlock caller, IDictionary<StackOperand, StackOperand> liveIn)
        {
            // Iterate all incoming variables
            int pos = 0;
            foreach (StackOperand key in liveIn.Keys)
            {
                IR.PhiInstruction phi = (IR.PhiInstruction)_architecture.CreateInstruction(typeof(IR.PhiInstruction), key);
                phi.AddValue(caller, key);
                block.Instructions.Insert(pos++, phi);
            }
        }

        private void RenameStackOperands(LegacyInstruction instruction, IDictionary<StackOperand, StackOperand> liveOut)
        {
            // Create new SSA variables for newly defined operands
            Operand[] ops = instruction.Results;
            for (int opIdx = 0; opIdx < ops.Length; opIdx++)
            {
                // Is this a stack operand?
                StackOperand op = ops[opIdx] as StackOperand, ssa;
                if (null != op)
                {
                    if (false == liveOut.TryGetValue(op, out ssa))
                        ssa = op;

                    ssa = RedefineOperand(ssa);
                    liveOut[op] = ssa;
                    instruction.SetResult(opIdx, ssa);

                    if (TRACING.TraceInfo)
                        Trace.WriteLine(String.Format("\tStore to {0} redefined as {1}", op, ssa));
                }
            }
        }

        private void UpdateUses(LegacyInstruction instruction, IDictionary<StackOperand, StackOperand> liveOut)
        {
            Operand[] ops = instruction.Operands;
            for (int opIdx = 0; opIdx < ops.Length; opIdx++)
            {
                // Is this a stack operand?
                StackOperand op = ops[opIdx] as StackOperand, ssa;
                if (null != op)
                {
                    // Determine the most recent version
                    Debug.Assert(liveOut.TryGetValue(op, out ssa), @"Stack operand not in live variable list.");
                    ssa = liveOut[op];

                    // Replace the use with the most recent version
                    instruction.SetOperand(opIdx, ssa);

                    if (TRACING.TraceInfo)
                        Trace.WriteLine(String.Format(@"\tUse {0} has been replaced with {1}", op, ssa));
                }
            }
        }



        class StackOperandComparer : IEqualityComparer<StackOperand>
        {
            #region IEqualityComparer<StackOperand> Members

            bool IEqualityComparer<StackOperand>.Equals(StackOperand x, StackOperand y)
            {
                return (x.Offset.ToInt32() == y.Offset.ToInt32());
            }

            int IEqualityComparer<StackOperand>.GetHashCode(StackOperand obj)
            {
                return obj.Offset.ToInt32();
            }

            #endregion // IEqualityComparer<StackOperand> Members
        }

        private static readonly IEqualityComparer<StackOperand> s_comparer = new StackOperandComparer();

        /// <summary>
        /// Redefines a <see cref="StackOperand"/> with a new SSA version.
        /// </summary>
        /// <param name="cur">The StackOperand to redefine.</param>
        /// <returns>A new StackOperand.</returns>
        private StackOperand RedefineOperand(StackOperand cur)
        {
            string name = cur.Name;
            if (0 == cur.Version)
                name = String.Format(@"T_{0}", name);
            StackOperand op = _compiler.CreateTemporary(cur.Type) as StackOperand;
            //StackOperand op = new LocalVariableOperand(cur.Base, name, idx, cur.Type);
            op.Version = ++_ssaVersion;
            return op;
        }

        #endregion // IMethodCompilerStage Members
    }
}
