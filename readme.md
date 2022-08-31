Deep debug package for Godot

Features:
- Node C# script inspector
- Explore base classes, deeper branches and arrays
- Edit node's variables
- Save and load layout
- Bookmarks

Place DeepDebug folder in your root project folder (res://)
To enable the debbuger just place the scene from DeepDebug folder in you game scene.

To open and close debugger at runtime, get the UI_Debug script from the debugger's scene main node and call OpenDebugger() and CloseDebugger().
You can also use events: onDebuggerOpen and onDebuggerClose to call additional functions on open/close like player character's input freeze.
Example:
```cs
override void Ready()
{
	onDebugOpen += InputFreeze;
	onDebugClose += InputUnfreeze;
}

override void ExitTree()
{
	onDebugOpen -= InputFreeze;
	onDebugClose -= InputUnfreeze;
}```

Events:
- onDebugOpen
- onDebugClose
- onDebugFreeze
- onDebugUnfreeze

Future updates:
- GDScript support
- Variable pinning - pin variable display on screen, regardless of inspector's path
- Variable tracking - graphic tracking of number variable's values over time