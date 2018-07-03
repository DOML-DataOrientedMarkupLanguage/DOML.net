﻿#region License
// ====================================================
// Team DOML Copyright(C) 2017 Team DOML
// This program comes with ABSOLUTELY NO WARRANTY; This is free software, 
// and you are welcome to redistribute it under certain conditions; See 
// file LICENSE, which is part of this source code package, for details.
// ====================================================
#endregion

using System;
using System.Collections.Generic;
using DOML.Logger;

namespace DOML.IR {
    /// <summary>
    /// Opcodes for DOML IR.
    /// </summary>
    public enum Opcodes : byte {
        #region System
        /// <summary>
        /// `nop(void)`
        /// Explicitly does nothing.
        /// </summary>
        NOP = 0,

        /// <summary>
        /// `init(stackSize: int, registerSize: int)`
        /// Initialises the stack and regsiters.
        /// </summary>
        INIT = 1,

        /// <summary>
        /// `init(void)`
        /// De-initialises the stack and regsiters.
        /// </summary>
        DE_INIT = 2,

        /// <summary>
        /// `create_type(id: int, depth: int, (CollectionID: CollectionTypeID, Type: TypeID)[depth])`
        /// Creates a complex type.
        /// </summary>
        CREATE_TYPE = 3,
        #endregion
        #region STACK
        /// <summary>
        /// `new_obj(object: Type, constructor: Fn, register: int, count: int)`
        /// Creates a new object with an applied constructor call.
        /// </summary>
        NEW_OBJ = 10,

        /// <summary>
        /// `push(type: TypeID, count: int, parameters: object[count])`
        /// Pushes a set of objects onto the stack.
        /// </summary>
        PUSH = 11,
        CALL_N = 12,
        CALL_STACK = 13,
        POP = 14,
        GET_N = 15,
        GET_STACK = 16,
        #endregion
        #region QUICK_STACK
        QUICK_PUSH = 20,
        QUICK_CALL = 21,
        P_CALL = 22, // UNCONFIRMED
        P_NEW_OBJ = 23, // UNCONFIRMED
        QUICK_GET = 24,
        DUMB_GET = 25,
        #endregion
        #region ARRAY
        PUSH_ARRAY = 30,
        SET_ARRAY = 31,
        GET_ARRAY = 32,
        ARRAY_CPY = 33,
        COMPACT = 34,
        #endregion
        #region MAPS
        PUSH_MAP = 40,
        PUSH_COLLECTION = 41,
        SET_MAP = 42,
        SET_COLLECTION = 43,
        QUICK_SET_MAP = 44,
        ZIP_MAP = 45,
        GET_MAP = 46,
        GET_COLLECTION = 47,
        #endregion
    }

    /// <summary>
    /// The interpreter for DOML IR.
    /// This handles executing the opcodes.
    /// </summary>
    public class Interpreter {
        /// <summary>
        /// All the instructions to execute.
        /// </summary>
        public readonly List<Instruction> Instructions;

        /// <summary>
        /// The runtime of the interpreter instance.
        /// Manages the stack VM and registers.
        /// </summary>
        public readonly InterpreterRuntime Runtime;

        /// <summary>
        /// Create a new interpreter instance.
        /// </summary>
        /// <param name="instructions"> The instructions to assign to this interpreter instance. </param>
        public Interpreter(List<Instruction> instructions) {
            Instructions = instructions;
            Runtime = new InterpreterRuntime();
        }

        /// <summary>
        /// Executes all instructions.
        /// </summary>
        /// <param name="safe"> Run either safe or unsafe instructions (safe does checks, unsafe doesn't). </param>
        /// <remarks> If using the code generated by the parser instance you can use unsafe else use safe. </remarks>
        public void Execute(bool safe) {
            Runtime.ClearSpace();

            for (int i = 0; i < Instructions.Count; i++) {
                if (safe) {
                    HandleSafeInstruction(Instructions[i]);
                } else {
                    HandleUnsafeInstruction(Instructions[i]);
                }
            }
        }

        private void ParameterHelper<T1>(Instruction instruction, out T1 t1) {
            t1 = (T1)instruction.Parameters[0];
        }

        private void ParameterHelper<T1, T2>(Instruction instruction, out T1 t1, out T2 t2) {
            t1 = (T1)instruction.Parameters[0];
            t2 = (T2)instruction.Parameters[1];
        }

        private void ParameterHelper<T1, T2, T3>(Instruction instruction, out T1 t1, out T2 t2, out T3 t3) {
            t1 = (T1)instruction.Parameters[0];
            t2 = (T2)instruction.Parameters[1];
            t3 = (T3)instruction.Parameters[2];
        }

        private Type GetTypeForParamType(ParamType type) {
            switch (type) {
                case ParamType.INT: return typeof(long);
                case ParamType.FLT: return typeof(double);
                case ParamType.DEC: return typeof(decimal);
                case ParamType.STR: return typeof(string);
                case ParamType.BOOL: return typeof(bool);
                case ParamType.OBJ: return typeof(object);
                case ParamType.MAP:
                case ParamType.VEC:
                throw new InvalidOperationException("Can't create 'pushArray' from collection type.");
                default:
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Handles the instruction safely.
        /// </summary>
        /// <param name="instruction"> The instruction. </param>
        public void HandleSafeInstruction(Instruction instruction) {
            switch ((Opcodes)instruction.OpCode) {
                #region System Instructions
                case Opcodes.NOP: break;
                case Opcodes.INIT: {
                    ParameterHelper(instruction, out int stackSize, out int regSize);
                    Runtime.ReserveRegister(regSize);
                    Runtime.ReserveSpace(stackSize);
                    break;
                }
                case Opcodes.DE_INIT:
                Runtime.ClearRegisters();
                Runtime.ClearSpace();
                break;
                case Opcodes.CREATE_TYPE:
                // @TODO: Support complex types
                throw new NotImplementedException();
                #endregion
                #region Push Instructions
                case Opcodes.NEW_OBJ: {
                    ParameterHelper(instruction, out FunctionDefinition constructor, out int register);
                    constructor.action(Runtime, register);
                    break;
                }
                case Opcodes.PUSH: {
                    ParameterHelper(instruction, out int count);
                    // @FIXME: single check
                    for (int i = 0; i < count; i++) {
                        if (!Runtime.Push(instruction.Parameters[2 + i], true)) {
                            Log.Error("Invalid Push");
                            return;
                        }
                    }
                    break;
                }
                case Opcodes.CALL_N: {
                    ParameterHelper(instruction, out FunctionDefinition callee, out int register);
                    callee.action(Runtime, register);
                    break;
                }
                case Opcodes.CALL_STACK: {
                    if (!Runtime.Pop(out object callee)) {
                        Log.Error("Invalid call n");
                        return;
                    }
                    Runtime.SetObject(callee, -1);
                    Action<InterpreterRuntime, int> action = ((FunctionDefinition)instruction.Parameters[0]).action;
                    action(Runtime, -1);
                    Runtime.Push(callee, true);
                    break;
                }
                case Opcodes.POP: {
                    ParameterHelper(instruction, out int count);
                    for (int i = 0; i < count; i++) {
                        if (!Runtime.Pop()) {
                            Log.Error("Invalid Pop");
                        }
                    }
                    break;
                }
                case Opcodes.GET_N: {
                    ParameterHelper(instruction, out int register, out FunctionDefinition func);
                    func.action(Runtime, register);
                    break;
                }
                case Opcodes.GET_STACK: {
                    if (!Runtime.Pop(out object callee)) {
                        Log.Error("Invalid call n");
                        return;
                    }
                    Runtime.SetObject(callee, -1);
                    Action<InterpreterRuntime, int> action = ((FunctionDefinition)instruction.Parameters[0]).action;
                    action(Runtime, -1);
                    Runtime.Push(callee, true);
                    break;
                }
                #endregion
                #region Quick Instructions
                case Opcodes.QUICK_PUSH:
                case Opcodes.QUICK_CALL:
                case Opcodes.P_CALL:
                case Opcodes.P_NEW_OBJ:
                case Opcodes.QUICK_GET:
                case Opcodes.DUMB_GET:
                throw new NotImplementedException();
                #endregion
                #region Array Instructions
                case Opcodes.PUSH_ARRAY: {
                    ParameterHelper(instruction, out ParamType type, out int len);
                    if (!Runtime.Push(Array.CreateInstance(GetTypeForParamType(type), len), true)) {
                        Log.Error("Invalid Push Array");
                        return;
                    }
                    break;
                }
                case Opcodes.SET_ARRAY: {
                    if (!Runtime.Pop(out object value) || !Runtime.Peek(out Array array)) {
                        Log.Error("Invalid set array");
                        return;
                    }

                    array.SetValue(value, (int)instruction.Parameters[0]);
                    break;
                }
                case Opcodes.GET_ARRAY: {
                    if (!Runtime.Peek(out Array array) || !Runtime.Push(array.GetValue((int)instruction.Parameters[0]), true)) {
                        Log.Error("Invalid get array");
                        return;
                    }
                    break;
                }
                case Opcodes.ARRAY_CPY: {
                    if (!Runtime.Pop(out Array array)) {
                        Log.Error("Invalid set array");
                        return;
                    }

                    // Maybe we can't always do this?
                    instruction.Parameters.CopyTo(array, 0);
                    break;
                }
                case Opcodes.COMPACT: {
                    // @TODO
                    break;
                }
                #endregion
                #region Map Instructions
                case Opcodes.PUSH_MAP:
                break;
                case Opcodes.PUSH_COLLECTION:
                break;
                case Opcodes.SET_MAP:
                break;
                case Opcodes.SET_COLLECTION:
                break;
                case Opcodes.QUICK_SET_MAP:
                break;
                case Opcodes.ZIP_MAP:
                break;
                case Opcodes.GET_MAP:
                break;
                case Opcodes.GET_COLLECTION:
                break;
                #endregion
                default:
                throw new NotImplementedException("Option not implemented");
            }
        }

        /// <summary>
        /// Handles the instruction unsafely.
        /// </summary>
        /// <param name="instruction"> The instruction. </param>
        public void HandleUnsafeInstruction(Instruction instruction) {
            switch ((Opcodes)instruction.OpCode) {
                #region System Instructions

                #endregion
                default:
                throw new NotImplementedException("Option not implemented");
            }
        }
    }
}
