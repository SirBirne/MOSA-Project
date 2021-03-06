﻿// Copyright (c) MOSA Project. Licensed under the New BSD License.

using Mosa.Compiler.Framework;
using Mosa.Compiler.Linker;
using Mosa.Compiler.MosaTypeSystem;
using Mosa.Compiler.Pdb;
using Mosa.Compiler.Trace;
using Mosa.Utility.GUI.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Mosa.Tool.Explorer
{
	public partial class Main : Form, ITraceListener
	{
		private CodeForm form = new CodeForm();

		private DateTime compileStartTime;

		private MosaCompiler Compiler = new MosaCompiler();

		private enum CompileStage { Nothing, Loaded, PreCompiled, Compiled };

		private CompileStage Stage = CompileStage.Nothing;

		private StringBuilder compileLog = new StringBuilder();
		private StringBuilder counterLog = new StringBuilder();
		private StringBuilder errorLog = new StringBuilder();
		private StringBuilder exceptionLog = new StringBuilder();

		private MethodStore methodStore = new MethodStore();

		public Main()
		{
			InitializeComponent();

			Compiler.CompilerTrace.TraceListener = this;
			Compiler.CompilerTrace.TraceFilter.Active = true;
			Compiler.CompilerTrace.TraceFilter.ExcludeInternalMethods = false;
			Compiler.CompilerTrace.TraceFilter.MethodMatch = MatchType.Any;
			Compiler.CompilerTrace.TraceFilter.StageMatch = MatchType.Any;

			Compiler.CompilerFactory = delegate { return new ExplorerCompiler(); };
			Compiler.CompilerOptions.LinkerFormatType = LinkerFormatType.Elf32;
		}

		private void SetStatus(string status)
		{
			toolStripStatusLabel.Text = status;
		}

		private void Main_Load(object sender, EventArgs e)
		{
			cbPlatform.SelectedIndex = 0;
			SetStatus("Ready!");
		}

		private void openToolStripMenuItem_Click(object sender, EventArgs e)
		{
			OpenFile();
		}

		private void toolStripButton1_Click(object sender, EventArgs e)
		{
			OpenFile();
		}

		private void OpenFile()
		{
			if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				LoadAssembly(openFileDialog.FileName);
			}
		}

		public void LoadArguments(string[] args)
		{
			for (int i = 0; i < args.Length; i++)
			{
				var arg = args[i];

				switch (arg.ToLower())
				{
					case "-inline": cbEnableInlinedMethods.Checked = true; continue;
					case "-inline-off": cbEnableInlinedMethods.Checked = false; continue;
					case "-threading-off": cbEnableInlinedMethods.Checked = false; continue;
					case "-no-code": cbEnableBinaryCodeGeneration.Checked = false; continue;
					case "-no-ssa": cbEnableSSA.Checked = false; continue;
					case "-no-ir-optimizations": cbEnableOptimizations.Checked = false; continue;
					case "-no-sparse": cbEnableSparseConditionalConstantPropagation.Checked = false; continue;
					default: break;
				}

				if (arg.IndexOf(Path.DirectorySeparatorChar) >= 0)
				{
					LoadAssembly(arg);
				}
				else
				{
					LoadAssembly(Path.Combine(Directory.GetCurrentDirectory(), arg));
				}
			}
		}

		public void LoadAssembly(string filename, string includeDirectory = null)
		{
			LoadAssembly(filename, cbPlatform.Text, includeDirectory);

			UpdateTree();

			Stage = CompileStage.Loaded;

			methodStore.Clear();

			SetStatus("Assemblies Loaded!");
		}

		protected void UpdateTree()
		{
			TypeSystemTree.UpdateTree(treeView, Compiler.TypeSystem, Compiler.TypeLayout, showSizes.Checked);
		}

		private void quitToolStripMenuItem_Click(object sender, EventArgs e)
		{
			Application.Exit();
		}

		private void showTokenValues_Click(object sender, EventArgs e)
		{
			UpdateTree();
		}

		private void showSizes_Click(object sender, EventArgs e)
		{
			UpdateTree();
		}

		private void SubmitTraceEventGUI(CompilerEvent compilerStage, string info, int threadID)
		{
			if (compilerStage != CompilerEvent.DebugInfo)
			{
				SetStatus(compilerStage.ToText() + ": " + info);
				toolStripStatusLabel1.GetCurrentParent().Refresh();
			}
		}

		private object compilerStageLock = new object();

		private void SubmitTraceEvent(CompilerEvent compilerStage, string message, int threadID)
		{
			lock (compilerStageLock)
			{
				if (compilerStage == CompilerEvent.Error)
				{
					errorLog.AppendLine(compilerStage.ToText() + ": " + message);
					compileLog.AppendLine(String.Format("{0:0.00}", (DateTime.Now - compileStartTime).TotalSeconds) + " [" + threadID.ToString() + "] " + compilerStage.ToText() + ": " + message);
				}
				if (compilerStage == CompilerEvent.Exception)
				{
					exceptionLog.AppendLine(compilerStage.ToText() + ": " + message);
					compileLog.AppendLine(String.Format("{0:0.00}", (DateTime.Now - compileStartTime).TotalSeconds) + " [" + threadID.ToString() + "] " + compilerStage.ToText() + ": " + message);
				}
				else if (compilerStage == CompilerEvent.Counter)
				{
					counterLog.AppendLine(compilerStage.ToText() + ": " + message);
				}
				else
				{
					compileLog.AppendLine(String.Format("{0:0.00}", (DateTime.Now - compileStartTime).TotalSeconds) + " [" + threadID.ToString() + "] " + compilerStage.ToText() + ": " + message);
				}
			}
		}

		private void SetCompilerOptions()
		{
			Compiler.CompilerOptions.EnableSSA = cbEnableSSA.Checked;
			Compiler.CompilerOptions.EnableIROptimizations = cbEnableOptimizations.Checked;
			Compiler.CompilerOptions.EnableSparseConditionalConstantPropagation = cbEnableSparseConditionalConstantPropagation.Checked;
			Compiler.CompilerOptions.EmitBinary = cbEnableBinaryCodeGeneration.Checked;
			Compiler.CompilerOptions.EnableInlinedMethods = cbEnableInlinedMethods.Checked;
			Compiler.CompilerOptions.InlinedIRMaximum = 20;
		}

		private void CleanGUI()
		{
			compileLog.Clear();
			errorLog.Clear();
			counterLog.Clear();
			exceptionLog.Clear();

			rbLog.Text = string.Empty;
			rbErrors.Text = string.Empty;
			rbGlobalCounters.Text = string.Empty;
			rbException.Text = string.Empty;
		}

		private void Compile()
		{
			compileStartTime = DateTime.Now;
			SetCompilerOptions();

			if (Stage == CompileStage.PreCompiled)
			{
				Compiler.ScheduleAll();
				Compiler.Compile();
				Compiler.PostCompile();
			}
			else
			{
				CleanGUI();

				methodStore.Clear();

				toolStrip1.Enabled = false;

				ThreadPool.QueueUserWorkItem(new WaitCallback(delegate
					{
						try
						{
							Compiler.Execute(Environment.ProcessorCount);
						}
						finally
						{
							OnCompileCompleted();
						}
					}
				));
			}
		}

		private void OnCompileCompleted()
		{
			MethodInvoker call = delegate ()
			{
				CompileCompleted();
			};

			Invoke(call);
		}

		private void CompileCompleted()
		{
			toolStrip1.Enabled = true;

			Stage = CompileStage.Compiled;

			SetStatus("Compiled!");

			tabControl1.SelectedTab = tbStages;

			rbLog.Text = compileLog.ToString();
			rbErrors.Text = errorLog.ToString();
			rbGlobalCounters.Text = counterLog.ToString();
			rbException.Text = exceptionLog.ToString();

			UpdateTree();
		}

		private static BaseArchitecture GetArchitecture(string platform)
		{
			switch (platform.ToLower())
			{
				case "x86": return Mosa.Platform.x86.Architecture.CreateArchitecture(Mosa.Platform.x86.ArchitectureFeatureFlags.AutoDetect);
				case "armv6": return Mosa.Platform.ARMv6.Architecture.CreateArchitecture(Mosa.Platform.ARMv6.ArchitectureFeatureFlags.AutoDetect);
				default: return Mosa.Platform.x86.Architecture.CreateArchitecture(Mosa.Platform.x86.ArchitectureFeatureFlags.AutoDetect);
			}
		}

		private void nowToolStripMenuItem_Click(object sender, EventArgs e)
		{
			Compile();
		}

		private void PreCompile()
		{
			if (Stage == CompileStage.Loaded)
			{
				SetCompilerOptions();
				Compiler.Initialize();
				Compiler.PreCompile();
				Stage = CompileStage.PreCompiled;
			}
		}

		private T GetCurrentNode<T>() where T : class
		{
			if (treeView.SelectedNode == null)
				return null;

			T node = treeView.SelectedNode as T;

			return node;
		}

		private ViewNode<MosaMethod> GetCurrentNode()
		{
			var node = GetCurrentNode<ViewNode<MosaMethod>>();
			return node;
		}

		private MosaMethod GetCurrentType()
		{
			var node = GetCurrentNode<ViewNode<MosaMethod>>();

			if (node == null)
				return null;
			else
				return node.Type;
		}

		private string GetCurrentStage()
		{
			string stage = cbStages.SelectedItem.ToString();
			return stage;
		}

		private string GetCurrentDebugStage()
		{
			string stage = cbDebugStages.SelectedItem.ToString();
			return stage;
		}

		private string GetCurrentLabel()
		{
			string label = cbLabels.SelectedItem as string;
			return label;
		}

		private List<string> GetCurrentLines()
		{
			var type = GetCurrentType();

			if (type == null)
				return null;

			var methodData = methodStore.GetMethodData(type, false);

			if (methodData == null)
				return null;

			string stage = GetCurrentStage();

			var lines = methodData.InstructionLogs[stage];

			return lines;
		}

		private List<string> GetCurrentDebugLines()
		{
			var type = GetCurrentType();

			if (type == null)
				return null;

			var methodData = methodStore.GetMethodData(type, false);

			if (methodData == null)
				return null;

			string stage = GetCurrentDebugStage();

			var lines = methodData.DebugLogs[stage];

			return lines;
		}

		private void UpdateStages()
		{
			var type = GetCurrentType();

			if (type == null)
				return;

			cbStages.Items.Clear();

			var methodData = methodStore.GetMethodData(type, false);

			if (methodData == null)
				return;

			foreach (string stage in methodData.OrderedStageNames)
			{
				cbStages.Items.Add(stage);
			}

			cbStages.SelectedIndex = 0;
		}

		private void UpdateDebugStages()
		{
			var type = GetCurrentType();

			if (type == null)
				return;

			cbDebugStages.Items.Clear();

			var methodData = methodStore.GetMethodData(type, false);

			if (methodData == null)
				return;

			foreach (string stage in methodData.OrderedDebugStageNames)
			{
				cbDebugStages.Items.Add(stage);
			}

			if (cbDebugStages.Items.Count > 0)
			{
				cbDebugStages.SelectedIndex = 0;
			}
		}

		private void UpdateCounters()
		{
			var type = GetCurrentType();

			if (type == null)
				return;

			rbMethodCounters.Text = string.Empty;

			var methodData = methodStore.GetMethodData(type, false);

			if (methodData == null)
				return;

			rbMethodCounters.Text = CreateText(methodData.CounterData);
		}

		private void UpdateLabels()
		{
			var lines = GetCurrentLines();

			cbLabels.Items.Clear();
			cbLabels.Items.Add("All");

			foreach (var line in lines)
			{
				if (line.StartsWith("Block #"))
				{
					cbLabels.Items.Add(line.Substring(line.IndexOf("L_")));
				}
			}
		}

		private void UpdateResults()
		{
			tbResult.Text = string.Empty;

			var type = GetCurrentType();
			var lines = GetCurrentLines();
			var label = GetCurrentLabel();

			if (type == null)
				return;

			SetStatus(type.FullName);

			if (lines == null)
				return;

			if (string.IsNullOrWhiteSpace(label) || label == "All")
				tbResult.Text = methodStore.GetStageInstructions(lines, string.Empty);
			else
				tbResult.Text = methodStore.GetStageInstructions(lines, label);
		}

		private void UpdateDebugResults()
		{
			rbDebugResult.Text = string.Empty;

			var lines = GetCurrentDebugLines();

			if (lines == null)
				return;

			rbDebugResult.Text = CreateText(lines);
		}

		private void treeView_AfterSelect(object sender, TreeViewEventArgs e)
		{
			tbResult.Text = string.Empty;

			var type = GetCurrentType();

			if (type == null)
				return;

			PreCompile();

			if (!Compiler.CompilationScheduler.IsScheduled(type))
			{
				Compiler.Schedule(type);
				Compiler.Compile();
			}

			UpdateStages();
			UpdateDebugStages();
			UpdateCounters();
		}

		private void cbStages_SelectedIndexChanged(object sender, EventArgs e)
		{
			var previousItemLabel = cbLabels.SelectedItem;

			UpdateLabels();

			if (previousItemLabel != null && cbLabels.Items.Contains(previousItemLabel))
				cbLabels.SelectedItem = previousItemLabel;
			else
				cbLabels.SelectedIndex = 0;

			cbLabels_SelectedIndexChanged(null, null);
		}

		private void cbDebugStages_SelectedIndexChanged(object sender, EventArgs e)
		{
			UpdateDebugResults();
		}

		private void snippetToolStripMenuItem_Click(object sender, EventArgs e)
		{
			ShowCodeForm();
		}

		private void toolStripButton2_Click(object sender, EventArgs e)
		{
			ShowCodeForm();
		}

		protected void LoadAssembly(string filename, string platform, string includeDirectory = null)
		{
			Compiler.CompilerOptions.Architecture = GetArchitecture(platform);

			var moduleLoader = new MosaModuleLoader();

			if (includeDirectory != null)
				moduleLoader.AddPrivatePath(includeDirectory);

			moduleLoader.AddPrivatePath(Path.GetDirectoryName(filename));
			moduleLoader.LoadModuleFromFile(filename);

			var metadata = moduleLoader.CreateMetadata();

			var typeSystem = TypeSystem.Load(metadata);

			Compiler.Load(typeSystem);
		}

		private void ShowCodeForm()
		{
			form.ShowDialog();

			if (form.DialogResult == DialogResult.OK)
			{
				if (!string.IsNullOrEmpty(form.Assembly))
				{
					LoadAssembly(form.Assembly, AppDomain.CurrentDomain.BaseDirectory);
				}
			}
		}

		private void toolStripButton3_Click(object sender, EventArgs e)
		{
			Compile();
		}

		private void cbLabels_SelectedIndexChanged(object sender, EventArgs e)
		{
			UpdateResults();
		}

		private string CreateText(List<string> list)
		{
			if (list == null)
				return string.Empty;

			var result = new StringBuilder();

			foreach (var l in list)
			{
				result.AppendLine(l);
			}

			return result.ToString();
		}

		private void toolStripButton4_Click(object sender, EventArgs e)
		{
			Compile();
		}

		private void SubmitMethodStatus(int totalMethods, int completedMethods)
		{
			toolStripProgressBar1.Maximum = totalMethods;
			toolStripProgressBar1.Value = completedMethods;
		}

		void ITraceListener.OnNewCompilerTraceEvent(CompilerEvent compilerStage, string message, int threadID)
		{
			SubmitTraceEvent(compilerStage, message, threadID);

			MethodInvoker call = delegate ()
			{
				SubmitTraceEventGUI(compilerStage, message, threadID);
			};

			Invoke(call);
		}

		void ITraceListener.OnUpdatedCompilerProgress(int totalMethods, int completedMethods)
		{
			MethodInvoker call = delegate ()
			{
				SubmitMethodStatus(totalMethods, completedMethods);
			};

			Invoke(call);
		}

		void ITraceListener.OnNewTraceLog(TraceLog traceLog)
		{
			if (traceLog.Type == TraceType.DebugTrace)
			{
				if (traceLog.Lines.Count == 0)
					return;

				var stagesection = traceLog.Stage;

				if (traceLog.Section != null)
					stagesection = stagesection + "-" + traceLog.Section;

				methodStore.SetDebugStageInformation(traceLog.Method, stagesection, traceLog.Lines);
			}
			else if (traceLog.Type == TraceType.Counters)
			{
				methodStore.SetMethodCounterInformation(traceLog.Method, traceLog.Lines);
			}
			else if (traceLog.Type == TraceType.InstructionList)
			{
				methodStore.SetInstructionTraceInformation(traceLog.Method, traceLog.Stage, traceLog.Lines);
			}
		}

		protected void LoadAssemblyDebugInfo(string assemblyFileName)
		{
			string dbgFile = Path.ChangeExtension(assemblyFileName, "pdb");

			if (File.Exists(dbgFile))
			{
				tbResult.AppendText("File: " + dbgFile + "\n");
				tbResult.AppendText("======================\n");
				using (FileStream fileStream = new FileStream(dbgFile, FileMode.Open, FileAccess.Read, FileShare.Read))
				{
					using (PdbReader reader = new PdbReader(fileStream))
					{
						tbResult.AppendText("Global targetSymbols: \n");
						tbResult.AppendText("======================\n");
						foreach (CvSymbol symbol in reader.GlobalSymbols)
						{
							tbResult.AppendText(symbol.ToString() + "\n");
						}

						tbResult.AppendText("Types:\n");
						foreach (PdbType type in reader.Types)
						{
							tbResult.AppendText(type.Name + "\n");
							tbResult.AppendText("======================\n");
							tbResult.AppendText("Symbols:\n");
							foreach (CvSymbol symbol in type.Symbols)
							{
								tbResult.AppendText("\t" + symbol.ToString() + "\n");
							}

							tbResult.AppendText("Lines:\n");
							foreach (CvLine line in type.LineNumbers)
							{
								tbResult.AppendText("\t" + line.ToString() + "\n");
							}
						}
					}
				}
			}
		}

		private void DumpAllMethodStagesToolStripMenuItem_Click(object sender, EventArgs e)
		{
			var type = GetCurrentType();

			if (type == null)
				return;

			if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
			{
				var path = folderBrowserDialog1.SelectedPath;

				cbStages.SelectedIndex = 0;

				while (true)
				{
					cbStages_SelectedIndexChanged(null, null);

					string stage = GetCurrentStage();
					var result = tbResult.Text.Replace("\n", "\r\n");

					File.WriteAllText(Path.Combine(path, stage + "-stage.txt"), result);

					if (cbStages.Items.Count == cbStages.SelectedIndex + 1)
						break;

					cbStages.SelectedIndex++;
				}

				cbDebugStages.SelectedIndex = 0;

				while (true)
				{
					cbDebugStages_SelectedIndexChanged(null, null);

					string stage = GetCurrentDebugStage();
					var result = rbDebugResult.Text.Replace("\n", "\r\n");

					File.WriteAllText(Path.Combine(path, stage + "-debug.txt"), result);

					if (cbDebugStages.Items.Count == cbDebugStages.SelectedIndex + 1)
						break;

					cbDebugStages.SelectedIndex++;
				}
			}
		}

		private void cbPlatform_SelectedIndexChanged(object sender, EventArgs e)
		{
			//
		}
	}
}
