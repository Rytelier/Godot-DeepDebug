Deep debug package for Godot

Features:
- Node C# script inspector
- Explore base classes, deeper branches and arrays
- Edit node's variables
- Save and load layout
- Bookmarks

![Inspector obj](https://user-images.githubusercontent.com/45795134/187693402-e1c70b0f-789e-44c0-87fb-b04ad013bb40.jpg)
![Inspector array](https://user-images.githubusercontent.com/45795134/187693438-20f3b1e0-4897-4a1e-8927-da438220a14e.jpg)
![Inspector bookmarks](https://user-images.githubusercontent.com/45795134/187693452-08e41f84-d47e-47b6-a8e2-af4f167fccda.jpg)
![Inspector editing](https://user-images.githubusercontent.com/45795134/187693533-170aca15-197b-4359-80f6-530a5d707ea7.jpg)


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
}
```

Events:
- onDebugOpen
- onDebugClose
- onDebugFreeze
- onDebugUnfreeze

Future updates:
- GDScript support
- Variable pinning - pin variable display on screen, regardless of inspector's path
- Variable tracking - graphic tracking of number variable's values over time
