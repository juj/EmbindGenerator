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

                "#include \"embind_prologue.h\" // Implement this file and all required code and header files to compile this file here.\n\n" +

                "EMSCRIPTEN_BINDINGS(([]() {\n\n";
            tw.Write(t);

            for (int i = 1; i < args.Length; ++i)
                WriteForwardDeclaration(args[i]);

            for (int i = 1; i < args.Length; ++i)
                GenerateEmbindFile(args[i], knownSymbolNames);

            t = "\n}));\n\n#endif\n";
            tw.Write(t);
            tw.Flush();
            tw.Close();
        }

        static TextWriter tw = new StreamWriter("embind_symbols.cpp");

        static void WriteForwardDeclaration(string className)
        {
            string t = "class_<" + className + ">(\"" + className + "\")\n";
            t += "\t.constructor<>();\n\n";
            tw.Write(t);
        }

        static void GenerateEmbindFile(string className, List<string> knownSymbolNames)
        {
            string t =
                "//#include <emscripten/bind.h>\n" +
                "//using namespace emscripten;\n\n" +

                "//#include \"embind_prologue.h\" // Implement this file and all required code and header files to compile this file here.\n\n" +

                "//EMSCRIPTEN_BINDINGS(([]() {\n\n";

            t += "class_<"+className+">(\""+className+"\")\n";
            Symbol s = cs.symbolsByName[className];
            foreach(Symbol f in s.children)
            {
                if (f.visibilityLevel != VisibilityLevel.Public)
                    continue; // Only public functions and members are exported.

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

                    string funcPtrType = f.type + "(" + (f.isStatic ? "" : ( className + "::")) + "*)(";
                    bool first = true;
                    string paramList = "";
                    foreach(Parameter p in f.parameters)
                    {
                        if (!first) paramList += ",";
                        first = false;
                        paramList += p.type;
                        if (isGoodSymbol && !knownSymbolNames.Contains(p.BasicType()))
                        {
                            isGoodSymbol = false;
                            reason += "(" + p.BasicType() + " is not known to embind)";
                        }
                    }
                    funcPtrType += paramList + ")";
                    if (f.isConst)
                        funcPtrType += " const";

                    if (isGoodSymbol && f.name == className && f.parameters.Count != 0) ///\todo Remove this line once multiple ctors is supported!
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
                        t += ".constructor<" + paramList + ">()\n";
                    else
                    {
                        if (f.isStatic)
                            t += ".classmethod(";
                        else
                            t += ".method(";
                        t += "\"" + f.name + "\", (" + funcPtrType + ")&" + className + "::" + f.name + ")\n";
                    }
                }
            }
            t += "\t;\n";

            t += "\n//}));\n";

//            TextWriter tw = new StreamWriter(className + "_embind.cpp");
//            TextWriter tw = new StreamWriter(className + "_embind.h");
            tw.Write(t);
//            tw.Flush();
//            tw.Close();
        }

    }
}
