# Windows only!

# Installation Instructions (For Leadwerks)

  1. Empty the contents of 'DROP_CONTENTS_IN_LE_PROJECT_DIRECTORY' into your LE project's main directory. (I.E: *Documents/Leadwerks/Projects/<ProjectName>/*)
  2. (If not already there.) Open the 'ToLuaPkgGenerator.sln' file with Visual Studio (2017) and compile the program.
  3. Drag 'ToLuaPkgGenerator2.exe' into *Documents/Leadwerks/Projects/<ProjectName>/ToLua/* **(NOT the one inside the 'Source' folder.)**
  4. Open '_toluaGenerate.bat' and ensure all your source code directories are a part of the 'SEARCH_DIR' array, ensure 'SEARCH_DIR.length' is correct.
  5. [Optional] Open '_toluaGenerate.bat' and ensure 'SEARCH_DIR' contains the directory of the of your Leadwerks installation's 'Include' folder.

  >**IMPORTANT NOTICE:** Directories added to the 'SEARCH_DIR' array ***MUST*** not end in a trailing backslash '\' as this would cause the trailing quotation mark to be handled incorrectly.
  
# Usage Instructions (For Leadwerks)

  1. Execute '*_toluaGenerate.bat*' located in your projects main directory.
  2. Open your Leadwerks project in any IDE and add the generated source files to your project. 
  > Your generated source files can be found in '*Documents/Leadwerks/Projects/<ProjectName>/**Source**/ToLua/*'.
  3. Open the generated '.cpp' files (from the directory above) and search for <ChangeMe>.h, replace this with the include(s) required by the generated '.cpp' files as the generator does not (currently) do this for you! 
  4. Include '*Documents/Leadwerks/Projects/<ProjectName>/**Source**/ToLua/tolua_export.h*' in a cpp file and execute the generated 'tolua_<namespace>_pkg_open()' method(s).
  > Note that it is best to execute these '_open()' methods BEFORE the interpreter executes the main lua file. (Usually in main.cpp)
  > Note that classes in namespaces are encased in Lua modules. [i.e: namespace MyNamespace { class MyClass {}; //lua } would be created through Lua via: MyNamespace.MyClass:new()] 

# Extra Feature(s)
> Note that if you edit "_toluaGenerate.bat" see comment on line 32 (i.e: "::FixToLuaNamespaces "%~dp0Source\ToLua\%%~nf.cpp") you may drop the EXE retrieved from https://bitbucket.org/Codeblockz/fixtoluanamespaces in the same directory as ToLuaPkgGenerator.exe to fix namespace related issues.
  
# Supported
 - C++ classes, and classes with single inheritance.
 - C++ struct support.
 - Methods with arguments that have default values specified in the header file. (i.e: void SomeMethod(std::string pValue = "Default!");)
 - Methods with arguments that have default values of types ToLua++ doesn't support! (Converted to nearest supported type in pkg file.)
 - Namespaces that contain classes or methods with the same name. (One pkg file is generated per namespace to avoid ambiguity related issues.)
 - Nested namespaces.
 - Determines start/end of namespace/class scopes to support a wide array of scenarios.
 - Fixed type integers (i.e: uint32_t) are automatically replaced in the pkg files with a type that is guaranteed to be at least the same size. (i.e: uint32_t to long)
 - Enums, can extend over multiple lines if properly enclosed with braces {}. i.e:
 
 enum MyNumbers { //lua
    Num1,  
    Num2
 };
 
# Not supported
 - C++ classes that use multiple inheritance.
 - Method defintions spread amongst multiple lines.
 - Nested classes/structs.
 - (Technically) Unions (ToLua++ itself lacks union support.) However you can expose unions like so (Notice how the C++ code on the last line is commented out.):

 union { float x, r; }
 union { float y, g; }
 union { float z, b; }
 //float x, r, y, g, z, b; //lua
 
 In the future an update may be released to make the generator do this implicitly.
 
 
# Credits
 - ToLuaPkgGenerator2 - Mathew Aloisio
 - ToLua++

# Known Bugs
  > [UNSOLVED] [3c7695] ToLua++ has a bug as follows 
  - Assume we have ClassA and ClassB defined as such:
  > class A {}; 
  > class B { static const int A; };
  - In the above case if class A is defined in the generated '.pkg' file before class B, the member defined as 'static const int A;' will be incorrectly named by ToLua++.

  [3c7695]: <https://bitbucket.org/Codeblockz/toluapkggenerator/commits/3ce7695cb6fdb8b305a8d41876a0caeebc8ef7b3>
  
### Development

Want to contribute? Great!
However, please do not repost your changes on another repository, instead submit them as pull requests.

## ****This is not a product of Leadwerks Software and is in no way affiliated with Leadwerks Software.*** ##