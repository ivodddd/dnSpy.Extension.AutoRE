dnSpy.Extension.AutoRE
======================

A [dnSpyEx](https://github.com/dnSpyEx/dnSpy) extension that tags methods by the APIs they use
and renames obfuscated wrapper methods. It's inspired by
[AutoRE](https://github.com/a1ext/auto_re), an IDA Pro plugin by
[a1ext](https://github.com/a1ext) that does the same for native binaries.

Features
--------

### API tags

Every method that references a known API gets one or more tags. The tags are shown in a tool
window grouped by their most interesting tag:

| Tag | Meaning |
|-----|---------|
| `ANTI-DEBUG` | debugger checks (`Debugger.IsAttached`, `NtQueryInformationProcess`, `IsDebuggerPresent`, ...) |
| `CRYPTO` | `System.Security.Cryptography`, `bcrypt`, `crypt32` |
| `NET` | `System.Net`, sockets, WinINet/WinHTTP |
| `REG` | `Microsoft.Win32.Registry`, `advapi32` |
| `FILE` | `System.IO`, file related native APIs |
| `PROC` | `System.Diagnostics.Process`, process related native APIs |
| `SECURITY` | tokens, privileges, `System.Security.Principal` |
| `MEM` | `Marshal`, `VirtualAlloc`, `VirtualProtect` |
| `NATIVE` | any P/Invoke |
| `REFLECT` | `System.Reflection` |
| `SERIALIZE` | JSON/XML serializers, `System.Runtime.Serialization` |
| `THREAD` | `System.Threading`, `CreateThread` |
| `UI` | WPF/WinForms/`user32` |
| `ENV` | `System.Environment` |
| `TEXT` | `System.Text` |
| `DEBUG` | `Debug.Assert`, `Debug.WriteLine`, ... |

P/Invoke methods are tagged from the native module and the entry point name, eg.
`kernel32!CreateFileW` is `FILE` + `NATIVE`.

String literals are inspected too: `http://`, `https://` and `ftp://` add `NET`, `HKEY_` and
`SOFTWARE\` add `REG`, `%APPDATA%`/`%TEMP%` add `FILE`, and PEM headers add `CRYPTO`.

### Wrapper renames

A method is considered a wrapper when its body only loads arguments (and constants), calls one
other method and returns. If its name looks obfuscated (`method_12`, `sub_40A1C0`, `\u0001`,
single letters, ...), a new name is proposed from the called API and its declaring type, eg.:

| Current | Proposed |
|---------|----------|
| `method_1` | `File_Delete` |
| `method_2` | `Directory_Exists` |
| `method_3` | `Process_Start` |
| `method_4` | `Debugger_Break` |

Nothing is ever renamed automatically. The results are listed with a checkbox and the changes are
only applied when you press **Apply renames**.

Usage
-----

1. Select an assembly, module or any of its members in the assembly explorer.
2. **Edit → AutoRE: Analyze Assembly**, the tree view context menu, or `Ctrl+Alt+R`.
3. Review the results in the **AutoRE** tool window. Double click a row to select the method in
   the assembly explorer, and **Cancel** to stop a long analysis. The analyzed assembly is
   always the one that owns the current selection in the assembly explorer.
4. Check the renames you want and press **Apply renames**. If *Include non-private* is checked
   you get a confirmation first, since renaming public/protected methods can break other
   assemblies that reference them.
5. Save with **File → Save Module**. The extension modifies the in-memory module directly, so
   dnSpy's undo/redo history and the modified (`*`) marker don't know about these changes; the
   normal *Save Module* command still writes the current module to disk.

**Export...** writes the current result as CSV (token, type, method, tags, access, proposed name,
APIs), which is handy to diff two builds or to feed another tool.

Installation
------------

1. Download `dnSpy.Extension.AutoRE.x.dll` from the releases page.
2. Copy it to the `bin/Extensions/dnSpy.Extension.AutoRE` directory of your dnSpy installation
   (create the directories if they don't exist).
3. Start dnSpy and use **Edit → AutoRE: Analyze Assembly** or `Ctrl+Alt+R`.

The extension targets `net10.0-windows` and is meant for the .NET build of dnSpyEx 6.6.0 or
newer. It doesn't load in the `net48` build.

Building
--------

You need the .NET 10 SDK and a dnSpyEx installation (or build output) to compile against. Point
`DnSpyDir` to the directory that contains `dnSpy.Contracts.DnSpy.dll`, `dnlib.dll`, etc. (the
`bin` directory of a dnSpyEx installation):

```
dotnet build src/dnSpy.Extension.AutoRE -c Release -p:DnSpyDir="<path to dnSpy bin directory>"
```

`DnSpyDir` defaults to `dnSpy` next to the repository root. The build copies the extension to
`<DnSpyDir>/Extensions/dnSpy.Extension.AutoRE/`; pass `-p:DeployToDnSpy=false` to skip that step.

Limitations
-----------

* Changes made by this extension are **not** part of dnSpy's undo history.
* Public/protected methods are skipped by default (see *Include non-private*) because renaming
  them can break other assemblies that reference them by name.
* Virtual methods, overrides, interface implementations, property/event accessors, constructors
  and P/Invoke methods are never renamed.
* The analysis is syntactic (IL references); it doesn't run deobfuscator passes such as string
  decryption or proxy removal.
* String based tags are pattern matches on string literals, so code that only prints a URL or a
  registry path also gets the tag.
* Only the methods that reference a tagged API or that look like wrappers are listed.

Credits
-------

* [a1ext/auto_re](https://github.com/a1ext/auto_re) by [a1ext](https://github.com/a1ext) is the
  original AutoRE plugin for IDA Pro and the inspiration for this extension: auto renaming
  functions that wrap a single API call and tagging functions by the APIs they use.
* [dnSpyEx](https://github.com/dnSpyEx/dnSpy), the host application.

License
-------

GPL-3.0-or-later, same as dnSpyEx. See [LICENSE.txt](LICENSE.txt).
