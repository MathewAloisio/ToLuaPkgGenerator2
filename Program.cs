using System;
using System.IO;
using System.Collections.Generic;
#if DEBUG
using System.Diagnostics;
#endif

namespace ToLuaPkgGenerator2 {
    class CClass {
        public string name;
        public string prefix;
        public string parentName;
        public CClass parent = null;
        public List<string> members = new List<string>();

        private static readonly Dictionary<string, string> _bannedTypes = new Dictionary<string, string> {
            { "GetEntryUserID_", "GetEntryUserID_ @ GetEntryUserID" },
            { "GetUserID_", "GetUserID_ @ GetUserID" },
            { "size_t", "long long" },
            { "uint8_t", "unsigned char" },
            { "uint16_t", "unsigned short" },
            { "uint32_t", "unsigned int" },
            { "uint64_t", "unsigned long long" },
            { "int8_t", "char" },
            { "int16_t", "short" },
            { "int32_t", "int" },
            { "int64_t", "long long" }
        };
        public static Dictionary<string, CClass> flatClasses = new Dictionary<string, CClass>();

        static public CClass Find(string pName) {
            // FLAW: Flat nature of the dictionary causes ambiguity issues w/ same-named classes from different namespaces.
            return flatClasses.ContainsKey(pName) ? flatClasses[pName] : null;
        }

        private bool written = false;
        public void Write(StreamWriter pStream, string pPadding) {
            if (written)
                return;

            // Write base classes first.
            CClass baseClass = parent;
            while (baseClass != null) {
                baseClass.Write(pStream, pPadding);
                baseClass = baseClass.parent;
            }

            // Write class header.
            if (parentName.Length != 0) {
                pStream.WriteLine(pPadding + prefix + name + " : public " + parentName + " {");
            }
            else { pStream.WriteLine(pPadding + prefix + name + " {"); }

            // Write class members.
            foreach (var _member in members) {
                string member = _member;
                foreach (var _type in _bannedTypes.Keys) {
                    if (member.Contains(_type))
                        member = member.Replace(_type, _bannedTypes[_type]);          
                }
                pStream.WriteLine(pPadding + "\t" + member);
            }
            pStream.WriteLine(pPadding + "};" + Environment.NewLine);

            written = true;
        }
    }

    class NamespaceClass {
        public string name;
        public bool empty = true;
        public int endingLine = -1;
        public int searchingEnd = 0;
        public bool searchingNSStart = false;

        public NamespaceClass parent = null;
        public List<CClass> classes = new List<CClass>();
        public List<string> members = new List<string>();
        public Dictionary<string, NamespaceClass> namespaces = new Dictionary<string, NamespaceClass>();

        public static List<NamespaceClass> namespacesSeekingEnd = new List<NamespaceClass>();

        public NamespaceClass(string pName = "") { name = pName; }
    }

    class Program {
        static private CClass inClass = null;
        static private NamespaceClass globalNamespace = new NamespaceClass();
        static private NamespaceClass currentNamespace = globalNamespace;
        static private List<string> generatedFiles = new List<string>();

        // READ
        static public void ReadHeaders(string pDir) {
            var files = Directory.GetFiles(pDir, "*.h", SearchOption.AllDirectories);
            foreach (var file in files) {
                ParseHeader(file);
            }
        }

        static public void ParseHeader(string pPath) {
            int lineNumber = 0;
            try {
                using (var stream = new StreamReader(File.OpenRead(pPath))) {
#if DEBUG
                    Console.WriteLine("Parsing header file \"{0}\"", pPath);
#endif
                    currentNamespace = globalNamespace; // Reset to global namespace at the start of each file.
                    while(stream.Peek() >= 0) {
                        ParseLine(stream.ReadLine().Trim(), lineNumber);
                        ++lineNumber;
                    }
                }
            }
            catch (Exception exception) {
                Console.WriteLine("EXCEPTION:" + exception);
#if DEBUG
                StackTrace trace = new StackTrace(exception, true);
                Console.WriteLine("\tFILE:" + trace.GetFrame(0).GetFileName());
                Console.WriteLine("\tLINE: " + trace.GetFrame(0).GetFileLineNumber());
#endif
            }
        }

        static public void ParseLine(string pLine, int pLineNumber) {
            var subStrings = pLine.Split(' ');
            CheckNamespace(pLine, pLineNumber, subStrings);
            CheckClassAndMembers(pLine, pLineNumber, subStrings);
        }

        static public void CheckNamespace(string pLine, int pLineNumber, string[] pSubStrings) { //TODO: fix me, breaks when ns changes b4 ends found.
            if (!pLine.Contains("using") && pLine.Contains("namespace")) {
                if (!currentNamespace.namespaces.ContainsKey(pSubStrings[1])) // Only create new namespaceclass if one doesn't exist.
                    currentNamespace.namespaces[pSubStrings[1]] = new NamespaceClass(pSubStrings[1]);

                currentNamespace = currentNamespace.namespaces[pSubStrings[1]];
                if (!pLine.Contains("{")) {
                    currentNamespace.searchingNSStart = true; // We're now searching for the namespace's opening bracket. (i.e: '{')
                }
                else { currentNamespace.searchingEnd = 1; }
                NamespaceClass.namespacesSeekingEnd.Add(currentNamespace);
#if DEBUG
                Console.WriteLine("Found namespace-start \"" + pSubStrings[1] + "\". Searching for end!");
#endif
            }

            for (int i = NamespaceClass.namespacesSeekingEnd.Count - 1; i >= 0; --i) {
                var ns = NamespaceClass.namespacesSeekingEnd[i];
                if (ns.searchingNSStart) {
                    if (pLine.Contains("{")) {
                        ns.searchingNSStart = false;
                        ns.searchingEnd = 1;
                    }
                }
                else if (pLine.Contains("{")) { // We've found a opening bracket, not belonging to the namespace.
                    if (!pLine.Contains("}")) // Bracket isn't closed on same line.
                        ++ns.searchingEnd; // One more opening bracket to end!
                }
                else if (ns.searchingEnd > 0 && pLine.Contains("}")) {
                    --ns.searchingEnd;
                    if (ns.searchingEnd < 1) {
                        ns.searchingEnd = 0;
                        if (ns.parent != null)
                            currentNamespace = ns.parent;
                        NamespaceClass.namespacesSeekingEnd.RemoveAt(i);
#if DEBUG
                        Console.WriteLine("Found namespace-end \"" + ns.name + "\"! Line: " + pLineNumber);
#endif
                    }
                }
            }
        }

        static private void _tagNamespaceNotEmpty(NamespaceClass pNamespace) {
            NamespaceClass ns = pNamespace;
            while (ns != null) {
                ns.empty = false;
                ns = ns.parent;
            }
        }

        static public void CheckClassAndMembers(string pLine, int pLineNumber, string[] pSubStrings) {
            int commentIndex = pLine.IndexOf("//");
            int luaIndex = pLine.IndexOf("lua");
            if (commentIndex != -1 && luaIndex != -1 && luaIndex > commentIndex) {
                string line = pLine.Substring(0, commentIndex);
                var lineStrings = line.Split(' ');
                bool containsClass = line.Contains("class") ? true : false;
                if (containsClass || line.Contains("struct")) {
                    currentNamespace.classes.Add(new CClass());
                    inClass = currentNamespace.classes[currentNamespace.classes.Count - 1];
                    inClass.name = lineStrings[1];
                    inClass.prefix = containsClass ? "class " : "struct ";
                    inClass.parentName = lineStrings.Length > 4 ? lineStrings[4].Trim() : "";

                    if (currentNamespace.empty)
                        _tagNamespaceNotEmpty(currentNamespace);
                }
                else {
                    if (inClass != null) {
                        inClass.members.Add(line);
                    }
                    else { currentNamespace.members.Add(line); }

                    if (currentNamespace.empty)
                        _tagNamespaceNotEmpty(currentNamespace);
                }
            }
        }

        // WRITE
        static public void WriteFiles() {
            try {
                string packageFilePath = "_pkg.pkg";
                if (File.Exists(packageFilePath))
                    File.Delete(packageFilePath);
                using (var stream = new StreamWriter(File.OpenWrite(packageFilePath))) {
                    string _name = Path.GetFileNameWithoutExtension(packageFilePath);
                    generatedFiles.Add(_name);
                    stream.WriteLine("$/* Add includes here. */" + Environment.NewLine + "$#include \"<CHANGEME>.h\"" + Environment.NewLine);

                    // Write global members.
                    foreach (var member in globalNamespace.members) {
                        stream.WriteLine(member);
                    }

                    // Write global namespace classes.
                    foreach (var obj in globalNamespace.classes) {
                        obj.Write(stream, "");
                    }

                    // Write sub-namespaces.
                    foreach (var pair in globalNamespace.namespaces) {
                        WriteNamespace(pair.Key, pair.Value);
                    }
                }
            }
            catch (Exception exception) {
                Console.WriteLine("EXCEPTION:" + exception);
#if DEBUG
                StackTrace trace = new StackTrace(exception, true);
                Console.WriteLine("\tFILE:" + trace.GetFrame(0).GetFileName());
                Console.WriteLine("\tLINE: " + trace.GetFrame(0).GetFileLineNumber());
#endif
            }
        }

        static private void _loopNamespace(ref List<string> pOut, string pPrefix, Dictionary<string, NamespaceClass> pNamespaces) {
            foreach (var pair in pNamespaces) {
                pOut.Add("$using namespace " + pPrefix + pair.Key + ";");

                if (pair.Value.namespaces.Count > 0) {
                    string nestedPrefix = pPrefix + pair.Key + "::";
                    _loopNamespace(ref pOut, nestedPrefix, pair.Value.namespaces);
                }
            }
        }

        static private string _formatUsingStatements(string pPrefix, Dictionary<string, NamespaceClass> pNamespaces) {
            List<string> _out = new List<string>();
            _loopNamespace(ref _out, pPrefix, pNamespaces);
            
            return string.Join(Environment.NewLine, _out) + Environment.NewLine;
        }

        static private void _WriteNS(StreamWriter pStream, string pName, NamespaceClass pNamespace, string pPadding) {
            // Write module tag.
            pStream.WriteLine(pPadding + "module " + pName + " {" + Environment.NewLine);

            // Write members.
            foreach (var member in pNamespace.members) {
                pStream.WriteLine(pPadding + "\t" + member);
            }

            // Write classes.
            foreach (var obj in pNamespace.classes) {
                obj.Write(pStream, pPadding + "\t");
            }

            // Write sub-namespaces.
            foreach (var pair in pNamespace.namespaces) {
                WriteNamespace(pair.Key, pair.Value, pStream, pPadding + "\t\t");
            }

            pStream.WriteLine(pPadding + "}");

            // Remove a single tab from padding.
            int lastTab = pPadding.LastIndexOf('\t');
            pPadding = pPadding.Substring(0, lastTab == -1 ? pPadding.Length : lastTab);
        }

        static public void WriteNamespace(string pName, NamespaceClass pNamespace, StreamWriter pStream = null, string pPadding = "") {
            if (pNamespace.empty) {// Ignore empty namespace(s).
                Console.WriteLine();
                return;
            }

            if (pStream == null) { // Create file.
                try {
                    string filePath = pName + "_pkg.pkg";
                    if (File.Exists(filePath))
                        File.Delete(filePath);
                    using (var stream = new StreamWriter(File.OpenWrite(filePath))) {
                        generatedFiles.Add(pName);
                        stream.WriteLine("$/* Add includes here. */" + Environment.NewLine + "$#include \"<CHANGEME>.h\"");
                        stream.WriteLine("$using namespace " + pName + ";");
                        stream.WriteLine(_formatUsingStatements(pName + "::", pNamespace.namespaces));

                        _WriteNS(stream, pName, pNamespace, pPadding);
                    }
                }
                catch (Exception exception) {
                    Console.WriteLine("EXCEPTION:" + exception);
#if DEBUG
                    StackTrace trace = new StackTrace(exception, true);
                    Console.WriteLine("\tFILE:" + trace.GetFrame(0).GetFileName());
                    Console.WriteLine("\tLINE: " + trace.GetFrame(0).GetFileLineNumber());
#endif
                }
            }
            else { // Write to stream.
                _WriteNS(pStream, pName, pNamespace, pPadding);
            }
        }

        static void Main(string[] pArgs) {
            string directory = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location) + "/";
            Console.WriteLine("Generating pkg files...");

            // Read all header files and gather class information.
            ReadHeaders(directory);
            foreach (string argument in pArgs) {
                if (!Directory.Exists(argument)) {
                    Console.WriteLine("ERROR: Invalid directory \"" + argument + "\". Skipping directory.");
                    continue;
                }  
                ReadHeaders(argument);
            }

            // Assign parents to classes.
            foreach (var obj in CClass.flatClasses) {
                if (obj.Value.name.Length != 0) {
                    obj.Value.parent = CClass.Find(obj.Value.name);
                    continue;
                }
            }

            // Write pkg files.
            WriteFiles();

            // Write export header.
            string headerFilePath = "tolua_export.h";
            if (File.Exists(headerFilePath))
                File.Delete(headerFilePath);
            try {
                using (StreamWriter stream = new StreamWriter(File.Create(headerFilePath))) {
                    stream.WriteLine("// ToLua++ Export Header");
                    stream.WriteLine("// *This file was generated automatically by ToLuaPkgGenerator2 by Mathew Aloisio.*");
                    stream.WriteLine("#ifndef _TOLUA_EXPORT_HEADER_");
                    stream.WriteLine("#define _TOLUA_EXPORT_HEADER_");
                    stream.WriteLine("#pragma once" + Environment.NewLine);
                    stream.WriteLine("#include \"tolua++.h\"" + Environment.NewLine);
                    foreach (var fileName in generatedFiles) {
                        stream.WriteLine("TOLUA_API int  tolua_" + fileName + "_pkg_open (lua_State* tolua_S);");
                    }
                    stream.WriteLine("#endif");
                }
            }
            catch (Exception exception) {
                Console.WriteLine("EXCEPTION:" + exception);
#if DEBUG
                StackTrace trace = new StackTrace(exception, true);
                Console.WriteLine("\tFILE:" + trace.GetFrame(0).GetFileName());
                Console.WriteLine("\tLINE: " + trace.GetFrame(0).GetFileLineNumber());
#endif
            }
        }
    }
}
