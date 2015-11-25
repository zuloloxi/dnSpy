﻿/*
    Copyright (C) 2014-2015 de4dot@gmail.com

    This file is part of dnSpy

    dnSpy is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    dnSpy is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with dnSpy.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using dndbg.COM.CorDebug;
using dndbg.Engine;
using dnlib.DotNet;
using dnlib.PE;
using dnSpy.Debugger.CallStack;
using dnSpy.Debugger.Dialogs;
using dnSpy.Debugger.IMModules;
using dnSpy.Files;
using dnSpy.MVVM;
using ICSharpCode.Decompiler;
using ICSharpCode.ILSpy;
using ICSharpCode.ILSpy.TextView;
using ICSharpCode.ILSpy.TreeNodes;
using ICSharpCode.TreeView;

namespace dnSpy.Debugger {
	public sealed class DebugManager {
		public static readonly DebugManager Instance = new DebugManager();
		DebuggedProcessRunningNotifier debuggedProcessRunningNotifier;

		[DllImport("user32")]
		static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

		[return: MarshalAs(UnmanagedType.Bool)]
		[DllImport("user32")]
		static extern bool SetForegroundWindow(IntPtr hWnd);

		internal void OnLoaded() {
			MainWindow.Instance.Closing += OnClosing;
			MainWindow.Instance.CanExecuteEvent += MainWindow_CanExecuteEvent;
			debuggedProcessRunningNotifier = new DebuggedProcessRunningNotifier();
			debuggedProcessRunningNotifier.ProcessRunning += DebuggedProcessRunningNotifier_ProcessRunning;
			DebuggerSettings.Instance.PropertyChanged += DebuggerSettings_PropertyChanged;
		}

		void DebuggerSettings_PropertyChanged(object sender, PropertyChangedEventArgs e) {
			if (e.PropertyName == "IgnoreBreakInstructions") {
				if (Debugger != null)
					Debugger.Options.IgnoreBreakInstructions = DebuggerSettings.Instance.IgnoreBreakInstructions;
			}
			else if (e.PropertyName == "UseMemoryModules") {
				if (DebugManager.Instance.ProcessState != DebuggerProcessState.Terminated && DebuggerSettings.Instance.UseMemoryModules)
					UpdateCurrentLocationToInMemoryModule();
			}
		}

		void DebuggedProcessRunningNotifier_ProcessRunning(object sender, DebuggedProcessRunningEventArgs e) {
			try {
				var hWnd = e.Process.MainWindowHandle;
				if (hWnd != IntPtr.Zero)
					SetForegroundWindow(hWnd);
			}
			catch {
			}

			if (ProcessRunning != null)
				ProcessRunning(this, EventArgs.Empty);
		}

		void MainWindow_CanExecuteEvent(object sender, MainWindow.CanExecuteEventArgs e) {
			if (e.Type == MainWindow.CanExecuteType.ReloadList) {
				if (Instance.IsDebugging)
					e.Result = false;
			}
		}

		DebugManager() {
			OnProcessStateChanged += DebugManager_OnProcessStateChanged;
		}

		public ICommand DebugCurrentAssemblyCommand {
			get { return new RelayCommand(a => DebugCurrentAssembly(a), a => CanDebugCurrentAssembly(a)); }
		}

		public ICommand DebugAssemblyCommand {
			get { return new RelayCommand(a => DebugAssembly(), a => CanDebugAssembly); }
		}

		public ICommand DebugCoreCLRAssemblyCommand {
			get { return new RelayCommand(a => DebugCoreCLRAssembly(), a => CanDebugCoreCLRAssembly); }
		}

		public ICommand StartWithoutDebuggingCommand {
			get { return new RelayCommand(a => StartWithoutDebugging(), a => CanStartWithoutDebugging); }
		}

		public ICommand AttachCommand {
			get { return new RelayCommand(a => Attach(), a => CanAttach); }
		}

		public ICommand BreakCommand {
			get { return new RelayCommand(a => Break(), a => CanBreak); }
		}

		public ICommand RestartCommand {
			get { return new RelayCommand(a => Restart(), a => CanRestart); }
		}

		public ICommand StopCommand {
			get { return new RelayCommand(a => Stop(), a => CanStop); }
		}

		public ICommand DetachCommand {
			get { return new RelayCommand(a => Detach(), a => CanDetach); }
		}

		public ICommand ContinueCommand {
			get { return new RelayCommand(a => Continue(), a => CanContinue); }
		}

		public ICommand StepIntoCommand {
			get { return new RelayCommand(a => StepInto(), a => CanStepInto); }
		}

		public ICommand StepOverCommand {
			get { return new RelayCommand(a => StepOver(), a => CanStepOver); }
		}

		public ICommand StepOutCommand {
			get { return new RelayCommand(a => StepOut(), a => CanStepOut); }
		}

		public ICommand ShowNextStatementCommand {
			get { return new RelayCommand(a => ShowNextStatement(), a => CanShowNextStatement); }
		}

		public ICommand SetNextStatementCommand {
			get { return new RelayCommand(a => SetNextStatement(a), a => CanSetNextStatement(a)); }
		}

		public DebuggerProcessState ProcessState {
			get { return Debugger == null ? DebuggerProcessState.Terminated : Debugger.ProcessState; }
		}

		/// <summary>
		/// true if we're debugging
		/// </summary>
		public bool IsDebugging {
			get { return ProcessState != DebuggerProcessState.Terminated; }
		}

		/// <summary>
		/// true if we've attached to a process
		/// </summary>
		public bool HasAttached {
			get { return IsDebugging && Debugger.HasAttached; }
		}

		/// <summary>
		/// Gets the current debugger. This is null if we're not debugging anything
		/// </summary>
		public DnDebugger Debugger {
			get { return debugger; }
		}
		DnDebugger debugger;

		public event EventHandler<DebuggerEventArgs> OnProcessStateChanged;

		/// <summary>
		/// Called when the process has been running for a short amount of time. Usually won't
		/// get called when stepping since it normally doesn't take a long time.
		/// </summary>
		public event EventHandler ProcessRunning;

		static void SetRunningStatusMessage() {
			MainWindow.Instance.SetStatus("Running...");
		}

		static void SetReadyStatusMessage(string msg) {
			if (string.IsNullOrEmpty(msg))
				MainWindow.Instance.SetStatus("Ready");
			else
				MainWindow.Instance.SetStatus(string.Format("Ready - {0}", msg));
		}

		internal event EventHandler<DebuggerEventArgs> OnProcessStateChanged2;
		void DebugManager_OnProcessStateChanged(object sender, DebuggerEventArgs e) {
			// InMemoryModuleManager should be notified here. It needs to execute first so it can
			// call LoadEverything() and load all dynamic modules so ResolveToken() of new methods
			// and types work.
			if (OnProcessStateChanged2 != null)
				OnProcessStateChanged2(sender, e);

			switch (DebugManager.Instance.ProcessState) {
			case DebuggerProcessState.Starting:
				evalDisabled = false;
				currentLocation = null;
				currentMethod = null;
				SetRunningStatusMessage();
				MainWindow.Instance.SetDebugging();
				break;

			case DebuggerProcessState.Continuing:
				break;

			case DebuggerProcessState.Running:
				if (Debugger.IsEvaluating)
					break;
				SetRunningStatusMessage();
				break;

			case DebuggerProcessState.Stopped:
				// If we're evaluating, or if eval has completed, don't do a thing. This code
				// should only be executed when a BP hits or if a stepping operation has completed.
				if (Debugger.IsEvaluating || Debugger.EvalCompleted)
					break;

				evalDisabled = false;
				BringMainWindowToFrontAndActivate();

				UpdateCurrentLocation();
				if (currentMethod != null && currentLocation != null)
					JumpToCurrentStatement(MainWindow.Instance.SafeActiveTextView);

				SetReadyStatusMessage(new StoppedMessageCreator().GetMessage(Debugger));
				break;

			case DebuggerProcessState.Terminated:
				evalDisabled = false;
				currentLocation = null;
				currentMethod = null;
				MainWindow.Instance.HideStatus();
				MainWindow.Instance.ClearDebugging();
				break;
			}
		}
		CodeLocation? currentLocation = null;

		static void BringMainWindowToFrontAndActivate() {
			SetWindowPos(new WindowInteropHelper(MainWindow.Instance).Handle, IntPtr.Zero, 0, 0, 0, 0, 3);
			MainWindow.Instance.Activate();
		}

		struct StoppedMessageCreator {
			StringBuilder sb;

			public string GetMessage(DnDebugger debugger) {
				if (debugger == null || debugger.ProcessState != DebuggerProcessState.Stopped)
					return null;

				sb = new StringBuilder();

				bool seenIlbp = false;
				foreach (var state in debugger.DebuggerStates) {
					foreach (var stopState in state.StopStates) {
						switch (stopState.Reason) {
						case DebuggerStopReason.Other:
							Append("Unknown Reason");
							break;

						case DebuggerStopReason.UnhandledException:
							Append("Unhandled Exception");
							break;

						case DebuggerStopReason.Exception:
							Append("Exception");
							break;

						case DebuggerStopReason.DebugEventBreakpoint:
							if (state.EventArgs != null)
								Append(GetEventDescription(state.EventArgs));
							else
								Append("DebugEvent");
							break;

						case DebuggerStopReason.AnyDebugEventBreakpoint:
							if (state.EventArgs != null)
								Append(GetEventDescription(state.EventArgs));
							else
								Append("Any DebugEvent");
							break;

						case DebuggerStopReason.Break:
							Append("Break Instruction");
							break;

						case DebuggerStopReason.ILCodeBreakpoint:
							if (seenIlbp)
								break;
							seenIlbp = true;
							Append("IL Breakpoint");
							break;

						case DebuggerStopReason.Step:
							break;
						}
					}
				}

				return sb.ToString();
			}

			string GetEventDescription(DebugCallbackEventArgs e) {
				CorModule mod;
				switch (e.Type) {
				case DebugCallbackType.Exception:
					var ex1Args = (ExceptionDebugCallbackEventArgs)e;
					return ex1Args.Unhandled ? "Unhandled Exception" : "Exception";

				case DebugCallbackType.CreateProcess:
					var cpArgs = (CreateProcessDebugCallbackEventArgs)e;
					var p = cpArgs.CorProcess;
					if (p == null)
						break;
					return string.Format("CreateProcess PID={0} CLR v{1}", p.ProcessId, p.CLRVersion);

				case DebugCallbackType.CreateThread:
					var ctArgs = (CreateThreadDebugCallbackEventArgs)e;
					var t = ctArgs.CorThread;
					if (t == null)
						break;
					return string.Format("CreateThread TID={0} VTID={1}", t.ThreadId, t.VolatileThreadId);

				case DebugCallbackType.LoadModule:
					var lmArgs = (LoadModuleDebugCallbackEventArgs)e;
					mod = lmArgs.CorModule;
					if (mod == null)
						break;
					if (mod.IsDynamic || mod.IsInMemory)
						return string.Format("LoadModule DYN={0} MEM={1} {2:X8} {3:X8} {4}", mod.IsDynamic ? 1 : 0, mod.IsInMemory ? 1 : 0, mod.Address, mod.Size, mod.Name);
					return string.Format("LoadModule A={0:X8} S={1:X8} {2}", mod.Address, mod.Size, mod.Name);

				case DebugCallbackType.LoadClass:
					var lcArgs = (LoadClassDebugCallbackEventArgs)e;
					var cls = lcArgs.CorClass;
					mod = cls == null ? null : cls.Module;
					if (mod == null)
						break;
					return string.Format("LoadClass 0x{0:X8} {1} {2}", cls.Token, FilterLongName(cls.ToString()), mod.Name);

				case DebugCallbackType.DebuggerError:
					var deArgs = (DebuggerErrorDebugCallbackEventArgs)e;
					return string.Format("DebuggerError hr=0x{0:X8} error=0x{1:X8}", deArgs.HError, deArgs.ErrorCode);

				case DebugCallbackType.CreateAppDomain:
					var cadArgs = (CreateAppDomainDebugCallbackEventArgs)e;
					var ad = cadArgs.CorAppDomain;
					if (ad == null)
						break;
					return string.Format("CreateAppDomain {0} {1}", ad.Id, ad.Name);

				case DebugCallbackType.LoadAssembly:
					var laArgs = (LoadAssemblyDebugCallbackEventArgs)e;
					var asm = laArgs.CorAssembly;
					if (asm == null)
						break;
					return string.Format("LoadAssembly {0}", asm.Name);

				case DebugCallbackType.ControlCTrap:
					return "Ctrl+C";

				case DebugCallbackType.BreakpointSetError:
					var bpseArgs = (BreakpointSetErrorDebugCallbackEventArgs)e;
					return string.Format("BreakpointSetError error=0x{0:X8}", bpseArgs.Error);

				case DebugCallbackType.Exception2:
					var ex2Args = (Exception2DebugCallbackEventArgs)e;
					var sb = new StringBuilder();
					sb.Append(string.Format("Exception Offset={0:X4} ", ex2Args.Offset));
					switch (ex2Args.EventType) {
					case CorDebugExceptionCallbackType.DEBUG_EXCEPTION_FIRST_CHANCE:
						sb.Append("FirstChance");
						break;
					case CorDebugExceptionCallbackType.DEBUG_EXCEPTION_USER_FIRST_CHANCE:
						sb.Append("UserFirstChance");
						break;
					case CorDebugExceptionCallbackType.DEBUG_EXCEPTION_CATCH_HANDLER_FOUND:
						sb.Append("CatchHandlerFound");
						break;
					case CorDebugExceptionCallbackType.DEBUG_EXCEPTION_UNHANDLED:
						sb.Append("Unhandled");
						break;
					default:
						sb.Append("Unknown");
						break;
					}
					return sb.ToString();

				case DebugCallbackType.MDANotification:
					var mdan = (MDANotificationDebugCallbackEventArgs)e;
					var mda = mdan.CorMDA;
					if (mda == null)
						return "MDA Notification";
					return string.Format("MDA Notification: TID={0} {1} {2}", mda.OSThreadId, mda.Name, mda.Description);
				}

				return e.Type.ToString();
			}

			void Append(string msg) {
				if (sb.Length > 0)
					sb.Append(", ");
				sb.Append(msg);
			}

			static string FilterLongName(string s) {
				const int MAX_LEN = 128;
				if (s.Length <= MAX_LEN)
					return s;
				return s.Substring(0, MAX_LEN / 2) + "..." + s.Substring(s.Length - (MAX_LEN - MAX_LEN / 2));
			}
		}

		void OnClosing(object sender, CancelEventArgs e) {
			if (IsDebugging) {
				var result = MainWindow.Instance.ShowIgnorableMessageBox("debug: exit program", "Do you want to stop debugging?", MessageBoxButton.YesNo);
				if (result == MsgBoxButton.None || result == MsgBoxButton.No)
					e.Cancel = true;
			}
		}

		static string GetIncompatiblePlatformErrorMessage() {
			if (IntPtr.Size == 4)
				return "Use dnSpy.exe to debug 64-bit applications.";
			return "Use dnSpy-x86.exe to debug 32-bit applications.";
		}

		bool DebugProcess(DebugProcessOptions options) {
			if (IsDebugging)
				return false;
			if (options == null)
				return false;

			RemoveDebugger();

			DnDebugger newDebugger;
			try {
				newDebugger = DnDebugger.DebugProcess(options);
			}
			catch (Exception ex) {
				var cex = ex as COMException;
				const int ERROR_NOT_SUPPORTED = unchecked((int)0x80070032);
				if (cex != null && cex.ErrorCode == ERROR_NOT_SUPPORTED)
					MainWindow.Instance.ShowMessageBox(string.Format("Could not start the debugger. {0}", GetIncompatiblePlatformErrorMessage()));
				else if (cex != null && cex.ErrorCode == CordbgErrors.CORDBG_E_UNCOMPATIBLE_PLATFORMS)
					MainWindow.Instance.ShowMessageBox(string.Format("Could not start the debugger. {0}", GetIncompatiblePlatformErrorMessage()));
				else if (cex != null && cex.ErrorCode == unchecked((int)0x800702E4))
					MainWindow.Instance.ShowMessageBox("Could not start the debugger. The debugged program requires admin privileges. Restart dnSpy with admin rights and try again.");
				else
					MainWindow.Instance.ShowMessageBox(string.Format("Could not start the debugger. Make sure you have access to the file '{0}'\n\nError: {1}", options.Filename, ex.Message));
				return false;
			}
			Initialize(newDebugger);

			return true;
		}

		void Initialize(DnDebugger newDebugger) {
			if (DebuggerSettings.Instance.DisableManagedDebuggerDetection)
				DisableSystemDebuggerDetection.Initialize(newDebugger);
			AddDebugger(newDebugger);
			Debug.Assert(debugger == newDebugger);
			CallOnProcessStateChanged();
		}

		void CallOnProcessStateChanged(DnDebugger dbg = null) {
			CallOnProcessStateChanged(dbg ?? debugger, DebuggerEventArgs.Empty);
		}

		void CallOnProcessStateChanged(object sender, DebuggerEventArgs e) {
			if (OnProcessStateChanged != null)
				OnProcessStateChanged(sender, e ?? DebuggerEventArgs.Empty);
		}

		void DnDebugger_OnProcessStateChanged(object sender, DebuggerEventArgs e) {
			if (debugger == null || sender != debugger)
				return;

			CallOnProcessStateChanged(sender, e);

			if (debugger.ProcessState == DebuggerProcessState.Terminated) {
				lastDebugProcessOptions = null;
				RemoveDebugger();
			}

			// This is sometimes needed. Press Ctrl+Shift+F5 a couple of times and the toolbar
			// debugger icons aren't updated until you release Ctrl+Shift.
			if (ProcessState == DebuggerProcessState.Stopped || !IsDebugging)
				CommandManager.InvalidateRequerySuggested();

			if (ProcessState == DebuggerProcessState.Stopped)
				ShowExceptionMessage();
		}

		void ShowExceptionMessage() {
			var dbg = Debugger;
			if (dbg == null)
				return;
			if (dbg.Current.GetStopState(DebuggerStopReason.Exception) == null)
				return;
			var thread = dbg.Current.Thread;
			if (thread == null)
				return;
			var exValue = thread.CorThread.CurrentException;
			if (exValue == null)
				return;
			var exType = exValue.ExactType;
			var name = exType == null ? null : exType.ToString(TypePrinterFlags.ShowNamespaces);
			var msg = string.Format("Exception thrown: '{0}' in {1}\n\nIf there is a handler for this exception, the program may be safely continued.", name, Path.GetFileName(thread.Process.Filename));
			BringMainWindowToFrontAndActivate();
			MainWindow.Instance.ShowMessageBox(msg);
		}

		void RemoveDebugger() {
			if (debugger == null)
				return;

			debugger.OnProcessStateChanged -= DnDebugger_OnProcessStateChanged;
			debugger = null;
		}

		void AddDebugger(DnDebugger newDebugger) {
			RemoveDebugger();

			debugger = newDebugger;
			DebugCallbackEvent_counter = 0;
			newDebugger.OnProcessStateChanged += DnDebugger_OnProcessStateChanged;
			newDebugger.DebugCallbackEvent += DnDebugger_DebugCallbackEvent;
		}

		void DnDebugger_DebugCallbackEvent(DnDebugger dbg, DebugCallbackEventArgs e) {
			try {
				DebugCallbackEvent_counter++;

				if (DebugCallbackEvent_counter > 1)
					return;
				if (e.Type == DebugCallbackType.Exception2) {
					var ee = (Exception2DebugCallbackEventArgs)e;
					if (ee.EventType == CorDebugExceptionCallbackType.DEBUG_EXCEPTION_UNHANDLED)
						UnhandledException(ee);
				}
				else if (e.Type == DebugCallbackType.DebuggerError)
					OnDebuggerError((DebuggerErrorDebugCallbackEventArgs)e);
			}
			finally {
				DebugCallbackEvent_counter--;
			}
		}
		int DebugCallbackEvent_counter = 0;

		void UnhandledException(Exception2DebugCallbackEventArgs e) {
			if (UnhandledException_counter != 0)
				return;
			try {
				UnhandledException_counter++;

				Debug.Assert(e.EventType == CorDebugExceptionCallbackType.DEBUG_EXCEPTION_UNHANDLED);
				var thread = e.CorThread;
				var exValue = thread == null ? null : thread.CurrentException;

				var sb = new StringBuilder();
				AddExceptionInfo(sb, exValue, "Exception");
				var innerExValue = EvalUtils.ReflectionReadExceptionInnerException(exValue);
				if (innerExValue != null && innerExValue.IsReference && !innerExValue.IsNull)
					AddExceptionInfo(sb, innerExValue, "\n\nInner Exception");

				var process = DebugManager.Instance.Debugger.Processes.FirstOrDefault(p => p.Threads.Any(t => t.CorThread == thread));
				CorProcess cp;
				var processName = process != null ? Path.GetFileName(process.Filename) : string.Format("pid {0}", (cp = thread.Process) == null ? 0 : cp.ProcessId);
				BringMainWindowToFrontAndActivate();
				var res = MainWindow.Instance.ShowMessageBox(string.Format("An unhandled exception occurred in {0}\n\n{1}\n\nPress OK to stop, and Cancel to let the program run.", processName, sb), MessageBoxButton.OKCancel);
				if (res != MsgBoxButton.Cancel)
					e.AddStopReason(DebuggerStopReason.UnhandledException);
			}
			finally {
				UnhandledException_counter--;
			}
		}
		int UnhandledException_counter = 0;

		void OnDebuggerError(DebuggerErrorDebugCallbackEventArgs e) {
			string msg;
			if (e.HError == CordbgErrors.CORDBG_E_UNCOMPATIBLE_PLATFORMS)
				msg = GetIncompatiblePlatformErrorMessage();
			else
				msg = string.Format("A CLR debugger error occurred. Terminate the debugged process and try again.\n\nHR: 0x{0:X8}\nError: 0x{1:X8}", e.HError, e.ErrorCode);
			BringMainWindowToFrontAndActivate();
			MainWindow.Instance.ShowMessageBox(msg);
		}

		static void AddExceptionInfo(StringBuilder sb, CorValue exValue, string msg) {
			var exType = exValue == null ? null : exValue.ExactType;
			int? hr = EvalUtils.ReflectionReadExceptionHResult(exValue);
			string exMsg = EvalUtils.ReflectionReadExceptionMessage(exValue);
			string exTypeString = exType == null ? "<Unknown Exception Type>" : exType.ToString();
			var s = string.Format("{0}: {1}\n\nMessage: {2}\n\nHResult: 0x{3:X8}", msg, exTypeString, exMsg, hr ?? -1);
			sb.Append(s);
		}

		public bool CanDebugCurrentAssembly(object parameter) {
			return GetCurrentExecutableAssembly(parameter as ContextMenuEntryContext) != null;
		}

		public void DebugCurrentAssembly(object parameter) {
			var asm = GetCurrentExecutableAssembly(parameter as ContextMenuEntryContext);
			if (asm == null)
				return;
			DebugAssembly(GetDebugAssemblyOptions(CreateDebugProcessVM(asm)));
		}

		internal DnSpyFile GetCurrentExecutableAssembly(ContextMenuEntryContext context) {
			if (context == null)
				return null;
			if (IsDebugging)
				return null;

			SharpTreeNode node;
			if (context.Element is DecompilerTextView) {
				var tabState = MainWindow.Instance.GetActiveDecompileTabState();
				if (tabState == null)
					return null;
				if (tabState.DecompiledNodes.Length == 0)
					return null;
				node = tabState.DecompiledNodes[0];
			}
			else if (context.SelectedTreeNodes != null) {
				if (context.SelectedTreeNodes.Length == 0)
					return null;
				node = context.SelectedTreeNodes[0];
			}
			else
				return null;

			return GetCurrentExecutableAssembly(node, true);
		}

		DnSpyFile GetCurrentExecutableAssembly(SharpTreeNode node, bool mustBeNetExe) {
			var asmNode = ILSpyTreeNode.GetNode<AssemblyTreeNode>(node);
			if (asmNode == null)
				return null;

			var file = asmNode.DnSpyFile;
			var peImage = file.PEImage;
			if (peImage == null)
				return null;
			if ((peImage.ImageNTHeaders.FileHeader.Characteristics & Characteristics.Dll) != 0)
				return null;
			if (mustBeNetExe) {
				var mod = file.ModuleDef;
				if (mod == null)
					return null;
				if (mod.Assembly == null || mod.Assembly.ManifestModule != mod)
					return null;
				if (mod.ManagedEntryPoint == null && mod.NativeEntryPoint == 0)
					return null;
			}

			return file;
		}

		DnSpyFile GetCurrentExecutableAssembly(bool mustBeNetExe) {
			return GetCurrentExecutableAssembly(MainWindow.Instance.treeView.SelectedItem as SharpTreeNode, mustBeNetExe);
		}

		public bool CanStartWithoutDebugging {
			get { return !IsDebugging && GetCurrentExecutableAssembly(false) != null; }
		}

		public void StartWithoutDebugging() {
			var asm = GetCurrentExecutableAssembly(false);
			if (asm == null || !File.Exists(asm.Filename))
				return;
			try {
				Process.Start(asm.Filename);
			}
			catch (Exception ex) {
				MainWindow.Instance.ShowMessageBox(string.Format("Could not start '{0}'\n:ERROR: {0}", asm.Filename, ex.Message));
			}
		}

		DebugCoreCLRVM CreateDebugCoreCLRVM(DnSpyFile asm = null) {
			// Re-use the previous one if it's the same file
			if (lastDebugCoreCLRVM != null && asm != null) {
				if (StringComparer.OrdinalIgnoreCase.Equals(lastDebugCoreCLRVM.Filename, asm.Filename))
					return lastDebugCoreCLRVM.Clone();
			}

			var vm = new DebugCoreCLRVM();
			if (asm != null)
				vm.Filename = asm.Filename;
			vm.DbgShimFilename = DebuggerSettings.Instance.CoreCLRDbgShimFilename;
			vm.BreakProcessType = DebuggerSettings.Instance.BreakProcessType;
			return vm;
		}

		public bool CanDebugCoreCLRAssembly {
			get { return !IsDebugging; }
		}

		public void DebugCoreCLRAssembly() {
			if (!CanDebugAssembly)
				return;
			DebugCoreCLRVM vm = null;
			if (vm == null) {
				var asm = GetCurrentExecutableAssembly(true);
				if (asm != null)
					vm = CreateDebugCoreCLRVM(asm);
			}
			if (vm == null)
				vm = lastDebugCoreCLRVM ?? CreateDebugCoreCLRVM();
			DebugAssembly(GetDebugAssemblyOptions(vm.Clone()));
		}
		DebugCoreCLRVM lastDebugCoreCLRVM;

		DebugProcessOptions GetDebugAssemblyOptions(DebugCoreCLRVM vm, bool askUser = true) {
			if (askUser) {
				var win = new DebugCoreCLRDlg();
				win.DataContext = vm;
				win.Owner = MainWindow.Instance;
				if (win.ShowDialog() != true)
					return null;
			}

			var opts = new DebugProcessOptions(new CoreCLRTypeDebugInfo(vm.DbgShimFilename, vm.HostFilename, vm.HostCommandLine));
			opts.DebugMessageDispatcher = WpfDebugMessageDispatcher.Instance;
			opts.CurrentDirectory = vm.CurrentDirectory;
			opts.Filename = vm.Filename;
			opts.CommandLine = vm.CommandLine;
			opts.BreakProcessType = vm.BreakProcessType;
			lastDebugCoreCLRVM = vm;
			return opts;
		}

		DebugProcessVM CreateDebugProcessVM(DnSpyFile asm = null) {
			// Re-use the previous one if it's the same file
			if (lastDebugProcessVM != null && asm != null) {
				if (StringComparer.OrdinalIgnoreCase.Equals(lastDebugProcessVM.Filename, asm.Filename))
					return lastDebugProcessVM.Clone();
			}

			var vm = new DebugProcessVM();
			if (asm != null)
				vm.Filename = asm.Filename;
			vm.BreakProcessType = DebuggerSettings.Instance.BreakProcessType;
			return vm;
		}

		public bool CanDebugAssembly {
			get { return !IsDebugging; }
		}

		public void DebugAssembly() {
			if (!CanDebugAssembly)
				return;
			DebugProcessVM vm = null;
			if (vm == null) {
				var asm = GetCurrentExecutableAssembly(true);
				if (asm != null)
					vm = CreateDebugProcessVM(asm);
			}
			if (vm == null)
				vm = lastDebugProcessVM ?? CreateDebugProcessVM();
			DebugAssembly(GetDebugAssemblyOptions(vm.Clone()));
		}
		DebugProcessVM lastDebugProcessVM;

		DebugProcessOptions GetDebugAssemblyOptions(DebugProcessVM vm, bool askUser = true) {
			if (askUser) {
				var win = new DebugProcessDlg();
				win.DataContext = vm;
				win.Owner = MainWindow.Instance;
				if (win.ShowDialog() != true)
					return null;
			}

			var opts = new DebugProcessOptions(new DesktopCLRTypeDebugInfo());
			opts.DebugMessageDispatcher = WpfDebugMessageDispatcher.Instance;
			opts.CurrentDirectory = vm.CurrentDirectory;
			opts.Filename = vm.Filename;
			opts.CommandLine = vm.CommandLine;
			opts.BreakProcessType = vm.BreakProcessType;
			lastDebugProcessVM = vm;
			return opts;
		}

		public bool CanRestart {
			get { return IsDebugging && lastDebugProcessOptions != null && !HasAttached; }
		}

		public void Restart() {
			if (!CanRestart)
				return;

			Stop();
			if (debugger != null) {
				var dbg = debugger;
				RemoveDebugger();
				CallOnProcessStateChanged(dbg);
			}

			DebugAssembly(lastDebugProcessOptions);
		}

		void DebugAssembly(DebugProcessOptions options) {
			if (options == null)
				return;
			var optionsCopy = options.Clone();
			if (!DebugProcess(options))
				return;
			lastDebugProcessOptions = optionsCopy;
		}
		DebugProcessOptions lastDebugProcessOptions = null;

		public bool CanAttach {
			get { return !IsDebugging; }
		}

		public void Attach() {
			if (!CanAttach)
				return;

			var data = new AttachProcessVM(MainWindow.Instance.Dispatcher);
			var win = new AttachProcessDlg();
			win.DataContext = data;
			win.Owner = MainWindow.Instance;
			var res = win.ShowDialog();
			data.Dispose();
			if (res != true)
				return;

			var processVM = data.SelectedProcess;
			if (processVM == null)
				return;

			var options = new AttachProcessOptions(processVM.CLRTypeInfo);
			options.ProcessId = processVM.PID;
			options.DebugMessageDispatcher = WpfDebugMessageDispatcher.Instance;
			Attach(options);
		}

		bool Attach(AttachProcessOptions options) {
			if (IsDebugging)
				return false;
			if (options == null)
				return false;

			RemoveDebugger();

			DnDebugger newDebugger;
			try {
				newDebugger = DnDebugger.Attach(options);
			}
			catch (Exception ex) {
				MainWindow.Instance.ShowMessageBox(string.Format("Could not start debugger.\n\nError: {0}", ex.Message));
				return false;
			}
			Initialize(newDebugger);

			return true;
		}

		public bool CanBreak {
			get { return ProcessState == DebuggerProcessState.Starting || ProcessState == DebuggerProcessState.Running; }
		}

		public void Break() {
			if (!CanBreak)
				return;

			int hr = Debugger.TryBreakProcesses();
			if (hr < 0)
				MainWindow.Instance.ShowMessageBox(string.Format("Could not break process. Error: 0x{0:X8}", hr));
		}

		public bool CanStop {
			get { return IsDebugging; }
		}

		public void Stop() {
			if (!CanStop)
				return;
			Debugger.TerminateProcesses();
		}

		public bool CanDetach {
			get { return ProcessState != DebuggerProcessState.Continuing && ProcessState != DebuggerProcessState.Terminated; }
		}

		public void Detach() {
			if (!CanDetach)
				return;
			int hr = Debugger.TryDetach();
			if (hr < 0)
				MainWindow.Instance.ShowMessageBox(string.Format("Could not detach process. Error: 0x{0:X8}", hr));
		}

		public bool CanContinue {
			get { return ProcessState == DebuggerProcessState.Stopped; }
		}

		public void Continue() {
			if (!CanContinue)
				return;
			Debugger.Continue();
		}

		public void JumpToCurrentStatement(DecompilerTextView textView) {
			JumpToCurrentStatement(textView, true);
		}

		void JumpToCurrentStatement(DecompilerTextView textView, bool canRefreshMethods) {
			if (textView == null)
				return;
			if (currentMethod == null)
				return;

			// The file could've been added lazily to the list so add a short delay before we select it
			MainWindow.Instance.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => {
				MainWindow.Instance.JumpToReference(textView, currentMethod, (success, hasMovedCaret) => {
					if (success)
						return MoveCaretToCurrentStatement(textView, canRefreshMethods);
					return false;
				});
			}));
		}

		bool MoveCaretToCurrentStatement(DecompilerTextView textView, bool canRefreshMethods) {
			if (currentLocation == null)
				return false;
			if (DebugUtils.MoveCaretTo(textView, currentLocation.Value.SerializedDnSpyToken, currentLocation.Value.Offset))
				return true;
			if (!canRefreshMethods)
				return false;

			RefreshMethodBodies(textView);

			return false;
		}

		void RefreshMethodBodies(DecompilerTextView textView) {
			if (currentLocation == null)
				return;
			if (currentMethod == null)
				return;

			// If this fails, we're probably in the prolog or epilog. Shouldn't normally happen.
			if (!currentLocation.Value.IsExact && !currentLocation.Value.IsApproximate)
				return;
			var body = currentMethod.Body;
			if (body == null)
				return;
			// If the offset is a valid instruction in the body, the method is probably not encrypted
			if (body.Instructions.Any(i => i.Offset == currentLocation.Value.Offset))
				return;

			// No instruction with the current offset: it must be encrypted, and since we're executing
			// the method, we must be using an invalid method body. Use a copy of the module in
			// memory, and refresh the method bodies in case it's already loaded, and re-decompile
			// the method.

			var mod = currentMethod.Module;
			if (mod == null)
				return;
			var modNode = MainWindow.Instance.DnSpyFileListTreeNode.FindModuleNode(mod);
			if (modNode == null)
				return;
			var memFile = modNode.DnSpyFile as MemoryModuleDefFile;
			DnSpyFile file = memFile;
			if (memFile == null) {
				if (modNode.DnSpyFile is CorModuleDefFile)
					return;
				var corMod = currentLocation.Value.Function.Module;
				if (corMod == null || corMod.IsDynamic)
					return;
				var dnMod = ModuleLoader.GetDnModule(corMod);
				file = InMemoryModuleManager.Instance.LoadFile(dnMod, true);
				Debug.Assert(file != null);
				memFile = file as MemoryModuleDefFile;
			}
			if (file == null)
				return;
			// It's null if we couldn't load the file from memory because the PE / COR20 headers
			// are corrupt (eg. an obfuscator overwrote various fields with garbage). In that case,
			// file is a CorModuleDefFile and it's using the MD API to read the MD.
			if (memFile != null)
				InMemoryModuleManager.Instance.UpdateModuleMemory(memFile);
			UpdateCurrentMethod(file);
			JumpToCurrentStatement(textView, false);
		}

		void UpdateCurrentLocationToInMemoryModule() {
			UpdateCurrentMethod();
			if (currentMethod != null && currentLocation != null)
				JumpToCurrentStatement(MainWindow.Instance.SafeActiveTextView);
		}

		struct CodeLocation {
			public readonly CorFunction Function;
			public uint Offset;
			public CorDebugMappingResult Mapping;

			public uint Token {
				get { return Function.Token; }
			}

			public bool IsExact {
				get { return (Mapping & CorDebugMappingResult.MAPPING_EXACT) != 0; }
			}

			public bool IsApproximate {
				get { return (Mapping & CorDebugMappingResult.MAPPING_APPROXIMATE) != 0; }
			}

			public SerializedDnSpyToken SerializedDnSpyToken {
				get {
					var mod = Function.Module;
					if (mod == null)
						return new SerializedDnSpyToken();
					return new SerializedDnSpyToken(mod.SerializedDnModule.ToSerializedDnSpyModule(), Function.Token);
				}
			}

			public CodeLocation(CorFunction func, uint offset, CorDebugMappingResult mapping) {
				this.Function = func;
				this.Offset = offset;
				this.Mapping = mapping;
			}

			public static bool SameMethod(CodeLocation a, CodeLocation b) {
				return a.Function == b.Function;
			}
		}

		void UpdateCurrentLocation() {
			UpdateCurrentLocation(Debugger.Current.ILFrame);
		}

		internal void UpdateCurrentLocation(CorFrame frame) {
			var newLoc = GetCodeLocation(frame);

			if (currentLocation == null || newLoc == null) {
				currentLocation = newLoc;
				UpdateCurrentMethod();
				return;
			}
			if (!CodeLocation.SameMethod(currentLocation.Value, newLoc.Value)) {
				currentLocation = newLoc;
				UpdateCurrentMethod();
				return;
			}

			currentLocation = newLoc;
		}

		void UpdateCurrentMethod(DnSpyFile file = null) {
			if (currentLocation == null) {
				currentMethod = null;
				return;
			}

			if (file == null)
				file = ModuleLoader.Instance.LoadModule(currentLocation.Value.Function.Module, true);
			Debug.Assert(file != null);
			var loadedMod = file == null ? null : file.ModuleDef;
			if (loadedMod == null) {
				currentMethod = null;
				return;
			}

			currentMethod = loadedMod.ResolveToken(currentLocation.Value.Token) as MethodDef;
			Debug.Assert(currentMethod != null);
		}
		MethodDef currentMethod;

		CodeLocation? GetCodeLocation(CorFrame frame) {
			if (ProcessState != DebuggerProcessState.Stopped)
				return null;
			if (frame == null)
				return null;
			var func = frame.Function;
			if (func == null)
				return null;

			return new CodeLocation(func, frame.GetILOffset(), frame.ILFrameIP.Mapping);
		}

		StepRange[] GetStepRanges(DnDebugger debugger, CorFrame frame, bool isStepInto) {
			if (frame == null)
				return null;
			if (!frame.IsILFrame)
				return null;
			if (frame.ILFrameIP.IsUnmappedAddress)
				return null;

			var key = CreateMethodKey(debugger, frame);
			if (key == null)
				return null;

			MemberMapping mapping;
			var textView = MainWindow.Instance.SafeActiveTextView;
			var cm = textView.CodeMappings;
			if (cm == null || !cm.TryGetValue(key.Value, out mapping)) {
				// User has decompiled some other code or switched to another tab
				UpdateCurrentMethod();
				JumpToCurrentStatement(textView);

				// It could be cached and immediately available. Check again
				cm = textView.CodeMappings;
				if (cm == null || !cm.TryGetValue(key.Value, out mapping))
					return null;
			}

			bool isMatch;
			var scm = mapping.GetInstructionByOffset(frame.GetILOffset(), out isMatch);
			uint[] ilRanges;
			if (scm == null)
				ilRanges = mapping.ToArray(null, false);
			else
				ilRanges = scm.ToArray(isMatch);

			if (ilRanges.Length == 0)
				return null;
			return CreateStepRanges(ilRanges);
		}

		static StepRange[] CreateStepRanges(uint[] ilRanges) {
			var stepRanges = new StepRange[ilRanges.Length / 2];
			if (stepRanges.Length == 0)
				return null;
			for (int i = 0; i < stepRanges.Length; i++)
				stepRanges[i] = new StepRange(ilRanges[i * 2], ilRanges[i * 2 + 1]);
			return stepRanges;
		}

		static SerializedDnSpyToken? CreateMethodKey(DnDebugger debugger, CorFrame frame) {
			var sma = frame.SerializedDnModule;
			if (sma == null)
				return null;

			return new SerializedDnSpyToken(sma.Value.ToSerializedDnSpyModule(), frame.Token);
		}

		CorFrame GetCurrentILFrame() {
			return StackFrameManager.Instance.FirstILFrame;
		}

		CorFrame GetCurrentMethodILFrame() {
			return StackFrameManager.Instance.SelectedFrame;
		}

		public bool CanStepInto {
			get { return ProcessState == DebuggerProcessState.Stopped && GetCurrentILFrame() != null; }
		}

		public void StepInto() {
			if (!CanStepInto)
				return;

			var ranges = GetStepRanges(debugger, GetCurrentILFrame(), true);
			debugger.StepInto(ranges);
		}

		public bool CanStepOver {
			get { return ProcessState == DebuggerProcessState.Stopped && GetCurrentILFrame() != null; }
		}

		public void StepOver() {
			if (!CanStepOver)
				return;

			var ranges = GetStepRanges(debugger, GetCurrentILFrame(), false);
			debugger.StepOver(ranges);
		}

		public bool CanStepOut {
			get { return ProcessState == DebuggerProcessState.Stopped && GetCurrentILFrame() != null; }
		}

		public void StepOut() {
			if (!CanStepOut)
				return;

			debugger.StepOut(GetCurrentILFrame());
		}

		public bool CanRunTo(CorFrame frame) {
			return ProcessState == DebuggerProcessState.Stopped && Debugger.CanRunTo(frame);
		}

		public void RunTo(CorFrame frame) {
			if (!CanRunTo(frame))
				return;

			Debugger.RunTo(frame);
		}

		public bool CanShowNextStatement {
			get { return ProcessState == DebuggerProcessState.Stopped && GetCurrentILFrame() != null; }
		}

		public void ShowNextStatement() {
			if (!CanShowNextStatement)
				return;

			var textView = MainWindow.Instance.SafeActiveTextView;
			if (!TryShowNextStatement(textView)) {
				UpdateCurrentMethod();
				JumpToCurrentStatement(textView);
			}
		}

		bool TryShowNextStatement(DecompilerTextView textView) {
			// Always reset the selected frame
			StackFrameManager.Instance.SelectedFrameNumber = 0;
			if (currentLocation == null)
				return false;
			return DebugUtils.MoveCaretTo(textView, currentLocation.Value.SerializedDnSpyToken, currentLocation.Value.Offset);
		}

		public bool CanSetNextStatement(object parameter) {
			if (!IsDebugging)
				return false;

			var ctx = parameter as ContextMenuEntryContext;

			SourceCodeMapping mapping;
			string errMsg;
			if (!DebugGetSourceCodeMappingForSetNextStatement(ctx == null ? null : ctx.Element as DecompilerTextView, out errMsg, out mapping))
				return false;

			if (currentLocation != null && currentLocation.Value.IsExact)
				return currentLocation.Value.Offset != mapping.ILInstructionOffset.From;
			return true;
		}

		public bool SetNextStatement(object parameter) {
			string errMsg;
			if (!DebugSetNextStatement(parameter, out errMsg)) {
				if (string.IsNullOrEmpty(errMsg))
					errMsg = "Could not set next statement (unknown reason)";
				MainWindow.Instance.ShowMessageBox(errMsg);
				return false;
			}

			return true;
		}

		bool DebugSetNextStatement(object parameter, out string errMsg) {
			var ctx = parameter as ContextMenuEntryContext;
			SourceCodeMapping mapping;
			if (!DebugGetSourceCodeMappingForSetNextStatement(ctx == null ? null : ctx.Element as DecompilerTextView, out errMsg, out mapping))
				return false;

			uint ilOffset = mapping.ILInstructionOffset.From;
			var ilFrame = GetCurrentMethodILFrame();
			bool failed = ilFrame == null || !ilFrame.SetILFrameIP(ilOffset);

			// All frames are invalidated
			CallOnProcessStateChanged();

			if (failed) {
				errMsg = "Could not set the next statement.";
				return false;
			}

			return true;
		}

		bool DebugGetSourceCodeMappingForSetNextStatement(DecompilerTextView textView, out string errMsg, out SourceCodeMapping mapping) {
			errMsg = string.Empty;
			mapping = null;

			if (ProcessState == DebuggerProcessState.Terminated) {
				errMsg = "We're not debugging";
				return false;
			}
			if (ProcessState == DebuggerProcessState.Starting || ProcessState == DebuggerProcessState.Continuing || ProcessState == DebuggerProcessState.Running) {
				errMsg = "Can't set next statement when the process is running";
				return false;
			}

			if (textView == null) {
				textView = MainWindow.Instance.ActiveTextView;
				if (textView == null) {
					errMsg = "No tab is available. Decompile the current method!";
					return false;
				}
			}

			Dictionary<SerializedDnSpyToken, MemberMapping> cm;
			if (currentLocation == null || !DebugUtils.VerifyAndGetCurrentDebuggedMethod(textView, currentLocation.Value.SerializedDnSpyToken, out cm)) {
				errMsg = "No debug information found. Make sure that only the debugged method is selected in the treeview (press 'Alt+Num *' to go to current statement)";
				return false;
			}
			Debug.Assert(currentLocation != null);

			var location = textView.TextEditor.TextArea.Caret.Location;
			var bps = SourceCodeMappingUtils.Find(cm, location.Line, location.Column);
			if (bps.Count == 0) {
				errMsg = "It's not possible to set the next statement here";
				return false;
			}

			if (GetCurrentMethodILFrame() == null) {
				errMsg = "There's no IL frame";
				return false;
			}

			foreach (var bp in bps) {
				var md = bp.MemberMapping.MethodDef;
				if (currentLocation.Value.Function.Token != md.MDToken.Raw)
					continue;
				var serAsm = md.Module.ToSerializedDnSpyModule();
				if (!serAsm.Equals(currentLocation.Value.SerializedDnSpyToken.Module))
					continue;

				mapping = bp;
				break;
			}
			if (mapping == null) {
				errMsg = "The next statement cannot be set to another method";
				return false;
			}

			return true;
		}

		/// <summary>
		/// Creates an eval. Don't call this if <see cref="EvalDisabled"/> is true
		/// </summary>
		/// <param name="thread">Thread to use</param>
		/// <returns></returns>
		public DnEval CreateEval(CorThread thread) {
			Debug.WriteLineIf(ProcessState != DebuggerProcessState.Stopped, "Can't evaluate unless debugger is stopped");
			if (ProcessState != DebuggerProcessState.Stopped)
				throw new EvalException(-1, "Can't evaluate unless debugger is stopped");
			if (UnhandledException_counter != 0)
				throw new EvalException(-1, "Can't evaluate when an unhandled exception has occurred");
			var eval = Debugger.CreateEval();
			eval.EvalEvent += (s, e) => DnEval_EvalEvent(s, e, eval);
			eval.SetThread(thread);
			return eval;
		}

		void DnEval_EvalEvent(object sender, EvalEventArgs e, DnEval eval) {
			if (eval == null || sender != eval)
				return;
			if (eval.EvalTimedOut)
				evalDisabled = true;
			if (callingEvalComplete)
				return;
			callingEvalComplete = true;
			var app = App.Current.Dispatcher;
			if (app != null && !app.HasShutdownStarted && !app.HasShutdownFinished) {
				app.BeginInvoke(DispatcherPriority.Send, new Action(() => {
					callingEvalComplete = false;
					if (ProcessState == DebuggerProcessState.Stopped)
						Debugger.SignalEvalComplete();
				}));
			}
		}
		volatile bool callingEvalComplete;

		public bool CanEvaluate {
			get { return Debugger != null && !Debugger.IsEvaluating && UnhandledException_counter == 0; }
		}

		public bool EvalCompleted {
			get { return Debugger != null && Debugger.EvalCompleted; }
		}

		public bool EvalDisabled {
			get { return evalDisabled; }
		}
		bool evalDisabled;

		public void DisposeHandle(CorValue value) {
			var dbg = Debugger;
			if (dbg != null)
				dbg.DisposeHandle(value);
		}
	}
}
