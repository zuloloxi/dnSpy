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
using System.Diagnostics;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnSpy.NRefactory;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Disassembler;
using ICSharpCode.ILSpy;

namespace dnSpy.AsmEditor.MethodBody {
	[Flags]
	enum WriteObjectFlags : uint {
		None						= 0,
		ShortInstruction			= 0x00000001,
	}

	static class BodyUtils {
		public static readonly Parameter NullParameter = new Parameter(int.MinValue);

		public static bool IsNull(object op) {
			return op == null ||
				op == NullParameter ||
				op == InstructionVM.Null ||
				op == LocalVM.Null;
		}

		public static object TryGetVM(Dictionary<object, object> ops, object objModel) {
			if (objModel == null)
				return null;
			object objVm;
			if (!ops.TryGetValue(objModel, out objVm))
				return objModel;
			return objVm;
		}

		public static object TryGetModel(Dictionary<object, object> ops, object objVm) {
			if (IsNull(objVm))
				return null;
			object objModel;
			if (!ops.TryGetValue(objVm, out objModel))
				return objVm;
			return objModel;
		}

		public static object ToOperandVM(Dictionary<object, object> ops, object operand) {
			var targets = operand as IList<Instruction>;
			if (targets != null) {
				var newTargets = new InstructionVM[targets.Count];
				for (int i = 0; i < newTargets.Length; i++)
					newTargets[i] = (InstructionVM)TryGetVM(ops, (object)targets[i] ?? InstructionVM.Null);
				return newTargets;
			}

			return TryGetVM(ops, operand);
		}

		public static object ToOperandModel(Dictionary<object, object> ops, object operand) {
			var targets = operand as IList<InstructionVM>;
			if (targets != null) {
				var newTargets = new Instruction[targets.Count];
				for (int i = 0; i < newTargets.Length; i++)
					newTargets[i] = TryGetModel(ops, targets[i]) as Instruction;
				return newTargets;
			}

			return TryGetModel(ops, operand);
		}

		public static void UpdateIndexesOffsets(this IList<InstructionVM> instrs, int index) {
			uint offset = index > 0 ? instrs[index - 1].Offset + (uint)instrs[index - 1].GetSize() : 0;
			for (; index < instrs.Count; index++) {
				var instr = instrs[index];
				instr.Index = index;
				instr.Offset = offset;
				offset += (uint)instr.GetSize();
			}
		}

		public static uint UpdateInstructionOffsets(this IList<InstructionVM> instrs, int index = 0) {
			uint offset = index > 0 ? instrs[index - 1].Offset + (uint)instrs[index - 1].GetSize() : 0;
			for (; index < instrs.Count; index++) {
				var instr = instrs[index];
				instr.Offset = offset;
				offset += (uint)instr.GetSize();
			}
			return offset;
		}

		public static void SimplifyMacros(this IList<InstructionVM> instrs, IList<LocalVM> locals, IList<Parameter> parameters) {
			foreach (var instr in instrs) {
				switch (instr.Code) {
				case Code.Beq_S:
					instr.Code = Code.Beq;
					break;

				case Code.Bge_S:
					instr.Code = Code.Bge;
					break;

				case Code.Bge_Un_S:
					instr.Code = Code.Bge_Un;
					break;

				case Code.Bgt_S:
					instr.Code = Code.Bgt;
					break;

				case Code.Bgt_Un_S:
					instr.Code = Code.Bgt_Un;
					break;

				case Code.Ble_S:
					instr.Code = Code.Ble;
					break;

				case Code.Ble_Un_S:
					instr.Code = Code.Ble_Un;
					break;

				case Code.Blt_S:
					instr.Code = Code.Blt;
					break;

				case Code.Blt_Un_S:
					instr.Code = Code.Blt_Un;
					break;

				case Code.Bne_Un_S:
					instr.Code = Code.Bne_Un;
					break;

				case Code.Br_S:
					instr.Code = Code.Br;
					break;

				case Code.Brfalse_S:
					instr.Code = Code.Brfalse;
					break;

				case Code.Brtrue_S:
					instr.Code = Code.Brtrue;
					break;

				case Code.Ldarg_0:
					instr.Code = Code.Ldarg;
					instr.InstructionOperandVM.OperandListItem = ReadList(parameters, 0) ?? BodyUtils.NullParameter;
					break;

				case Code.Ldarg_1:
					instr.Code = Code.Ldarg;
					instr.InstructionOperandVM.OperandListItem = ReadList(parameters, 1) ?? BodyUtils.NullParameter;
					break;

				case Code.Ldarg_2:
					instr.Code = Code.Ldarg;
					instr.InstructionOperandVM.OperandListItem = ReadList(parameters, 2) ?? BodyUtils.NullParameter;
					break;

				case Code.Ldarg_3:
					instr.Code = Code.Ldarg;
					instr.InstructionOperandVM.OperandListItem = ReadList(parameters, 3) ?? BodyUtils.NullParameter;
					break;

				case Code.Ldarg_S:
					instr.Code = Code.Ldarg;
					break;

				case Code.Ldarga_S:
					instr.Code = Code.Ldarga;
					break;

				case Code.Ldc_I4_0:
					instr.Code = Code.Ldc_I4;
					instr.InstructionOperandVM.Int32.Value = 0;
					break;

				case Code.Ldc_I4_1:
					instr.Code = Code.Ldc_I4;
					instr.InstructionOperandVM.Int32.Value = 1;
					break;

				case Code.Ldc_I4_2:
					instr.Code = Code.Ldc_I4;
					instr.InstructionOperandVM.Int32.Value = 2;
					break;

				case Code.Ldc_I4_3:
					instr.Code = Code.Ldc_I4;
					instr.InstructionOperandVM.Int32.Value = 3;
					break;

				case Code.Ldc_I4_4:
					instr.Code = Code.Ldc_I4;
					instr.InstructionOperandVM.Int32.Value = 4;
					break;

				case Code.Ldc_I4_5:
					instr.Code = Code.Ldc_I4;
					instr.InstructionOperandVM.Int32.Value = 5;
					break;

				case Code.Ldc_I4_6:
					instr.Code = Code.Ldc_I4;
					instr.InstructionOperandVM.Int32.Value = 6;
					break;

				case Code.Ldc_I4_7:
					instr.Code = Code.Ldc_I4;
					instr.InstructionOperandVM.Int32.Value = 7;
					break;

				case Code.Ldc_I4_8:
					instr.Code = Code.Ldc_I4;
					instr.InstructionOperandVM.Int32.Value = 8;
					break;

				case Code.Ldc_I4_M1:
					instr.Code = Code.Ldc_I4;
					instr.InstructionOperandVM.Int32.Value = -1;
					break;

				case Code.Ldc_I4_S:
					if (instr.InstructionOperandVM.SByte.HasError)
						break;
					instr.Code = Code.Ldc_I4;
					instr.InstructionOperandVM.Int32.Value = instr.InstructionOperandVM.SByte.Value;
					break;

				case Code.Ldloc_0:
					instr.Code = Code.Ldloc;
					instr.InstructionOperandVM.OperandListItem = ReadList(locals, 0) ?? LocalVM.Null;
					break;

				case Code.Ldloc_1:
					instr.Code = Code.Ldloc;
					instr.InstructionOperandVM.OperandListItem = ReadList(locals, 1) ?? LocalVM.Null;
					break;

				case Code.Ldloc_2:
					instr.Code = Code.Ldloc;
					instr.InstructionOperandVM.OperandListItem = ReadList(locals, 2) ?? LocalVM.Null;
					break;

				case Code.Ldloc_3:
					instr.Code = Code.Ldloc;
					instr.InstructionOperandVM.OperandListItem = ReadList(locals, 3) ?? LocalVM.Null;
					break;

				case Code.Ldloc_S:
					instr.Code = Code.Ldloc;
					break;

				case Code.Ldloca_S:
					instr.Code = Code.Ldloca;
					break;

				case Code.Leave_S:
					instr.Code = Code.Leave;
					break;

				case Code.Starg_S:
					instr.Code = Code.Starg;
					break;

				case Code.Stloc_0:
					instr.Code = Code.Stloc;
					instr.InstructionOperandVM.OperandListItem = ReadList(locals, 0) ?? LocalVM.Null;
					break;

				case Code.Stloc_1:
					instr.Code = Code.Stloc;
					instr.InstructionOperandVM.OperandListItem = ReadList(locals, 1) ?? LocalVM.Null;
					break;

				case Code.Stloc_2:
					instr.Code = Code.Stloc;
					instr.InstructionOperandVM.OperandListItem = ReadList(locals, 2) ?? LocalVM.Null;
					break;

				case Code.Stloc_3:
					instr.Code = Code.Stloc;
					instr.InstructionOperandVM.OperandListItem = ReadList(locals, 3) ?? LocalVM.Null;
					break;

				case Code.Stloc_S:
					instr.Code = Code.Stloc;
					break;
				}
			}
		}

		static T ReadList<T>(IList<T> list, int index) {
			if (list == null || index < 0 || index >= list.Count)
				return default(T);
			return list[index];
		}

		public static void OptimizeMacros(this IList<InstructionVM> instrs) {
			foreach (var instr in instrs) {
				Parameter arg;
				LocalVM local;
				switch (instr.Code) {
				case Code.Ldarg:
				case Code.Ldarg_S:
					arg = instr.InstructionOperandVM.Value as Parameter;
					if (arg == null)
						break;
					if (arg.Index == 0)
						instr.Code = Code.Ldarg_0;
					else if (arg.Index == 1)
						instr.Code = Code.Ldarg_1;
					else if (arg.Index == 2)
						instr.Code = Code.Ldarg_2;
					else if (arg.Index == 3)
						instr.Code = Code.Ldarg_3;
					else if (byte.MinValue <= arg.Index && arg.Index <= byte.MaxValue)
						instr.Code = Code.Ldarg_S;
					break;

				case Code.Ldarga:
					arg = instr.InstructionOperandVM.Value as Parameter;
					if (arg == null)
						break;
					if (byte.MinValue <= arg.Index && arg.Index <= byte.MaxValue)
						instr.Code = Code.Ldarga_S;
					break;

				case Code.Ldc_I4:
				case Code.Ldc_I4_S:
					int i4;
					if (instr.Code == Code.Ldc_I4) {
						if (instr.InstructionOperandVM.Int32.HasError)
							break;
						i4 = instr.InstructionOperandVM.Int32.Value;
					}
					else {
						if (instr.InstructionOperandVM.SByte.HasError)
							break;
						i4 = instr.InstructionOperandVM.SByte.Value;
					}
					switch (i4) {
					case 0: instr.Code = Code.Ldc_I4_0; break;
					case 1: instr.Code = Code.Ldc_I4_1; break;
					case 2: instr.Code = Code.Ldc_I4_2; break;
					case 3: instr.Code = Code.Ldc_I4_3; break;
					case 4: instr.Code = Code.Ldc_I4_4; break;
					case 5: instr.Code = Code.Ldc_I4_5; break;
					case 6: instr.Code = Code.Ldc_I4_6; break;
					case 7: instr.Code = Code.Ldc_I4_7; break;
					case 8: instr.Code = Code.Ldc_I4_8; break;
					case-1: instr.Code = Code.Ldc_I4_M1; break;

					default:
						if (sbyte.MinValue <= i4 && i4 <= sbyte.MaxValue) {
							instr.Code = Code.Ldc_I4_S;
							instr.InstructionOperandVM.SByte.Value = (sbyte)i4;
						}
						break;
					}
					break;

				case Code.Ldloc:
				case Code.Ldloc_S:
					local = instr.InstructionOperandVM.Value as LocalVM;
					if (local == null)
						break;
					if (local.Index == 0)
						instr.Code = Code.Ldloc_0;
					else if (local.Index == 1)
						instr.Code = Code.Ldloc_1;
					else if (local.Index == 2)
						instr.Code = Code.Ldloc_2;
					else if (local.Index == 3)
						instr.Code = Code.Ldloc_3;
					else if (byte.MinValue <= local.Index && local.Index <= byte.MaxValue)
						instr.Code = Code.Ldloc_S;
					break;

				case Code.Ldloca:
					local = instr.InstructionOperandVM.Value as LocalVM;
					if (local == null)
						break;
					if (byte.MinValue <= local.Index && local.Index <= byte.MaxValue)
						instr.Code = Code.Ldloca_S;
					break;

				case Code.Starg:
					arg = instr.InstructionOperandVM.Value as Parameter;
					if (arg == null)
						break;
					if (byte.MinValue <= arg.Index && arg.Index <= byte.MaxValue)
						instr.Code = Code.Starg_S;
					break;

				case Code.Stloc:
				case Code.Stloc_S:
					local = instr.InstructionOperandVM.Value as LocalVM;
					if (local == null)
						break;
					if (local.Index == 0)
						instr.Code = Code.Stloc_0;
					else if (local.Index == 1)
						instr.Code = Code.Stloc_1;
					else if (local.Index == 2)
						instr.Code = Code.Stloc_2;
					else if (local.Index == 3)
						instr.Code = Code.Stloc_3;
					else if (byte.MinValue <= local.Index && local.Index <= byte.MaxValue)
						instr.Code = Code.Stloc_S;
					break;
				}
			}

			instrs.OptimizeBranches();
		}

		public static void SimplifyBranches(this IList<InstructionVM> instrs) {
			foreach (var instr in instrs) {
				switch (instr.Code) {
				case Code.Beq_S:	instr.Code = Code.Beq; break;
				case Code.Bge_S:	instr.Code = Code.Bge; break;
				case Code.Bgt_S:	instr.Code = Code.Bgt; break;
				case Code.Ble_S:	instr.Code = Code.Ble; break;
				case Code.Blt_S:	instr.Code = Code.Blt; break;
				case Code.Bne_Un_S:	instr.Code = Code.Bne_Un; break;
				case Code.Bge_Un_S:	instr.Code = Code.Bge_Un; break;
				case Code.Bgt_Un_S:	instr.Code = Code.Bgt_Un; break;
				case Code.Ble_Un_S:	instr.Code = Code.Ble_Un; break;
				case Code.Blt_Un_S:	instr.Code = Code.Blt_Un; break;
				case Code.Br_S:		instr.Code = Code.Br; break;
				case Code.Brfalse_S:instr.Code = Code.Brfalse; break;
				case Code.Brtrue_S:	instr.Code = Code.Brtrue; break;
				case Code.Leave_S:	instr.Code = Code.Leave; break;
				}
			}
		}

		public static void OptimizeBranches(this IList<InstructionVM> instrs) {
			while (true) {
				instrs.UpdateInstructionOffsets();

				bool modified = false;
				foreach (var instr in instrs) {
					OpCode shortOpCode;
					switch (instr.Code) {
					case Code.Beq:		shortOpCode = OpCodes.Beq_S; break;
					case Code.Bge:		shortOpCode = OpCodes.Bge_S; break;
					case Code.Bge_Un:	shortOpCode = OpCodes.Bge_Un_S; break;
					case Code.Bgt:		shortOpCode = OpCodes.Bgt_S; break;
					case Code.Bgt_Un:	shortOpCode = OpCodes.Bgt_Un_S; break;
					case Code.Ble:		shortOpCode = OpCodes.Ble_S; break;
					case Code.Ble_Un:	shortOpCode = OpCodes.Ble_Un_S; break;
					case Code.Blt:		shortOpCode = OpCodes.Blt_S; break;
					case Code.Blt_Un:	shortOpCode = OpCodes.Blt_Un_S; break;
					case Code.Bne_Un:	shortOpCode = OpCodes.Bne_Un_S; break;
					case Code.Br:		shortOpCode = OpCodes.Br_S; break;
					case Code.Brfalse:	shortOpCode = OpCodes.Brfalse_S; break;
					case Code.Brtrue:	shortOpCode = OpCodes.Brtrue_S; break;
					case Code.Leave:	shortOpCode = OpCodes.Leave_S; break;
					default: continue;
					}
					var targetInstr = instr.InstructionOperandVM.Value as InstructionVM;
					if (targetInstr == null)
						continue;

					int afterShortInstr;
					if (targetInstr.Offset >= instr.Offset) {
						// Target is >= this instruction so use the offset after
						// current instruction
						afterShortInstr = (int)instr.Offset + instr.GetSize();
					}
					else {
						// Target is < this instruction so use the offset after
						// the short instruction
						const int operandSize = 1;
						afterShortInstr = (int)instr.Offset + shortOpCode.Size + operandSize;
					}

					int displ = (int)targetInstr.Offset - afterShortInstr;
					if (sbyte.MinValue <= displ && displ <= sbyte.MaxValue) {
						instr.Code = shortOpCode.Code;
						modified = true;
					}
				}
				if (!modified)
					break;
			}
		}

		public static void WriteObject(ITextOutput output, object obj, WriteObjectFlags flags = WriteObjectFlags.None) {
			if (IsNull(obj)) {
				output.Write("null", TextTokenType.Keyword);
				return;
			}

			var mr = obj as IMemberRef;
			if (mr != null) {
				if (ILLanguage.Write(output, mr))
					return;
			}

			var local = obj as LocalVM;
			if (local != null) {
				output.Write(IdentifierEscaper.Escape(GetLocalName(local.Name, local.Index)), TextTokenType.Local);
				output.WriteSpace();
				output.WriteLocalParameterIndex(local.Index);
				return;
			}

			var parameter = obj as Parameter;
			if (parameter != null) {
				if (parameter.IsHiddenThisParameter)
					output.Write("this", TextTokenType.Keyword);
				else {
					output.Write(IdentifierEscaper.Escape(GetParameterName(parameter.Name, parameter.Index)), TextTokenType.Parameter);
					output.WriteSpace();
					output.WriteLocalParameterIndex(parameter.Index);
				}
				return;
			}

			var instr = obj as InstructionVM;
			if (instr != null) {
				if ((flags & WriteObjectFlags.ShortInstruction) != 0)
					output.WriteShort(instr);
				else
					output.WriteLong(instr);
				return;
			}

			var instrs = obj as IList<InstructionVM>;
			if (instrs != null) {
				output.Write(instrs);
				return;
			}

			var methodSig = obj as MethodSig;
			if (methodSig != null) {
				output.Write(methodSig);
				return;
			}

			if (obj is TypeSig) {
				(obj as TypeSig).WriteTo(output);
				return;
			}

			if (obj is Code) {
				output.Write(((Code)obj).ToOpCode().Name, TextTokenType.OpCode);
				return;
			}

			// This code gets called by the combobox and it sometimes passes in the empty string.
			// It's never shown in the UI.
			Debug.Assert(string.Empty.Equals(obj), "Shouldn't be here");
			output.Write(obj.ToString(), TextTokenType.Text);
		}

		static string GetLocalName(string name, int index) {
			if (!string.IsNullOrEmpty(name))
				return name;
			return string.Format("V_{0}", index);
		}

		static string GetParameterName(string name, int index) {
			if (!string.IsNullOrEmpty(name))
				return name;
			return string.Format("A_{0}", index);
		}

		static void WriteLocalParameterIndex(this ITextOutput output, int index) {
			output.Write('(', TextTokenType.Operator);
			output.Write(index.ToString(), TextTokenType.Number);
			output.Write(')', TextTokenType.Operator);
		}

		static void WriteLong(this ITextOutput output, InstructionVM instr) {
			output.WriteShort(instr);
			output.WriteSpace();
			output.Write(instr.Code.ToOpCode().Name, TextTokenType.OpCode);
			output.WriteSpace();
			Write(output, instr.InstructionOperandVM);
		}

		static void Write(this ITextOutput output, InstructionOperandVM opvm) {
			switch (opvm.InstructionOperandType) {
			case MethodBody.InstructionOperandType.None:
				break;

			case MethodBody.InstructionOperandType.SByte:	output.Write(opvm.SByte.StringValue, TextTokenType.Number); break;
			case MethodBody.InstructionOperandType.Byte:	output.Write(opvm.Byte.StringValue, TextTokenType.Number); break;
			case MethodBody.InstructionOperandType.Int32:	output.Write(opvm.Int32.StringValue, TextTokenType.Number); break;;
			case MethodBody.InstructionOperandType.Int64:	output.Write(opvm.Int64.StringValue, TextTokenType.Number); break;;
			case MethodBody.InstructionOperandType.Single:	output.Write(opvm.Single.StringValue, TextTokenType.Number); break;;
			case MethodBody.InstructionOperandType.Double:	output.Write(opvm.Double.StringValue, TextTokenType.Number); break;;
			case MethodBody.InstructionOperandType.String:	output.Write(opvm.String.StringValue, TextTokenType.String); break;;

			case MethodBody.InstructionOperandType.Field:
			case MethodBody.InstructionOperandType.Method:
			case MethodBody.InstructionOperandType.Token:
			case MethodBody.InstructionOperandType.Type:
			case MethodBody.InstructionOperandType.MethodSig:
			case MethodBody.InstructionOperandType.SwitchTargets:
			case MethodBody.InstructionOperandType.BranchTarget:
			case MethodBody.InstructionOperandType.Local:
			case MethodBody.InstructionOperandType.Parameter:
				WriteObject(output, opvm.Value, WriteObjectFlags.ShortInstruction);
				break;

			default: throw new InvalidOperationException();
			}
		}

		static void WriteShort(this ITextOutput output, InstructionVM instr) {
			output.Write(instr.Index.ToString(), TextTokenType.Number);
			output.WriteSpace();
			output.Write('(', TextTokenType.Operator);
			output.Write(string.Format("{0:X4}", instr.Offset), TextTokenType.Number);
			output.Write(')', TextTokenType.Operator);
		}

		static void Write(this ITextOutput output, IList<InstructionVM> instrs) {
			output.Write('[', TextTokenType.Operator);
			for (int i = 0; i < instrs.Count; i++) {
				if (i > 0) {
					output.Write(',', TextTokenType.Operator);
					output.WriteSpace();
				}
				output.WriteShort(instrs[i]);
			}
			output.Write(']', TextTokenType.Operator);
		}
	}
}
