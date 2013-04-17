using System.Xml;
using System.IO;
using System.Web;
using System.Text.RegularExpressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DocGenerator;

namespace EmbindGenerator
{
    class Program
    {
        static CodeStructure cs = new CodeStructure();
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: EmbindGenerator <directory_to_doxygen_xml_docs> [class1 [class2 [class3 ... [classN]]]]");
                return;
            }
            cs.LoadSymbolsFromDirectory(args[0]);

            List<string> knownSymbolNames = new List<string>();
            knownSymbolNames.Add(""); // Typeless 'types', e.g. return value of ctor is parsed to an empty string.
            knownSymbolNames.Add("void");
            knownSymbolNames.Add("bool");
            knownSymbolNames.Add("char");
            knownSymbolNames.Add("signed char");
            knownSymbolNames.Add("unsigned char");
            knownSymbolNames.Add("short");
            knownSymbolNames.Add("signed short");
            knownSymbolNames.Add("unsigned short");
            knownSymbolNames.Add("int");
            knownSymbolNames.Add("signed int");
            knownSymbolNames.Add("unsigned int");
            knownSymbolNames.Add("long");
            knownSymbolNames.Add("signed long");
            knownSymbolNames.Add("unsigned long");
            knownSymbolNames.Add("float");
            knownSymbolNames.Add("double");
            knownSymbolNames.Add("unsigned int");
            knownSymbolNames.Add("std::string");
            knownSymbolNames.Add("emscripten::val");
            for (int i = 1; i < args.Length; ++i)
                knownSymbolNames.Add(args[i]);

            string t =
                "#ifdef EMSCRIPTEN\n\n" +

                "#include <emscripten/bind.h>\n" +
                "using namespace emscripten;\n\n" +

                "#include \"embind_prologue.h\" // Implement this file and all required code and header files to compile this file here.\n\n";
            tw.Write(t);

            js_tw.WriteLine("function RegisterFunctionSelectors() {");

            for (int i = 1; i < args.Length; ++i)
                GenerateCtorFunctions(args[i], knownSymbolNames);

            tw.Write("EMSCRIPTEN_BINDINGS(bindings) /* todo: if we create a separate bindings file for each, generate a unique name here. */ {\n\n");

            for (int i = 1; i < args.Length; ++i)
                WriteForwardDeclaration(args[i]);

            for (int i = 1; i < args.Length; ++i)
                GenerateEmbindFile(args[i], knownSymbolNames);

            tw.Write("\n}\n\n");

            tw.Write("#endif\n");
            tw.Flush();
            tw.Close();

            Console.WriteLine("Writing embind_symbols.cpp done.");

            GenerateTypeIdAssignments();

            js_tw.WriteLine("}");
            js_tw.WriteLine("window['RegisterFunctionSelectors'] = RegisterFunctionSelectors;");

            GenerateIsOfTypeFunctions();
            js_tw.Flush();
            js_tw.Close();
            Console.WriteLine("Writing embind_symbols.js done.");
        }

        static TextWriter tw = new StreamWriter("embind_symbols.cpp");
        static TextWriter js_tw = new StreamWriter("embind_symbols.js");

        static void WriteForwardDeclaration(string className)
        {
//            string t = "auto " + className + "_class = class_<" + className + ">(\"" + className + "\");\n\n";
//            tw.Write(t);
        }

        static List<string> generatedFunctionSelectors = new List<string>();

        static List<Symbol> FetchFunctionOverloads(Symbol function, List<string> knownSymbolNames)
        {
            List<Symbol> functionOverloads = new List<Symbol>();
            foreach (Symbol s in function.parent.children)
                if (s.name == function.name && IsGoodSymbol(s, knownSymbolNames))
                    functionOverloads.Add(s);
            functionOverloads.Sort(delegate(Symbol left, Symbol right)
            {
                return right.parameters.Count - left.parameters.Count;
                /*
                if (left.parameters.Count > right.parameters.Count)
                    return true;
                else if (left.parameters.Count < right.parameters.Count)
                    return false;
                return left.parameters.cou
                 */
            });

            return functionOverloads;
        }

        /// <summary>
        /// Lists the types for which we need to generate type identifier functions for.
        /// </summary>
        static List<string> isOfTypeFunctionsRequired = new List<string>();

        static void GenerateTypeIdAssignments()
        {
            int typeIdCounter = 1;
            foreach (string type in isOfTypeFunctionsRequired)
            {
                if (type == "float" || type == "int")
                    continue;

                js_tw.WriteLine("\tModule." + type + ".prototype.TypeId = " + typeIdCounter + " /* Magic automatically generated TypeId number for "
                        + type + " */");
                ++typeIdCounter;
            }
        }

        static void GenerateIsOfTypeFunctions()
        {
            js_tw.WriteLine("function isNumber(value) {");
            js_tw.WriteLine("\tif ((undefined === value) || (null === value)) {");
            js_tw.WriteLine("\t\treturn false;");
            js_tw.WriteLine("\t}");
            js_tw.WriteLine("\tif (typeof value == 'number') {");
            js_tw.WriteLine("\t\treturn true;");
            js_tw.WriteLine("\t}");
            js_tw.WriteLine("\treturn !isNaN(value - 0);");
            js_tw.WriteLine("}");
            js_tw.WriteLine("");

            int typeIdCounter = 1;
            foreach (string type in isOfTypeFunctionsRequired)
            {
                if (type == "float" || type == "int")
                {
                    js_tw.WriteLine("function IsOfType_" + type + "(obj) { return isNumber(obj); }");
                }
                else
                {
//                    js_tw.WriteLine(type + ".prototype.TypeId = " + typeIdCounter + " /* Magic automatically generated TypeId number for "
//                        + type + " */");
                    js_tw.WriteLine("function IsOfType_" + type + "(obj) { return obj != undefined && obj != null && obj.TypeId == "
                        + typeIdCounter + " /* Magic automatically generated TypeId number for " + type + " */; }");
                    ++typeIdCounter;
                }
//                js_tw.WriteLine("");
            }
        }

        static void GenerateFunctionSelector(List<Symbol> functionOverloads)
        {
            functionOverloads.Sort(delegate(Symbol left, Symbol right)
            {
                return right.parameters.Count - left.parameters.Count;
            });

            Symbol function = functionOverloads.First();

            if (generatedFunctionSelectors.Contains(function.parent.name + "::" + function.name))
                return;
            generatedFunctionSelectors.Add(function.parent.name + "::" + function.name);
//            if (function.name == function.parent.name)
//                return; // TODO: Add support for ctors.

            if (functionOverloads.First().parameters.Count == 0)
                return;

            bool isCtor = (function.name == function.parent.name);
            string prototype;
            if (isCtor)
                prototype = function.name + "_ = function("; ///\todo Add support for REAL ctors.
            else
                prototype = function.parent.name + ".prototype." + function.name + " = function(";
            string paramList = "";
            for (int i = 0; i < functionOverloads.First().parameters.Count; ++i)
            {
                if (i != 0)
                    paramList += ", ";
                paramList += "arg" + (i + 1);
            }
            prototype += paramList + ") {";
            js_tw.WriteLine(prototype);

            for(int i = 0; i < functionOverloads.Count; ++i)
            {
                Symbol thisFunc = functionOverloads[i];
                Symbol nextFunc = (i == functionOverloads.Count - 1) ? null : functionOverloads[i + 1];

                js_tw.Write("\t");
                if (i != 0)
                    js_tw.Write("else ");
                if (nextFunc != null)
                {
                    if (thisFunc.parameters.Count != nextFunc.parameters.Count)
                        js_tw.WriteLine("if (arg" + thisFunc.parameters.Count + " != undefined)");
                    else
                    {
                        js_tw.Write("if (");
                        for (int j = 0; j < thisFunc.parameters.Count; ++j)
                        {
                            if (j != 0)
                                js_tw.Write(" && ");
                            js_tw.Write("IsOfType_" + thisFunc.parameters[j].BasicType() + "(arg" + (j + 1) + ")");
                            if (!isOfTypeFunctionsRequired.Contains(thisFunc.parameters[j].BasicType()))
                                isOfTypeFunctionsRequired.Add(thisFunc.parameters[j].BasicType());
                        }
                        js_tw.WriteLine(")");
                    }
                }
                else
                    js_tw.WriteLine("");
                js_tw.Write("\t\t" + ((thisFunc.type != "void") ? "return " : ""));
                js_tw.Write((isCtor ? "Module." : "this.") + function.name);
                foreach (Parameter p in thisFunc.parameters)
                    js_tw.Write("_" + p.BasicType().Replace(':', '_').Replace('<', '_').Replace('>', '_'));

                paramList = "";
                for (int j = 0; j < thisFunc.parameters.Count; ++j)
                {
                    if (j != 0)
                        paramList += ", ";
                    paramList += "arg" + (j + 1);
                }

                js_tw.WriteLine("(" + paramList + ");");
            }
            js_tw.WriteLine("}");
        }

        static bool IsGoodSymbol(Symbol s, List<string> knownSymbolNames)
        {
            if (s.attributes.Contains("noembind"))
                return false;
            if (!knownSymbolNames.Contains(s.type))
                return false;
            foreach(Parameter p in s.parameters)
                if (!knownSymbolNames.Contains(p.BasicType()))
                    return false;
            return true;
        }

        static void GenerateEmbindFile(string className, List<string> knownSymbolNames)
        {
            if (!cs.symbolsByName.ContainsKey(className))
            {
                Console.WriteLine("Error: Cannot generate bindings for class '" + className + "', XML docs for that class don't exist!");
                return;
            }
            string t =
                "//#include <emscripten/bind.h>\n" +
                "//using namespace emscripten;\n\n" +

                "//#include \"embind_prologue.h\" // Implement this file and all required code and header files to compile this file here.\n\n" +

                "//EMSCRIPTEN_BINDINGS(" + className + ") { \n\n";

            bool hasCtorExposed = false; // Embind only supports exposing one ctor, so pick the first one.

            t += "class_<"+className+">(\""+className+"\")\n";
//            t += className + "_class\n";
            Symbol s = cs.symbolsByName[className];
            foreach(Symbol f in s.children)
            {
                if (f.visibilityLevel != VisibilityLevel.Public)
                    continue; // Only public functions and members are exported.

                List<Symbol> functionOverloads = FetchFunctionOverloads(f, knownSymbolNames);
                bool isGoodSymbol = !f.attributes.Contains("noembind"); // If true, this symbol is exposed. If false, this symbol is not enabled for JS.
                string reason = f.attributes.Contains("noembind") ? "(ignoring since [noembind] specified)" : "";
                if (f.kind == "function" && !f.name.StartsWith("operator"))
                {
                    bool isCtor = f.name == className;

                    if (isGoodSymbol && !knownSymbolNames.Contains(f.type))
                    {
                        isGoodSymbol = false;
                        reason += "(" + f.type + " is not known to embind)";
                    }

                    string targetFunctionName = f.name; // The JS side name with which this function will be exposed.
                    bool hasOverloads = (functionOverloads.Count > 1);

                    string funcPtrType = f.type + "(" + (f.isStatic ? "" : ( className + "::")) + "*)(";
                    bool first = true;
                    string paramList = "";
                    foreach(Parameter p in f.parameters)
                    {
                        if (!first) paramList += ",";
                        paramList += p.type;
                        if (isGoodSymbol && !knownSymbolNames.Contains(p.BasicType()))
                        {
                            isGoodSymbol = false;
                            reason += "(" + p.BasicType() + " is not known to embind)";
                        }
                        if (hasOverloads)
                        {
                            targetFunctionName += "_" + p.BasicType().Replace(':', '_').Replace('<', '_').Replace('>', '_');
                        }
                        first = false;
                    }
                    funcPtrType += paramList + ")";
                    if (f.isConst)
                        funcPtrType += " const";

                    if (isGoodSymbol && f.name == className && hasCtorExposed) ///\todo Remove this line once multiple ctors is supported!
                    {
                        isGoodSymbol = false;
                        reason = "(Multiple constructors not yet supported by embind!)";
                    }
                    if (isGoodSymbol && !f.isStatic && !isCtor && f.type != "void" && !f.isConst && f.parameters.Count <= 2)
                    {
                        isGoodSymbol = false;
                        reason = "(Non-void-returning, non-const functions not yet supported by embind!)";
                    }

                    if (!isGoodSymbol)
                        t += "// /*" + reason + "*/ ";

                    t += "\t";

                    if (isCtor)
                    {
                        t += ".constructor<" + paramList + ">()\n";
                        if (isGoodSymbol)
                            hasCtorExposed = true;
                    }
                    else
                    {
                        if (f.isStatic)
                            t += ".class_function(";
                        else
                            t += ".function(";
                        t += "\"" + targetFunctionName + "\", (" + funcPtrType + ")&" + className + "::" + f.name + ")\n";
                    }

                    if (hasOverloads && isGoodSymbol)
                        GenerateFunctionSelector(functionOverloads);
                }
                else if (f.kind == "variable" && f.visibilityLevel == VisibilityLevel.Public)
                {
                    if (!knownSymbolNames.Contains(f.type))
                        t += "// /* " + f.type + " is not known to embind. */ ";
                    else if (f.IsArray())
                        t += "// /* Exposing array types as fields are not supported by embind. */ ";
                    else if (f.isStatic)
                        t += "// /* Exposting static class variables not yet implemented (are they supported?) */ ";
                    t += "\t";
                    t += ".property(\"" + f.name + "\", &" + className + "::" + f.name + ")\n";
                }
            }
            t += "\t;\n";

            t += "\n//}\n";

            RegisterCtorFunctions(className, knownSymbolNames);

//            TextWriter tw = new StreamWriter(className + "_embind.cpp");
//            TextWriter tw = new StreamWriter(className + "_embind.h");
            tw.Write(t);
//            tw.Flush();
//            tw.Close();
        }

        static void GenerateCtorFunctions(string className, List<string> knownGoodSymbols)
        {
            Symbol s = cs.symbolsByName[className];

            List<Symbol> ctors = new List<Symbol>();
            foreach (Symbol f in s.children)
            {
                if (f.name == s.name)
                {
                    bool isGoodCtor = true;
                    foreach (Parameter p in f.parameters)
                        if (!knownGoodSymbols.Contains(p.BasicType()))
                        {
                            isGoodCtor = false;
                            break;
                        }

                    if (f.parameters.Count == 0) // 0-parameter ctors are created with 'new type();'
                        isGoodCtor = false;

                    if (!isGoodCtor)
                        continue;

                    ctors.Add(f);

                    tw.Write(className + " " + className);
                    foreach (Parameter p in f.parameters)
                    {
                        tw.Write("_");
                        tw.Write(p.BasicType());
                    }
                    if (f.parameters.Count == 0)
                        tw.Write("_");
                    tw.WriteLine(f.ArgStringWithTypes() + " { return " + className + f.ArgStringWithoutTypes() + "; }");
                }
            }

            tw.WriteLine("");
//            js_tw.WriteLine(className + " = Module." + className +";");
            js_tw.WriteLine("window['" + className + "'] = Module." + className + ";");
            if (ctors.Count > 1)
            {
                GenerateFunctionSelector(ctors);
                js_tw.WriteLine("window['" + className + "_'] = " + className + "_;");
            }


        }

        static void RegisterCtorFunctions(string className, List<string> knownGoodSymbols)
        {
            Symbol s = cs.symbolsByName[className];

            foreach (Symbol f in s.children)
            {
                if (f.name == s.name)
                {
                    bool isGoodCtor = true;
                    foreach (Parameter p in f.parameters)
                        if (!knownGoodSymbols.Contains(p.BasicType()))
                        {
                            isGoodCtor = false;
                            break;
                        }

                    if (f.parameters.Count == 0) // 0-parameter ctors are created with 'new type();'
                        isGoodCtor = false;

                    if (!isGoodCtor)
                        continue;

                    string t = "";
                    t += className;
                    foreach (Parameter p in f.parameters)
                    {
                        t += "_";
                        t += p.BasicType();
                    }

                    tw.WriteLine("function(\"" + t + "\", &" + t + ");");
                }
            }
        }

    }
}
