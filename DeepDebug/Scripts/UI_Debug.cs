using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Array = Godot.Collections.Array;

public class UI_Debug : Control
{
    public delegate void EventDebug();
    public event EventDebug onDebugOpen;
    public event EventDebug onDebugClose;
    public event EventDebug onDebugFreeze;
    public event EventDebug onDebugUnfreeze;

    [Export] bool fpsEnabled;

    public string folder = "DeepDebug";
    string path = "CanvasLayer";
    CanvasLayer canvas;

    Label fps;
    CanvasLayer freezer;

    List<UI_DebugInspector> inspectorPanels = new List<UI_DebugInspector>();

    Button createInspectorButton;
    Button saveLayoutButton;

    public List<string> bookmarkList = new List<string>();

    //Settings
    Button debugSettingsButton;
    PopupPanel debugSettings;
    public int openCloseKey;
    public int freezeKey;
    Button debugsSettingsSaveButton;

    Button openCloseButton;
    Button freezeButton;

    int keyBindModeCurrent = -1;
    Button openCloseKeyBind;
    Button freezeKeyBind;

    bool init;

#if DEBUG
    public override void _Ready()
    {
        DelayedStart();
    }

    async void DelayedStart()
    {
        float delay = 0.05f;
        bool readDelay = float.TryParse(ReadSetting("Settings", "Delay"), out delay);
        await ToSignal(GetTree().CreateTimer(delay), "timeout");
        Start();
    }

    void Start()
    {
        canvas = GetNode(path) as CanvasLayer;

        fps = canvas.GetNode("fps") as Label;
        debugSettings = canvas.GetNode("Settings menu") as PopupPanel;
        debugSettingsButton = canvas.GetNode("Settings") as Button;
        debugsSettingsSaveButton = debugSettings.GetNode("Panel/Save") as Button;
        debugsSettingsSaveButton.Connect("pressed", this, nameof(SaveSettings));
        debugSettingsButton.Connect("pressed", this, nameof(SettingsOpen));
        freezer = GetNode("Freezer") as CanvasLayer;

        LoadLayout();
        CreateBookmarkList();
        
        if (inspectorPanels.Count == 0)
        {
            CreateInspector("Inspector 1");
        }

        openCloseKey = int.Parse(ReadSetting("Hotkeys", "OpenClose"));
        freezeKey = int.Parse(ReadSetting("Hotkeys", "Freeze"));
        openCloseButton = canvas.GetNode("Close") as Button;
        openCloseButton.Connect("pressed", this, nameof(OpenClose));
        freezeButton = canvas.GetNode("Freeze") as Button;
        freezeButton.Connect("pressed", this, nameof(FreezeUnfreeze));

        createInspectorButton = canvas.GetNode("Inspector create") as Button;
        createInspectorButton.Connect("pressed", this, nameof(CreateInspector));
        saveLayoutButton = canvas.GetNode("Save") as Button;
        saveLayoutButton.Connect("pressed", this, nameof(SaveLayout));

        openCloseKeyBind = debugSettings.GetNode("Panel/Open key") as Button;
        openCloseKeyBind.Connect("pressed", this, nameof(BindKey), new Array { 0 });
        freezeKeyBind = debugSettings.GetNode("Panel/Freeze key") as Button;
        freezeKeyBind.Connect("pressed", this, nameof(BindKey), new Array { 1 });

        UpdateHotkeyHints();

        init = true;

        onDebugClose += CloseSettings;
    }

    public override void _ExitTree()
    {
        onDebugClose -= CloseSettings;
    }

    public override void _Process(float delta)
    {
        if (!init) return;

        if (fpsEnabled) fps.Text = "FPS: " + Engine.GetFramesPerSecond().ToString();
        fps.Visible = fpsEnabled;
    }

    public override void _Input(InputEvent @event)
    {
        if (!init) return;

        if (@event is InputEventKey && !@event.IsEcho() && Input.IsKeyPressed(openCloseKey))
        {
            OpenClose();
        }

        if (@event is InputEventKey && !@event.IsEcho() && Input.IsKeyPressed(freezeKey) && canvas.Visible)
        {
            FreezeUnfreeze();
        }
        if(keyBindModeCurrent != -1)
        {
            if (@event is InputEventKey)
            {
                switch (keyBindModeCurrent)
                {
                    case 0:
                        openCloseKey = (int)((InputEventKey)@event).Scancode;
                        break;
                    case 1:
                        freezeKey = (int)((InputEventKey)@event).Scancode;
                        break;
                }
                UpdateHotkeyHints();
                keyBindModeCurrent = -1;
                freezer.Visible = false;
                (debugSettings.GetNode("Panel/Freeze") as Panel).Visible = false;
            }
        }

    }

    public void CreateInspector()
    {
        CreateInspector($"Inspector {inspectorPanels.Count + 1}");
    }

    public void CreateInspector(string name)
    {
        var panel = (GD.Load($"res://{folder}/Resources/Inspector Panel.tscn") as PackedScene).Instance() as UI_DebugInspector;
        inspectorPanels.Add(panel);
        canvas.AddChild(panel);
        panel.Visible = true;
        panel.debug = this;
        panel.name = name;
        panel.Start();
    }

    public void DeleteInspector(string name)
    {
        int idx = inspectorPanels.FindIndex(x => x.name == name);

        inspectorPanels.RemoveAt(idx);
    }

    public void InspectorOpen(int i)
    {
        inspectorPanels[i].Visible = true;
    }

    public void InspectorClose(int i)
    {
        inspectorPanels[i].Visible = false;
    }

    public void SettingsOpen()
    {
        debugSettings.PopupCentered();
    }

    void UpdateHotkeyHints()
    {
        (debugSettings.GetNode("Panel/Open key") as Button).Text = Enum.GetName(typeof(KeyList), openCloseKey);
        openCloseButton.Text = Enum.GetName(typeof(KeyList), openCloseKey);
        (debugSettings.GetNode("Panel/Freeze key") as Button).Text = Enum.GetName(typeof(KeyList), freezeKey);
        freezeButton.Text = Enum.GetName(typeof(KeyList), freezeKey);
    }

    public string ReadSetting(string fileName, string id)
    {
        File file = new File();
        file.Open($"res://{folder}/Settings/{fileName}.txt", File.ModeFlags.Read);
        string[] allValues = file.GetAsText().Split('\n');
        file.Close();

        for (int i = 0; i < allValues.Length; i++)
        {
            string line = allValues[i].Replace("\r", "").Trim();

            string[] values = line.Split(" ");
            if (values.Length == 1) continue;
            if (values[0] == id) return values[1];
        }

        return "";
    }

    public void OpenClose()
    {
        if (canvas.Visible)
        {
            onDebugClose?.Invoke();
            canvas.Visible = false;
        }
        else
        {
            onDebugOpen?.Invoke();
            canvas.Visible = true;
        }
    }

    public void FreezeUnfreeze()
    {
        if (!freezer.Visible)
        {
            onDebugFreeze?.Invoke();
            freezer.Visible = true;
        }
        else
        {
            onDebugUnfreeze?.Invoke();
            freezer.Visible = false;
        }
    }

    public void SaveLayout()
    {
        File file = new File();
        file.Open($"res://{folder}/Settings/Layout.txt", File.ModeFlags.WriteRead);

        for (int i = 0; i < inspectorPanels.Count; i++)
        {
            UI_DebugInspector inspector = inspectorPanels[i];
            file.StoreLine($"{inspector.name.Replace("|", " ")}|{inspector.RectPosition.x}|{inspector.RectPosition.y}|" +
                $"{inspector.RectSize.x}|{inspector.RectSize.y}|{inspector.RectScale.x}|{inspector.container.Visible}|" +
                $"{(inspector.listType == UI_DebugInspector.ListType.Variables ? inspector.GetTargetPath() : "no")}");
        }
        file.Close();

        GD.Print("Saved inspector layout.");
    }

    public void LoadLayout()
    {
        File file = new File();
        file.Open($"res://{folder}/Settings/Layout.txt", File.ModeFlags.Read);
        string[] settings = file.GetAsText().Split('\n');
        file.Close();

        for (int i = 0; i < settings.Length; i++)
        {
            string line = settings[i].Replace("\r", "").Trim();
            string[] setting = line.Split('|');
            if (setting.Length <= 1) continue;

            CreateInspector(setting[0]);
            UI_DebugInspector inspector = inspectorPanels[inspectorPanels.Count - 1];
            inspector.RectPosition = new Vector2(float.Parse(setting[1]), float.Parse(setting[2]));
            inspector.RectSize = new Vector2(float.Parse(setting[3]), float.Parse(setting[4]));
            inspector.RectScale = new Vector2(float.Parse(setting[5]), float.Parse(setting[5]));
            inspector.container.Visible = bool.Parse(setting[6]);
            if(setting[7] != "no") inspector.SetTargetPath(setting[7]);
        }
    }

    void CreateBookmarkList()
    {
        File file = new File();

        file.Open($"res://{folder}/Settings/Bookmarks.txt", File.ModeFlags.Read);
        string[] bookmarks = file.GetAsText().Split('\n');
        file.Close();

        bookmarkList = bookmarks.ToList();
    }

    public void SaveBookmark(string bookmark)
    {
        if (bookmarkList.Contains(bookmark)) 
        {
            int toRemove = bookmarkList.FindIndex(x => x == bookmark);
            bookmarkList.RemoveAt(toRemove);
            GD.Print($"Removed bookmark: {bookmark}");
        }
        else 
        {
            bookmarkList.Add(bookmark); 
            GD.Print($"Saved bookmark: {bookmark}");
        }

        string saveList = "";
        for (int i = 0; i < bookmarkList.Count; i++)
        {
            saveList += bookmarkList[i];
            if (i < bookmarkList.Count - 1) saveList += "\n";
        }

        File file = new File();
        file.Open($"res://{folder}/Settings/Bookmarks.txt", File.ModeFlags.Write);
        file.StoreString(saveList);
        file.Close();
    }

    public void CloseSettings()
    {
        debugSettings.Visible = false;
    }

    public void SaveSettings()
    {
        SaveKeyBindings();
        GD.Print("Saved debug settings");
        debugSettings.Visible = false;
    }

    public void BindKey(int key)
    {
        (debugSettings.GetNode("Panel/Freeze") as Panel).Visible = true;
        keyBindModeCurrent = key;
        freezer.Visible = true;
    }

    void SaveKeyBindings()
    {
        File file = new File();
        file.Open($"res://{folder}/Settings/Hotkeys.txt", File.ModeFlags.WriteRead);

        file.StoreLine("OpenClose " + openCloseKey.ToString());
        file.StoreLine("Freeze " + freezeKey.ToString());
        file.Close();
    }
#endif
}

public class DispFieldInfo
{
    public int fieldIdx;
    public string fieldName;
    public int fieldArraySize;
}