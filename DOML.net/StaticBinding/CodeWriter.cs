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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace StaticBindings
{
    /// <summary>
    /// This writes C# code that handles the instructions.
    /// </summary>
    public class CodeWriter : IDisposable
    {
        /// <summary>
        /// The internal writer.
        /// </summary>
        private TextWriter writer;

        /// <summary>
        /// The namespace that objects live in.
        /// </summary>
        private string rootNamespace;

        /// <summary>
        /// All the registers calls.
        /// </summary>
        private List<string> registerCalls = new List<string>();

        /// <summary>
        /// All the unregisters calls.
        /// </summary>
        private List<string> unregisterCalls = new List<string>();

        /// <summary>
        /// What indent level are we currently.
        /// </summary>
        private int indentLevel = 0;

        /// <summary>
        /// Create a new code writer pointing at a path.
        /// </summary>
        /// <param name="filePath"> The file path to output to. </param>
        /// <param name="append"> If true append to file else overwrite. </param>
        /// <param name="rootNamespace"> The namespace that objects exist in. </param>
        public CodeWriter(string filePath, bool append, string rootNamespace)
            : this(new StreamWriter(File.Exists(filePath) ? File.Open(filePath, FileMode.Truncate) : new FileStream(filePath, append ? FileMode.Append : FileMode.Create)),
                  rootNamespace)
        {
        }

        /// <summary>
        /// Create a new code writer pointing at a path.
        /// </summary>
        /// <param name="resultText"> The text to write to. </param>
        /// <param name="rootNamespace"> The namespace that objects exist in. </param>
        public CodeWriter(StringBuilder resultText, string rootNamespace) : this(new StringWriter(resultText), rootNamespace)
        {
        }

        /// <summary>
        /// Create a new code writer pointing at a path.
        /// </summary>
        /// <param name="writer"> The writer to write to. </param>
        /// <param name="rootNamespace"> The namespace that objects exist in. </param>
        private CodeWriter(TextWriter writer, string rootNamespace)
        {
            this.writer = writer;
            this.rootNamespace = rootNamespace;
        }

        /// <summary>
        /// Deconstructor, handles disposing of writer.
        /// </summary>
        ~CodeWriter()
        {
            Dispose();
        }

        /// <summary>
        /// Dispose writer.
        /// </summary>
        public void Dispose()
        {
            writer.Dispose();
        }

        /// <summary>
        /// Writes line to writer.
        /// Exposes some functionality of writer to outside.
        /// </summary>
        /// <param name="text"> The text to write</param>
        public void WriteLine(string text) => writer.WriteLine(text);

        /// <summary>
        /// Writes to writer.
        /// Exposes some functionality of writer to outside.
        /// </summary>
        /// <param name="text"> The text to write</param>
        public void Write(string text) => writer.Write(text);

        /// <summary>
        /// Writes an empty line to writer.
        /// Exposes some functionality of writer to outside.
        /// </summary>
        public void WriteEmptyLine() => writer.WriteLine();

        /// <summary>
        /// Writes line that is indented properly to writer.
        /// Exposes some functionality of writer to outside.
        /// </summary>
        /// <param name="text"> The text to write</param>
        public void WriteIndentLine(string text) => writer.WriteLine(new string('\t', indentLevel) + text);

        /// <summary>
        /// Writes that is indented properly to writer.
        /// Exposes some functionality of writer to outside.
        /// </summary>
        /// <param name="text"> The text to write</param>
        public void WriteWithIndent(string text) => writer.Write(new string('\t', indentLevel) + text);

        /// <summary>
        /// Writes a suppression.
        /// </summary>
        /// <param name="code"> The code to suppress. </param>
        /// <param name="disable"> If true it'll disable the suppression else restore it. </param>
        public void WriteSuppression(string code, bool disable, string comment)
        {
            WriteLine($"#pragma warning {(disable ? "disable" : "restore")} {code} // {comment}");
        }

        /// <summary>
        /// Writes the auto generated header.
        /// </summary>
        public void WriteHeader()
        {
            Write("/* THIS IS AUTO-GENERATED\n * ALL CHANGES WILL BE RESET\n * UPON GENERATION\n */\n");
        }

        /// <summary>
        /// Writes all the DOML usings.
        /// </summary>
        public void WriteUsings()
        {
            WriteLine("using DOML.Logger;");
            WriteLine("using DOML.IR;");
        }

        /// <summary>
        /// Writes the namespace signature.
        /// </summary>
        public void WriteNamespaceSignature()
        {
            WriteIndentLine($"namespace {rootNamespace}");
            WriteIndentLine("{");
            indentLevel++;
        }

        /// <summary>
        /// Writes the closing brace.
        /// </summary>
        /// <remarks> Use this rather than <see cref="Write(string)"/> as it will reflect the change in the indent too. </remarks>
        public void CloseBrace()
        {
            indentLevel--;
            WriteIndentLine("}");
        }

        /// <summary>
        /// Writes the class (its signature, and all its fields, properties, and functions).
        /// </summary>
        /// <param name="classType"> The class type to write. </param>
        public void WriteClass(Type classType)
        {
            // Get all runtime fields/properties/methods/constructors
            IEnumerable<FieldInfo> fieldInfo = classType.GetRuntimeFields();
            IEnumerable<PropertyInfo> propertyInfo = classType.GetRuntimeProperties();
            IEnumerable<MethodInfo> methodInfo = classType.GetRuntimeMethods();
            IEnumerable<ConstructorInfo> constructorInfo = classType.GetTypeInfo().DeclaredConstructors;

            // Allows users to customise this class
            string objectType = classType.GetTypeInfo().GetCustomAttribute<DOMLCustomiseAttribute>()?.Name ?? classType.Name;

            WriteClassSignature(classType.Name);
            bool notInitial = false;

            // Constructors
            foreach (ConstructorInfo info in constructorInfo.Where(x => x.DeclaringType == classType))
            {
                if (notInitial) WriteEmptyLine();
                else notInitial = true;

                WriteConstructor(info, objectType);
            }

            // Fields
            foreach (FieldInfo info in fieldInfo.Where(x => x.DeclaringType == classType))
            {
                WriteEmptyLine();
                WriteField(info, objectType);
            }

            // Properties
            foreach (PropertyInfo info in propertyInfo.Where(x => x.DeclaringType == classType))
            {
                WriteEmptyLine();
                WriteProperty(info, objectType);
            }

            // Methods
            foreach (MethodInfo info in methodInfo.Where(x => x.DeclaringType == classType))
            {
                WriteEmptyLine();
                WriteFunction(info, objectType);
            }

            WriteEmptyLine();

            WriteRegisterCall();

            WriteEmptyLine();

            WriteUnRegisterCall();
            CloseBrace();
        }

        /// <summary>
        /// Writes all the register calls.
        /// </summary>
        public void WriteRegisterCall()
        {
            WriteFunctionSignature("RegisterCalls", string.Empty);
            for (int i = 0; i < registerCalls.Count; i++)
            {
                WriteIndentLine(registerCalls[i]);
            }
            CloseBrace();
        }

        /// <summary>
        /// Writes all the unregister calls.
        /// </summary>
        public void WriteUnRegisterCall()
        {
            WriteFunctionSignature("UnRegisterCalls", string.Empty);
            for (int i = 0; i < unregisterCalls.Count; i++)
            {
                WriteIndentLine(unregisterCalls[i]);
            }
            CloseBrace();
        }

        /// <summary>
        /// Writes the class signature.
        /// </summary>
        /// <param name="name"> The name of the class. </param>
        /// <param name="decorator"> Add decorator ____ and StaticBindings____ to the begin/end. </param>
        public void WriteClassSignature(string name, bool decorator = true)
        {
            WriteIndentLine($"public static partial class {(decorator ? "____" : string.Empty)}{name}{(decorator ? "StaticBindings____" : string.Empty)}");
            WriteIndentLine("{");
            indentLevel++;
        }

        /// <summary>
        /// Writes the function signature.
        /// </summary>
        /// <param name="name"> The name of the function. </param>
        /// <param name="parameters"> All the parameters. </param>
        public void WriteFunctionSignature(string name, string parameters)
        {
            WriteIndentLine($"public static void {name}({parameters})");
            WriteIndentLine("{");
            indentLevel++;
        }

        /// <summary>
        /// Writes the constructor info.
        /// </summary>
        /// <param name="constructorInfo"> TypeInfo of constructor. </param>
        /// <param name="objectType"> The object type. </param>
        public void WriteConstructor(ConstructorInfo constructorInfo, string objectType)
        {
            ParameterInfo[] info = constructorInfo.GetParameters();
            string functionName = "New" + objectType + (info.Length > 0 ? string.Join("", info.Select(x => x.ParameterType.FullName.Replace('.', '_'))) : "Empty");

            WriteFunctionSignature(functionName, "InterpreterRuntime runtime");
            WriteWithIndent($"if (");

            for (int i = 0; i < info.Length; i++)
            {
                Write($"!runtime.Pop(out {info[i].ParameterType.FullName} a{i}) || ");
            }

            if (info.Length > 0)
                Write($"!runtime.Push(new {constructorInfo.DeclaringType.FullName}({string.Join(",", Enumerable.Range(0, info.Length).Select(x => "a" + x))}), true)");
            else
                Write($"!runtime.Push(new {constructorInfo.DeclaringType.FullName}(), true)");

            WriteLine(")");
            indentLevel++;
            WriteIndentLine($"Log.Error(\"{objectType} Constructor failed\");");
            indentLevel--;
            CloseBrace();

            registerCalls.Add($"InstructionRegister.RegisterConstructor(\"{rootNamespace}.{objectType}\", {functionName});");
            unregisterCalls.Add($"InstructionRegister.UnRegisterConstructor(\"{rootNamespace}.{objectType}\");");
        }

        /// <summary>
        /// Writes the function info.
        /// </summary>
        /// <param name="methodInfo"> TypeInfo of constructor. </param>
        /// <param name="objectType"> The object type. </param>
        public void WriteFunction(MethodInfo methodInfo, string objectType)
        {
            ParameterInfo[] info = methodInfo.GetParameters();
            string type = methodInfo.ReturnType == typeof(void) ? "Set" : "Get";
            string functionName = type + methodInfo.Name;

            WriteFunctionSignature(functionName, "InterpreterRuntime runtime");
            WriteWithIndent($"if (!runtime.Pop(out {methodInfo.DeclaringType.FullName} result)");

            for (int i = 0; i < info.Length; i++)
            {
                Write($" || !runtime.Pop(out {info[i].ParameterType.FullName} a{i})");
            }

            if (methodInfo.ReturnType != typeof(void))
            {
                if (info.Length > 0)
                    Write($" || !runtime.Push(result.{methodInfo.Name}({string.Join(",", Enumerable.Range(0, info.Length).Select(x => "a" + x))}), true)");
                else
                    Write($" || !runtime.Push(result.{methodInfo.Name}(), true)");
            }

            WriteLine(")");

            indentLevel++;

            WriteIndentLine($"Log.Error(\"{objectType}::{methodInfo.Name} Function failed\");");

            indentLevel--;

            if (methodInfo.ReturnType == typeof(void))
            {
                WriteIndentLine("else");
                indentLevel++;

                if (info.Length > 0)
                    WriteIndentLine($"result.{methodInfo.Name}({string.Join(", ", Enumerable.Range(0, info.Length).Select(x => "a" + x))});");
                else
                    WriteIndentLine($"result.{methodInfo.Name}();");

                indentLevel--;
            }

            CloseBrace();

            string name = $"{methodInfo.GetCustomAttribute<DOMLCustomiseAttribute>()?.Name ?? methodInfo.Name}";

            if (type == "Get")
            {
                registerCalls.Add($"InstructionRegister.RegisterGetter(\"{name}\", \"{rootNamespace}.{objectType}\", 1, {functionName});");
                unregisterCalls.Add($"InstructionRegister.UnRegisterGetter(\"{name}\", \"{rootNamespace}.{objectType}\");");
            }
            else
            {
                // Set
                registerCalls.Add($"InstructionRegister.RegisterSetter(\"{name}\", \"{rootNamespace}.{objectType}\", {info.Length}, {functionName});");
                unregisterCalls.Add($"InstructionRegister.UnRegisterSetter(\"{name}\", \"{rootNamespace}.{objectType}\");");
            }
        }

        /// <summary>
        /// Writes the property info.
        /// </summary>
        /// <param name="propertyInfo"> TypeInfo of property. </param>
        /// <param name="objectType"> Object Type. </param>
        public void WriteProperty(PropertyInfo propertyInfo, string objectType)
        {
            string name = $"{propertyInfo.GetCustomAttribute<DOMLCustomiseAttribute>()?.Name ?? propertyInfo.Name}";

            if (propertyInfo.CanRead && (propertyInfo.GetMethod?.IsPublic ?? false))
            {
                // Write Getter
                WriteFunctionSignature($"GetProperty{propertyInfo.Name}", "InterpreterRuntime runtime");
                WriteIndentLine($"if (!runtime.Pop(out {propertyInfo.DeclaringType.Name} result) || !runtime.Push(result.{propertyInfo.Name}, true))");
                indentLevel++;
                WriteIndentLine($"Log.Error(\"{objectType}::{name} Getter failed\");");
                indentLevel--;
                CloseBrace();

                registerCalls.Add($"InstructionRegister.RegisterGetter(\"{name}\", \"{rootNamespace}.{objectType}\", {1}, GetProperty{propertyInfo.Name});");
                unregisterCalls.Add($"InstructionRegister.UnRegisterGetter(\"{name}\", \"{rootNamespace}.{objectType}\");");
            }

            if (propertyInfo.CanWrite && (propertyInfo.SetMethod?.IsPublic ?? false))
            {
                WriteEmptyLine();

                // Write Setter
                WriteFunctionSignature($"SetProperty{propertyInfo.Name}", "InterpreterRuntime runtime");
                WriteIndentLine($"if (runtime.Pop(out {propertyInfo.DeclaringType.FullName} result) || !runtime.Pop(out result.{propertyInfo.Name}))");
                indentLevel++;
                WriteIndentLine($"Log.Error(\"{objectType}::{name} Setter failed\");");
                indentLevel--;
                CloseBrace();

                registerCalls.Add($"InstructionRegister.RegisterSetter(\"{name}\", \"{rootNamespace}.{objectType}\", {1}, SetProperty{propertyInfo.Name});");
                unregisterCalls.Add($"InstructionRegister.UnRegisterSetter(\"{name}\", \"{rootNamespace}.{objectType}\");");
            }
        }

        /// <summary>
        /// Writes the field info.
        /// </summary>
        /// <param name="fieldInfo"> TypeInfo of field. </param>
        /// <param name="objectType"> Object Type. </param>
        public void WriteField(FieldInfo fieldInfo, string objectType)
        {
            string name = $"{fieldInfo.GetCustomAttribute<DOMLCustomiseAttribute>()?.Name ?? fieldInfo.Name}";

            // Write Getter
            WriteFunctionSignature($"GetField{name}", "InterpreterRuntime runtime");
            WriteIndentLine($"if (!runtime.Pop(out {fieldInfo.DeclaringType.FullName} result) || !runtime.Push(result.{fieldInfo.Name}, true))");
            indentLevel++;
            WriteIndentLine($"Log.Error(\"{objectType}::{name} Getter failed\");");
            indentLevel--;
            CloseBrace();

            registerCalls.Add($"InstructionRegister.RegisterGetter(\"{name}\", \"{rootNamespace}.{objectType}\", {1}, GetField{fieldInfo.Name});");
            unregisterCalls.Add($"InstructionRegister.UnRegisterGetter(\"{name}\", \"{rootNamespace}.{objectType}\");");

            if (fieldInfo.IsLiteral == false && fieldInfo.IsInitOnly == false)
            {
                WriteEmptyLine();

                // Write Setter
                WriteFunctionSignature($"SetField{fieldInfo.Name}", "InterpreterRuntime runtime");
                WriteIndentLine($"if (runtime.Pop(out {fieldInfo.DeclaringType.FullName} result) || !runtime.Pop(out result.{fieldInfo.Name}))");
                indentLevel++;
                WriteIndentLine($"Log.Error(\"{objectType}::{name} Setter failed\");");
                indentLevel--;
                CloseBrace();

                registerCalls.Add($"InstructionRegister.RegisterSetter(\"{name}\", \"{rootNamespace}.{objectType}\", {1}, SetField{fieldInfo.Name});");
                unregisterCalls.Add($"InstructionRegister.UnRegisterSetter(\"{name}\", \"{rootNamespace}.{objectType}\");");
            }
        }
    }
}
