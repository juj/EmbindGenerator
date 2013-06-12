EmbindGenerator
===============

This tool generates C++11 code files for exposing C/C++ types over to JavaScript with Emscripten and Embind.

## Installation

1. Install Visual Studio 2010 for C#. Express should be fine. VS 2012 for Desktop should be fine as well. VS2012 for Win8 (Metro) does not work.
2. Create a directory for EmbindGenerator, and clone the following two repositories in sibling directories from command line with:
   - git clone https://github.com/juj/CodeStructure.git
   - git clone https://github.com/juj/EmbindGenerator.git

   As a result, you should have e.g. folders \generator\CodeStructure and \generator\EmbindGenerator.
3. Go to \generator\EmbindGenerator and open and build the solution EmbindGenerator.sln. This solution expects to find the CodeStructure project appropriately in a sibling directory like described above.
4. Add \generator\EmbindGenerator\bin\\(Debug or Release) to system PATH if you want to be able to run 'EmbindGenerator.exe' from command line. Alternative approach is to create a .bat file in your project folder with absolute paths to the generator exe.
5. Download and install Doxygen from http://www.stack.nl/~dimitri/doxygen/ .
6. After installation, add doxygen bin folder to system PATH if you want to be able to run 'doxygen.exe' from command line. Alternatively, you will need to use the absolute path to where you installed doxygen to in later steps.

The guide below assumes both 'EmbindGenerator.exe' and 'doxygen.exe' are in PATH. If not, substitute the commands with absolute paths to these tools.

## Preparing a project for EmbindGenerator

This section must be performed once for the new project that contains types that are to be exposed to JavaScript with embind.

1. Start up Doxywizard from doxygen Start Menu folder.
2. Create a new Doxyfile for your project, set up general info as you please: name of your project, source code directories for symbols to export, and so on. This step can liberally point to all C/C++ code files in your project. The actual types to export are chosen below.
3. In the Expert tab of the project, find the "XML" topic, and <b>enable</b> the <b>GENERATE_XML</b> checkbox.
4. (Optional) In the LaTeX topic of the project, you can uncheck the GENERATE_LATEX checkbox, since this is unneeded for EmbindGenerator.
5. (Optional) In the HTML topic of the project, you can uncheck the GENERATE_HTML checkbox, since this is unneeded for EmbindGenerator.
6. Save the doxygen run configuration file as 'Doxyfile' in the directory where your project resides in.
## Generating Embind code files for your Emscripten project

To generate the C/C++ -> JS interop registration code, perform the steps in this section. These steps must also be rerun always whenever you change the public .cpp/.h code interface of your C/C++ types, so that the generated embind registration code will get updated for those types.

1. In the directory of your project, run '<b>doxygen.exe Doxyfile</b>'. After doxygen finishes, you should have a folder xml\ that contains an xml file for each class you want to expose. If the xml files do not appear, use Doxywizard to set up the proper lookup paths to your project .cpp and .h files, and to check that the output xml\ folder is correct.
2. In the directory of your project, run '<b>EmbindGenerator.exe path_to_xml_directory class1 \[class2\] ... \[classN\]</b>', where path_to_xml_directory is a relative or an absolute path to the directory where doxygen generated all the .xml output files, and the parameters class1 to classN are the names of C++ classes, structures or symbols you want to expose to Javascript.
3. Create a new file "<b>embind_prologue.h</b>" in the same directory the file "<b>embind_symbols.cpp</b>" was generated to. Manually add #include directives to that file as necessary to make embind_symbols.cpp build properly.

After running EmbindGenerator.exe, the current directory should contain two new files 'embind_symbols.cpp' and 'embind_symbols.js', which contain all the necessary registration code for the C/C++ types that you specified.

## Building an Emscripten project with the generated interop files

After generating the embind bindings files for your C++ types, you must integrate the EmbindGenerator output files to your Emscripten project, and (re)build your codebase with Emscripten. To achieve that:

1. Add the embind_symbols.cpp file into your project to be built along with the rest of your project .cpp files. This step depends on whatever build architecture you are using. The file embind_symbols.cpp <b>must</b> be built with the <b>--bind</b> command line option enabled for Emscripten. Also, you may <b>not</b> specify a <b>--std=</b> directive to em++/clang in the invocation, since using --bind will override the C++11 standard to be used for the build of that file.
2. In the Emscripten .html/.js link stage, add the <b>--bind</b> option. This will instruct Emscripten that it needs to link in important internal .js files related to embind. If you are missing embind/emval-related symbols at runtime, you may have forgotten this step.
3. Add a linker option <b>--pre-js embind_symbols.js</b> to the Emscripten .html/.js link stage. This will add the EmbindGenerator-generated .js bindings file to the final output.

When the build finishes and you run your application in the browser, the types should be available under the Emscripten module '<b>Module</b>'. For example, the C++ class <b>Matrix</b> would be accessible from Javascript via the name <b>Module.Matrix</b>. You can test that the types were exposed properly by accessing them from the live JS interpreter in your web browser developer console.
