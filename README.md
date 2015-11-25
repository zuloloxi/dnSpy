dnSpy
=====

dnSpy is a .NET assembly editor, decompiler, and debugger forked from
[ILSpy](https://github.com/icsharpcode/ILSpy).

License: GPLv3

![pic1](https://cloud.githubusercontent.com/assets/1060731/10716326/9cfbc9ec-7b35-11e5-9024-0e286700335f.png)
![pic2](https://cloud.githubusercontent.com/assets/1060731/10712444/bf69a66a-7a9b-11e5-8039-0c3c2ea4ffb1.png)
![pic3](https://cloud.githubusercontent.com/assets/1060731/10716327/9cfc9d4a-7b35-11e5-831c-e63730d3c834.png)

Binaries
========
Latest release: https://github.com/0xd4d/dnSpy/releases

Latest build (possibly unstable): https://ci.appveyor.com/project/0xd4d/dnspy/build/artifacts

Features
========

* Assembly editor
* Decompiler
* Debugger
* Tabs and tab groups
* Themes (blue, dark, light and high contrast)

Themes
======

dnSpy looks for *.dntheme files in the `<dnSpy-bin-dir>\dntheme` directory
and the user's `%APPDATA%\dnSpy\dntheme` directory. If you wish to override a
standard theme, copy the file to `%APPDATA%\dnSpy\dntheme` and edit the file.

Keyboard shortcuts
==================

Key | Description
--- | -----------
Ctrl+F		| (Text view) Search
F3			| (Text view) Find next match
Shift+F3	| (Text view) Find previous match
ESC			| (Text view) Remove selected markers or close search box
Backspace	| Navigate back in history
Alt+Left Arrow | Navigate back in history
Alt+Right Arrow | Navigate forward in history
F5			| (Debugger) Debug an assembly
Ctrl+F5		| (Debugger) Start without debugging
F5			| (Debugger) Continue debugging
Shift+F5	| (Debugger) Stop debugging
Ctrl+Shift+F5 | (Debugger) Restart debugged program
F9			| (Text view) Toggle breakpoint at caret
Ctrl+F9		| (Text view) Toggle enable/disable breakpoint at caret
Ctrl+Shift+F9 | Delete all breakpoints
F10			| (Debugger) Step over
Ctrl+Shift+F10 | (Debugger) Set next statement
F11			| (Debugger) Step into next method
Shift+F11	| (Debugger) Step out of current method
Ctrl+Pause	| (Debugger) Break
Alt+*		| (Debugger) Show next statement
Ctrl+D		| (Text view) Go to token
Ctrl+Shift+D| (Text view) Go to MD table row
Shift+Alt+R	| (Text view) Go to highlighted reference's MD table row
Ctrl+G		| (Text view) Go to line
Ctrl+X		| (Text view) Show current instruction in hex editor or open hex editor
Ctrl+T		| Open a new tab
Ctrl+W		| Close current tab
Ctrl+F4		| Close current tab
Ctrl+Tab	| Go to next tab
Ctrl+Shift+Tab | Go to previous tab
Ctrl+K		| Open search pane
Ctrl+T		| (Search pane) Select Type
Ctrl+M		| (Search pane) Select Member
Ctrl+S		| (Search pane) Select Literal
Alt+Enter	| (Text view, Tree view) Edit current type, method, field, etc
F12			| (Text view) Follow reference at caret
Enter		| (Text view) Follow reference at caret
Ctrl+F12	| (Text view) Follow reference at caret in a new tab
Ctrl+Enter	| (Text view) Follow reference at caret in a new tab
Ctrl+Click	| (Text view) Follow the clicked reference in a new tab
Ctrl+Alt+W	| (Text view) Toggle word wrap
Shift+Dbl Click| (BP/Call stack/Search/etc windows) Open BP/method/etc in a new tab
Ctrl+C		| (Text view) Copy selected text
Ctrl+B		| (Text view, IL language) Copy selected lines as IL hex bytes
Ctrl+E		| (Text view) Edit IL instructions
Ctrl+S		| Save code
Ctrl+Shift+S| Save all modified assemblies and netmodules
Ctrl+O		| Open assembly
Ctrl+Z		| (Assembly Editor) Undo
Ctrl+Y		| (Assembly Editor) Redo
Ctrl+Shift+Z| (Assembly Editor) Redo
Ctrl++		| (Text view) Zoom In
Ctrl+-		| (Text view) Zoom Out
Ctrl+0		| (Text view) Zoom Reset
Ctrl+Scroll Wheel| (Text view) Zoom In/Out
Alt+Click	| (Text view) Don't follow the clicked reference so it's possible to start selecting text without being taken to the definition. References are only followed if none of Ctrl, Alt and Shift are pressed or if Ctrl is pressed.
F7			| Give text editor keyboard focus
Ctrl+Alt+0	| Give text editor keyboard focus
Ctrl+Alt+L	| Give tree view keyboard focus
Ctrl+Alt+B	| (Debugger) Show Breakpoints window
Alt+F9		| (Debugger) Show Breakpoints window
Ctrl+Alt+C	| (Debugger) Show Call Stack window
Alt+7		| (Debugger) Show Call Stack window
Ctrl+Alt+H	| (Debugger) Show Threads window
Ctrl+Alt+U	| (Debugger) Show Modules window
Ctrl+Alt+E	| (Debugger) Show Exceptions window
Alt+4		| (Debugger) Show Locals window
Alt+6		| (Debugger) Show Memory 1 window
Ctrl+Alt+1	| (Debugger) Show Memory 1 window
Ctrl+Alt+2	| (Debugger) Show Memory 2 window
Ctrl+Alt+3	| (Debugger) Show Memory 3 window
Ctrl+Alt+4	| (Debugger) Show Memory 4 window
Shift+Alt+Enter | Toggle full screen mode
Tab			| (Text view) Move to the next reference. Does nothing if the caret is not on a reference.
Shift+Tab	| (Text view) Move to the previous reference. Does nothing if the caret is not on a reference.
N			| (Method Editor) Nop instruction
I			| (Method Editor) Invert branch
B			| (Method Editor) Convert to unconditional branch
P			| (Method Editor) Remove instruction and add an equal number of pops that the original instruction popped
S			| (Method Editor) Simplify instructions, eg. convert ldc.i4.8 to ldc.i4 with 8 as operand
O			| (Method Editor) Optimize instructions, eg. convert ldc.i4 with 8 as operand to ldc.i4.8
F			| (Method Editor) Add a new instruction before selection
C			| (Method Editor) Add a new instruction after selection
A			| (Method Editor) Append a new instruction
U			| (Method Editor) Move selection up
D			| (Method Editor) Move selection down
Del			| (Method Editor) Remove selected instructions
Ctrl+Del	| (Method Editor) Remove all instructions
Ctrl+T		| (Method Editor) Copy selection as text
Ctrl+X		| (Method Editor) Cut selected instructions
Ctrl+C		| (Method Editor) Copy selected instructions
Ctrl+V		| (Method Editor) Paste instructions
Ctrl+Alt+V	| (Method Editor) Paste instructions after selection
Ctrl+M		| (Method Editor) Copy operand's MD token
Ctrl+R		| (Method Editor) Copy RVA of instruction
Ctrl+F		| (Method Editor) Copy file offset of instruction
Ctrl+R		| (Text view) Analyze reference at caret
Tab			| (Hex editor) Switch caret from hex bytes to ASCII or back
Ctrl+C		| (Hex editor) Copy binary data
Ctrl+Shift+8| (Hex editor) Copy UTF-8 encoded data
Ctrl+Shift+U| (Hex editor) Copy Unicode encoded data
Ctrl+Shift+P| (Hex editor) Copy data as a C# array
Ctrl+Shift+B| (Hex editor) Copy data as a VB array
Ctrl+Shift+C| (Hex editor) Copy hex editor screen contents
Ctrl+Alt+O	| (Hex editor) Copy offset
Ctrl+G		| (Hex editor) Go to offset
Ctrl+8		| (Hex editor) Paste (UTF-8)
Ctrl+U		| (Hex editor) Paste (Unicode)
Ctrl+B		| (Hex editor) Paste (#Blob data with compressed length)
Ctrl+L		| (Hex editor) Show 'Select' dialog box
Ctrl+D		| (Hex editor) Show only the selected bytes
Ctrl+Shift+D| (Hex editor) Show all bytes
Ctrl+Alt+S	| (Hex editor) Save selection

Credits
=======

For license info, authors and other credits, see README.txt.

Build instructions
==================

First grab the code using `git`:

```sh
git clone https://github.com/0xd4d/dnSpy.git
cd dnSpy
git submodule update --init --recursive
```

Use Visual Studio 2010 or later or run `debugbuild.bat` / `releasebuild.bat`
to build it once you have all dependencies. You probably don't need Visual Studio
installed to run the `*.bat` files. The C# compiler is usually installed if you
have the .NET Framework installed. Otherwise, download and install `Microsoft Build Tools`.

Dependencies
============

The `git` command above should've downloaded the correct versions. If you
can't use `git`, grab the code from these links. dnlib must have `THREAD_SAFE`
defined when you compile it.

* [dnlib](https://github.com/0xd4d/dnlib)
* [NRefactory](https://github.com/0xd4d/NRefactory)
* [AvalonEdit](https://github.com/0xd4d/AvalonEdit)
