using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Array = Godot.Collections.Array;
using System.Diagnostics;

public class UI_DebugInspector : UI_Window
{
    public UI_Debug debug;
    public string name;

    string targetPath;
    Node target;

    VBoxContainer buttonVariableTemplate;
    VBoxContainer buttonSearchTemplate;

    Button refreshButton, backButton, arrayEntryButton, bookmarkButton, bookListButton, refreshPathsButton;

    Button foundPathButton;
    HBoxContainer valueGroup;

    PopupPanel valueInput, valueBool, valueNumber, valueVector, valueColor;
    PopupPanel popupCurrent;
    SpinBox valueX, valueY, valueZ;

    bool valueMouseLeft;
    Control valueEditCurrent;

    Button pathButton;
    Label typeLabel;
    VBoxContainer buttonsList;

    public enum ListType
    {
        Paths,
        Variables,
        Bookmarks
    }
    public ListType listType;

    int scriptTypeCurrent;
    List<string> fieldNamesChain = new List<string>();
    List<object> fieldValuesChain = new List<object>();
    List<int> fieldCategoryChain = new List<int>();
    List<FieldInfo> fieldInfoChain = new List<FieldInfo>();

    FieldInfo[] fieldsCurrent;
    System.Collections.IList arrayCurrent;
    Button[] valueButtons;
    ParameterInfo[] paramsCurrent;
    string[] foundPaths;

    int variableSelected;

    public enum VarType
    {
        Int,
        Float,
        Bool,
        String,
        Vector3,
        Vector2,
        Color,
        Other
    }

    string tagSymbol = "$#$";

    public void Start()
    {
        pathButton = container.GetNode("Path") as Button;
        pathButton.Connect("pressed", this, nameof(PathButton));
        typeLabel = container.GetNode("Type") as Label;
        refreshButton = container.GetNode("Refresh") as Button;
        refreshButton.Connect("pressed", this, nameof(Refresh));
        backButton = container.GetNode("Back") as Button;
        backButton.Connect("pressed", this, nameof(GoBack));
        bookmarkButton = container.GetNode("Bookmark") as Button;
        bookmarkButton.Connect("pressed", this, nameof(SaveBookmark));
        bookListButton = container.GetNode("Bookmark list") as Button;
        bookListButton.Connect("pressed", this, nameof(CreateBookmarksList));
        refreshPathsButton = container.GetNode("Paths refresh") as Button;
        refreshPathsButton.Connect("pressed", this, nameof(PathsRefresh));

        arrayEntryButton = container.GetNode("Array entry") as Button;
        foundPathButton = container.GetNode("FoundPath") as Button;
        foundPathButton.Visible = false;
        valueGroup = container.GetNode("Value group") as HBoxContainer;
        buttonsList = container.GetNode("Variable list/List") as VBoxContainer;

        valueInput = container.GetNode("Value input") as PopupPanel;
        valueBool = container.GetNode("Value bool") as PopupPanel;
        valueNumber = container.GetNode("Value number") as PopupPanel;
        valueColor = container.GetNode("Value color") as PopupPanel;
        valueVector = container.GetNode("Value vector") as PopupPanel;
        valueX = valueVector.GetNode("Panel/x") as SpinBox;
        valueY = valueVector.GetNode("Panel/y") as SpinBox;
        valueZ = valueVector.GetNode("Panel/z") as SpinBox;

        panelName.Text = name;

        CreateAllPathsList();

        OnDelete += DeleteInspector;
        debug.onDebugClose += ClosePopup;
    }

    public override void _ExitTree()
    {
        OnDelete -= DeleteInspector;
        debug.onDebugClose -= ClosePopup;
    }

    public override void _Process(float delta)
    {
        if (!Visible) return;
        switch (listType)
        {
            case ListType.Paths:

                break;
            case ListType.Variables:
                if(categoryCurrent == 0) UpdateBaseVariables();
                if (categoryCurrent == 1) RefreshArrayList();
                break;
        }

        backButton.Visible = fieldNamesChain.Count > 0 && (listType == ListType.Variables);
        bookmarkButton.Visible = listType == ListType.Variables;
        refreshPathsButton.Visible = listType == ListType.Paths;
    }

    public override void _Input(InputEvent @event)
    {
        base._Input(@event);

        if (!Visible) return;
    }

    void UpdateBaseVariables()
    {
        try
        {
            if(targetCurrent == null)
            {
                CreateAllPathsList();
            }

            for (int i = 0; i < valueButtons.Length; i++)
            {            
                if (IsNotAllowed(fieldsCurrent[i])) return;
                if (fieldsCurrent[i].GetValue(targetCurrent) == null)
                    valueButtons[i].Text = "(null)";
                else if (fieldsCurrent[i].GetValue(targetCurrent).ToString() == "")
                    valueButtons[i].Text = "(EMPTY)";
                else
                    valueButtons[i].Text = fieldsCurrent[i].GetValue(targetCurrent).ToString();
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"Debug inspector: failed to get values - {targetCurrent}\n{e.Message}");
        }
    }

    public void GetTarget()
    {
        target = GetTree().Root.GetNodeOrNull(targetPath);
    }

    public void SetTarget(string path)
    {
        targetPath = path;
        GetTarget();
    }

    #region Current var
    FieldInfo fieldCurrent => fieldsCurrent[variableSelected];
    string fieldCurrentDebug => variableSelected < fieldsCurrent.Length ? fieldsCurrent[variableSelected].Name : "??";
    object targetCurrent
    {
        get
        {
            if (fieldValuesChain.Count == 0)
                if (!IsInstanceValid(target)) return null;
                else return target;
            else
                return fieldValuesChain[fieldValuesChain.Count - 1];
        }
    }
    object targetPrevious
    {
        get
        {
            if (fieldValuesChain.Count <= 1)
                return target;
            else
                return fieldValuesChain[fieldValuesChain.Count - 2];
        }
    }
    int categoryCurrent
    {
        get
        {
            if (fieldCategoryChain.Count == 0)
                return 0;
            else
                return fieldCategoryChain[fieldCategoryChain.Count - 1];
        }
    }
    int categoryPrevious
    {
        get
        {
            if (fieldCategoryChain.Count <= 1)
                return 0;
            else
                return fieldCategoryChain[fieldCategoryChain.Count - 2];
        }
    }
    FieldInfo fieldInfoCurrent
    {
        get
        {
            if (fieldInfoChain.Count == 0)
                return null;
            else
                return fieldInfoChain[fieldInfoChain.Count - 1];
        }
    }
    #endregion

    #region Is field ?
    bool IsBasicType(FieldInfo field) =>
           field.FieldType.IsPrimitive
        || field.FieldType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEquatable<>));

    bool IsBasicType(Type type) =>
           type.IsPrimitive
        || type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEquatable<>));

    bool IsNotAllowed(FieldInfo field) => field.Name == "ptr" || field.Name == "memoryOwn";

    bool IsArray(FieldInfo field) => 
        field.FieldType.IsArray
     || field.FieldType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>))
     || field.FieldType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>));
    #endregion

    void GetFieldsBase()
    {
        if (target == null && !IsInstanceValid(target)) return;

        try
        {
            fieldsCurrent = target.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }
        catch (Exception e)
        {
            GD.PrintErr($"Debug inspector: failed to get base fields\n{e.Message}");
        }
    }

    List<string> GetAllNodePaths(Node source = null, bool scriptedOnly = true)
    {
        List<string> paths = new List<string>();

        Node node = source;
        if(source == null) node = GetTree().Root;

        foreach (Node found in node.GetChildren())
        {
            string tag = "";
            if (found.GetScript() != null)
            {
                if (found.GetScript().GetType() == typeof(GDScript))
                    tag = tagSymbol + "gs";
                else
                    tag = tagSymbol + "s";
            }

            if(tag!= "" && scriptedOnly)
                paths.Add(found.GetPath() + tag);
            else if (!scriptedOnly)
                paths.Add(found.GetPath() + tag);

            if (found.GetChildCount() > 0) paths.AddRange(GetAllNodePaths(found));
        }

        return paths;
    }

    #region List
    void ClearButtonList()
    {
        for (int i = buttonsList.GetChildCount() - 1; i >= 0; i--)
        {
            buttonsList.GetChild(i).QueueFree();
        }
    }

    void CreateAllPathsList()
    {
        listType = ListType.Paths;

        ClearButtonList();

        typeLabel.Text = "Node selection";
        foundPaths = GetAllNodePaths().ToArray();
        for (int i = 0; i < foundPaths.Length; i++)
        {
            string path = foundPaths[i];
            string[] pathSplit = path.Split(tagSymbol);

            string tag = "";
            path = pathSplit[0];
            if(pathSplit.Length > 1) tag = pathSplit[1];

            /* //When accepting all types of object, idk if editing them will be implemented
            string[] pathParts = path.Split('/');
            string text = "";
            for (int p = 0; p < pathParts.Length; p++)
            {
                if (p != pathParts.Length - 1) 
                {
                    text += " ";
                } 
                else text += pathParts[p];
            }*/
            string text = "   " + path.Replace("/root/", "");
            Button button = foundPathButton.Duplicate() as Button;
            var label = button.GetNode("Label") as Label;

            button.Text = text;
            button.Name = path;
            button.Visible = true;

            int scriptType = 0;
            if      (tag == "s") { label.Text = "C"; label.Modulate = new Color(0.22f,0.62f,0.12f); scriptType = 0; }
            else if (tag == "gs"){ label.Text = "G"; label.Modulate = new Color(0.28f,0.59f,0.75f); scriptType = 1; }
            buttonsList.AddChild(button);
            button.Connect("pressed", this, nameof(SetPath), new Array { path, scriptType, "" });
        }
    }

    string ArrayButtonName(int i)
    {
        string text = "";
        if (arrayCurrent[i] != null)
        {
            var id = arrayCurrent[i].GetType().GetFields()[0].GetValue(arrayCurrent[i]);
            if (IsBasicType(arrayCurrent[i].GetType())) id = arrayCurrent[i].ToString();
            if (id == null) text += "(null)";
            else if (id.ToString() == "") text += "(EMPTY)";
            else text += id.ToString();
        }
        else
        {
            text += "(null)";
        }
        return text;
    }

    void CreateVariablesList(bool deeper = false)
    {
        listType = ListType.Variables;

        ClearButtonList();
        typeLabel.Text = targetCurrent.GetType().FullName;

        if (!deeper) 
        {
            fieldNamesChain.Clear();
            fieldValuesChain.Clear();
            fieldInfoChain.Clear();
            fieldCategoryChain.Clear();
            GetFieldsBase(); 
        }
        valueButtons = new Button[fieldsCurrent.Length];

        pathButton.Text = targetPath;
        for (int i = 0; i < fieldNamesChain.Count; i++) //Field path text on the top
        {
            pathButton.Text += "." + fieldNamesChain[i];
            if (i == fieldNamesChain.Count - 1 && categoryCurrent == 0) continue;
            if (fieldCategoryChain[i] == 1) pathButton.Text += "[]";
        }

        if(categoryCurrent == 1) //Array entries
        {
            for (int i = 0; i < arrayCurrent.Count; i++)
            {
                var button = arrayEntryButton.Duplicate() as Button;

                button.Text = $"[{i}]";
                button.Text += ArrayButtonName(i);

                button.Visible = true;
                buttonsList.AddChild(button);

                button.Connect("pressed", this, nameof(GoToArrayEntry), new Array { i });
            }
            return;
        }

        if (fieldsCurrent.Length == 0) return;
        for (int i = 0; i < fieldsCurrent.Length; i++) //Fields
        {
            if (IsNotAllowed(fieldsCurrent[i])) return;
            var buttonGroup = valueGroup.Duplicate() as HBoxContainer;
            buttonsList.AddChild(buttonGroup);

            buttonGroup.Visible = true;
            var buttonVar = buttonGroup.GetNode("Variable") as Button;
            var buttonValue = buttonGroup.GetNode("Value") as Button;
            var extend = buttonGroup.GetNode("Extend") as Control;
            valueButtons[i] = buttonValue;
            VarType vartype = GetFieldType(fieldsCurrent[i]);
            buttonValue.Connect("pressed", this, nameof(ValueEdit), new Array { buttonValue.RectPosition, i, vartype });
            buttonVar.Connect("pressed", this, nameof(GoDeeper), new Array { i });

            extend.Visible = !IsBasicType(fieldsCurrent[i]);
            buttonVar.Text = fieldsCurrent[i].Name + ":";
        }
    }

    void CreateBookmarksList()
    {
        listType = ListType.Bookmarks;

        ClearButtonList();

        for (int i = 0; i < debug.bookmarkList.Count; i++)
        {
            if (debug.bookmarkList[i] == "") continue;
            string path = debug.bookmarkList[i].Replace("\r", "");
            int scriptType = int.Parse((debug.bookmarkList[i][debug.bookmarkList[i].Length - 1]).ToString());

            string text = "   " + path.Replace("/root/", "");
            text = text.Replace(tagSymbol, ":");
            text = text.Remove(text.Length - 1);

            Button button = foundPathButton.Duplicate() as Button;
            var label = button.GetNode("Label") as Label;
            label.Text = "*";

            button.Text = text;
            button.Name = path;
            button.Visible = true;

            buttonsList.AddChild(button);
            button.Connect("pressed", this, nameof(SetTargetPath), new Array { path});
        }
    }
    #endregion
    VarType GetFieldType(FieldInfo field)
    {
        switch (field.FieldType.ToString().ToLower())
        {
            case "system.boolean":
                return VarType.Bool;

            case "system.int32":
                return VarType.Int;
            case "system.uint32":
                return VarType.Int;
            case "system.int16":
                return VarType.Int;
            case "system.uint16":
                return VarType.Int;
            case "system.int64":
                return VarType.Int;
            case "system.uint64":
                return VarType.Int;
            case "system.byte":
                return VarType.Int;
            case "system.sbyte":
                return VarType.Int;

            case "system.single":
                return VarType.Float;
            case "system.double":
                return VarType.Float;

            case "system.string":
                return VarType.String;

            case "godot.vector2":
                return VarType.Vector2;
            case "godot.vector3":
                return VarType.Vector3;

            case "godot.color":
                return VarType.Color;
        }

        return VarType.Other;
    }

    #region Button functions
    public void SetPath(string path, int scriptType = 0, string varPath = "")
    {
        scriptTypeCurrent = scriptType;

        if (scriptType == 0)
        {
            SetTarget(path);
            CreateVariablesList();
        }
        else
        {
            GD.Print("Deep inspector: GD Script support is not implemented.");

            //SetTarget(path);
            //GD.Print(((GDScript)target.GetScript()).GetScriptPropertyList());
        }
    }

    public void PathButton()
    {
        switch (listType)
        {
            case ListType.Variables:
                CreateAllPathsList();
                break;
        }
    }

    public void Refresh()
    {
        if (targetPath != null) CreateVariablesList();
    }

    public void GoDeeper(int i)
    {
        try
        {
            variableSelected = i;
            if (IsBasicType(fieldCurrent)) return;

            var value = fieldCurrent.GetValue(targetCurrent);
            if (value == null) return;
            
            fieldNamesChain.Add(fieldCurrent.Name);
            fieldInfoChain.Add(fieldCurrent);

            if (IsArray(fieldCurrent))
            {
                fieldCategoryChain.Add(1);

                var array = (System.Collections.IList)fieldCurrent.GetValue(targetCurrent);

                fieldsCurrent = new FieldInfo[0];
                arrayCurrent = array;
            }
            else
            {
                fieldCategoryChain.Add(0);
                fieldsCurrent = fieldCurrent.FieldType.GetFields();
            }

            fieldValuesChain.Add(value);

            CreateVariablesList(true);
        }
        catch (Exception e)
        {
            var st = new StackTrace(e, true);
            var file = st.GetFrame(st.FrameCount - 1).GetFileName();
            var line = st.GetFrame(st.FrameCount-1).GetFileLineNumber();
            GD.PrintErr($"Debug inspector: failed to get class value - {fieldCurrentDebug}\n{e.Message}, {file}:{line}");
        }
    }

    public void GoDeeper(string varName)
    {
        int idx = -1;
        if(categoryCurrent == 0)
        {
            idx = fieldsCurrent.ToList().FindIndex(x => x.Name.Replace("[]", "") == varName);
            if(idx!= -1) GoDeeper(idx);

        }
    }

    public void RefreshArrayList()
    {
        if (targetCurrent == null) return;
        var array = (System.Collections.IList)fieldInfoCurrent.GetValue(targetPrevious);

        arrayCurrent = array;

        int buttonsAmount = buttonsList.GetChildCount();
        if (buttonsAmount > array.Count) //If there are more buttons than array entries
        {
            for (int i = buttonsList.GetChildCount() - 1; i >= 0; i--)
            {
                if (i >= array.Count) buttonsList.GetChild(i).QueueFree();
            }
        }
        for (int i = 0; i < arrayCurrent.Count; i++)
        {
            if (i < buttonsAmount) //Update existing buttons
            {
                var origButton = buttonsList.GetChild(i) as Button;
                origButton.Text = $"[{i}]";
                origButton.Text += ArrayButtonName(i);
            }
            else //If there are less buttons than array entries
            {
                var newButton = arrayEntryButton.Duplicate() as Button;

                newButton.Text = $"[{i}]";
                newButton.Text += ArrayButtonName(i);

                newButton.Visible = true;
                buttonsList.AddChild(newButton);

                newButton.Connect("pressed", this, nameof(GoToArrayEntry), new Array { i });
            }
        }
    }

    public void GoBack()
    {
        try
        {
            if (fieldNamesChain.Count == 0) return;

            fieldNamesChain.RemoveAt(fieldNamesChain.Count - 1);
            fieldCategoryChain.RemoveAt(fieldCategoryChain.Count - 1);
            fieldValuesChain.RemoveAt(fieldValuesChain.Count - 1);
            fieldInfoChain.RemoveAt(fieldInfoChain.Count - 1);

            if (fieldNamesChain.Count == 0)
            {
                CreateVariablesList();
            }
            else
            {
                if (fieldCategoryChain[fieldCategoryChain.Count - 1] == 1)
                {
                    var array = (System.Collections.IList)fieldValuesChain[fieldValuesChain.Count - 1];

                    fieldsCurrent = new FieldInfo[0];
                    arrayCurrent = array;
                }
                else
                {
                    fieldsCurrent = fieldValuesChain[fieldValuesChain.Count - 1].GetType().GetFields();
                }
                CreateVariablesList(true);
            }
        }
        catch(Exception e)
        {
            var st = new StackTrace(e, true);
            var file = st.GetFrame(st.FrameCount - 1).GetFileName();
            var line = st.GetFrame(st.FrameCount-1).GetFileLineNumber();
            GD.PrintErr($"\n{e.Message}, {file}:{line}");
        }

    }

    public void GoToArrayEntry(int i)
    {
        if(arrayCurrent != null && arrayCurrent.Count > 0)
        {
            if (arrayCurrent[i] == null) return;
            if (IsBasicType(arrayCurrent[i].GetType())) return;

            var value = arrayCurrent[i];

            fieldNamesChain.Add(i.ToString());
            fieldValuesChain.Add(value);
            fieldInfoChain.Add(fieldInfoCurrent);
            fieldCategoryChain.Add(0);

            fieldsCurrent = value.GetType().GetFields();

            CreateVariablesList(true);
        }
    }

    public void ValueEdit(Vector2 pos, int variable, VarType type)
    {
        try
        {
            variableSelected = variable;
            if (!IsBasicType(fieldCurrent)) return;

            switch (type)
            {
                case VarType.Bool:
                    var bvalue = valueBool.GetNode("Switch") as CheckBox;
                    bvalue.Pressed = (bool)fieldCurrent.GetValue(targetCurrent);

                    if (bvalue.IsConnected("toggled", this, nameof(ValueApplyBool)))
                        bvalue.Disconnect("toggled", this, nameof(ValueApplyBool));
                    bvalue.Connect("toggled", this, nameof(ValueApplyBool));

                    popupCurrent = valueBool;
                    break;
                case VarType.String:
                    var sval = fieldCurrent.GetValue(targetCurrent);
                    if (sval == null) fieldCurrent.SetValue(targetCurrent, Convert.ChangeType("", typeof(string)));

                    var svalue = valueInput.GetNode("Input") as LineEdit;

                    svalue.Text = fieldCurrent.GetValue(targetCurrent).ToString();

                    if (svalue.IsConnected("text_entered", this, nameof(ValueApply)))
                        svalue.Disconnect("text_entered", this, nameof(ValueApply));
                    svalue.Connect("text_entered", this, nameof(ValueApply));

                    popupCurrent = valueInput;
                    break;
                case VarType.Int:
                    //valueNumber.Value = (int)fieldCurrent.GetValue(targetCurrent);
                    var val = fieldCurrent.GetValue(targetCurrent);
                    var ivalue = valueNumber.GetNode("Value") as SpinBox;

                    ivalue.Value = (double)Convert.ChangeType(val, typeof(double));
                    ivalue.Rounded = true;
                    ivalue.Step = 1;

                    if (ivalue.IsConnected("value_changed", this, nameof(NumberApply)))
                        ivalue.Disconnect("value_changed", this, nameof(NumberApply));
                    ivalue.Connect("value_changed", this, nameof(NumberApply));

                    popupCurrent = valueNumber;
                    break;
                case VarType.Float:
                    var valf = fieldCurrent.GetValue(targetCurrent);
                    var fvalue = valueNumber.GetNode("Value") as SpinBox;

                    fvalue.Value = (double)Convert.ChangeType(valf, typeof(double));
                    fvalue.Rounded = false;
                    fvalue.Step = 0.001;

                    if (fvalue.IsConnected("value_changed", this, nameof(NumberApply)))
                        fvalue.Disconnect("value_changed", this, nameof(NumberApply));
                    fvalue.Connect("value_changed", this, nameof(NumberApply));

                    popupCurrent = valueNumber;
                    break;
                case VarType.Color:
                    ColorPicker picker = valueColor.GetNode("Picker") as ColorPicker;
                    picker.Color = (Color)fieldCurrent.GetValue(targetCurrent);

                    if (picker.IsConnected("color_changed", this, nameof(ColorApply)))
                        picker.Disconnect("color_changed", this, nameof(ColorApply));
                    picker.Connect("color_changed", this, nameof(ColorApply));

                    popupCurrent = valueColor;
                    break;
                case VarType.Vector2:
                    valueX.Value = ((Vector2)fieldCurrent.GetValue(targetCurrent)).x;
                    valueY.Value = ((Vector2)fieldCurrent.GetValue(targetCurrent)).y;

                    if (valueX.IsConnected("value_changed", this, nameof(Vector2Apply))) valueX.Disconnect("value_changed", this, nameof(Vector2Apply));
                    if (valueY.IsConnected("value_changed", this, nameof(Vector2Apply))) valueY.Disconnect("value_changed", this, nameof(Vector2Apply));
                    if (valueX.IsConnected("value_changed", this, nameof(Vector3Apply))) valueX.Disconnect("value_changed", this, nameof(Vector3Apply));
                    if (valueY.IsConnected("value_changed", this, nameof(Vector3Apply))) valueY.Disconnect("value_changed", this, nameof(Vector3Apply));
                    if (valueZ.IsConnected("value_changed", this, nameof(Vector3Apply))) valueZ.Disconnect("value_changed", this, nameof(Vector3Apply));
                    valueX.Connect("value_changed", this, nameof(Vector2Apply), new Array { 0 });
                    valueY.Connect("value_changed", this, nameof(Vector2Apply), new Array { 1 });

                    popupCurrent = valueVector;
                    break;
                case VarType.Vector3:
                    valueX.Value = ((Vector3)fieldCurrent.GetValue(targetCurrent)).x;
                    valueY.Value = ((Vector3)fieldCurrent.GetValue(targetCurrent)).y;
                    valueZ.Value = ((Vector3)fieldCurrent.GetValue(targetCurrent)).z;

                    //Hell.
                    if (valueX.IsConnected("value_changed", this, nameof(Vector3Apply))) valueX.Disconnect("value_changed", this, nameof(Vector3Apply));
                    if (valueY.IsConnected("value_changed", this, nameof(Vector3Apply))) valueY.Disconnect("value_changed", this, nameof(Vector3Apply));
                    if (valueZ.IsConnected("value_changed", this, nameof(Vector3Apply))) valueZ.Disconnect("value_changed", this, nameof(Vector3Apply));
                    if (valueX.IsConnected("value_changed", this, nameof(Vector2Apply))) valueX.Disconnect("value_changed", this, nameof(Vector2Apply));
                    if (valueY.IsConnected("value_changed", this, nameof(Vector2Apply))) valueY.Disconnect("value_changed", this, nameof(Vector2Apply));
                    if (valueZ.IsConnected("value_changed", this, nameof(Vector2Apply))) valueZ.Disconnect("value_changed", this, nameof(Vector2Apply));
                    valueX.Connect("value_changed", this, nameof(Vector3Apply), new Array { 0 });
                    valueY.Connect("value_changed", this, nameof(Vector3Apply), new Array { 1 });
                    valueZ.Connect("value_changed", this, nameof(Vector3Apply), new Array { 2 });

                    popupCurrent = valueVector;
                    break;
            }

            if (popupCurrent != null)
            {
                //Temp converting to popups
                popupCurrent.PopupCenteredMinsize();
                if(type != VarType.Color) popupCurrent.RectPosition = GetViewport().GetMousePosition();
                valueEditCurrent = popupCurrent;
            }
        }
        catch (Exception e)
        {
            var st = new StackTrace(e, true);
            var file = st.GetFrame(st.FrameCount - 1).GetFileName();
            var line = st.GetFrame(st.FrameCount - 1).GetFileLineNumber();
            GD.PrintErr($"Debug inspector: error accessing the value.\n{e.Message}, {file}:{line}");
        }
    }

    public void ClosePopup()
    {
        if (popupCurrent == null) return;

        popupCurrent.Visible = false;
    }

    string valueIncorrect = "Debug inspector: incorrect value";

    public void ValueApply(string new_text)
    {
        try
        {
            fieldCurrent.SetValue(targetCurrent, Convert.ChangeType(new_text, fieldCurrent.FieldType));
        }
        catch
        {
            GD.PrintErr(valueIncorrect);
        }
    }

    public void NumberApply(float value)
    {
        try
        {
            fieldCurrent.SetValue(targetCurrent, Convert.ChangeType(value, fieldCurrent.FieldType));
        }
        catch
        {
            GD.PrintErr(valueIncorrect);
        }
    }

    public void Vector3Apply(float value, int v)
    {
        try
        {
            Vector3 orig = (Vector3)fieldCurrent.GetValue(targetCurrent);
            switch (v)
            {
                case 0:
                    fieldCurrent.SetValue(targetCurrent, Convert.ChangeType(new Vector3(value, orig.y, orig.z), fieldCurrent.FieldType));
                    break;
                case 1:
                    fieldCurrent.SetValue(targetCurrent, Convert.ChangeType(new Vector3(orig.x, value, orig.z), fieldCurrent.FieldType));
                    break;
                case 2:
                    fieldCurrent.SetValue(targetCurrent, Convert.ChangeType(new Vector3(orig.x, orig.y, value), fieldCurrent.FieldType));
                    break;
            }
        }
        catch
        {
            GD.PrintErr(valueIncorrect);
        }
    }

    public void Vector2Apply(float value, int v)
    {
        try
        {
            Vector2 orig = (Vector2)fieldCurrent.GetValue(targetCurrent);
            switch (v)
            {
                case 0:
                    fieldCurrent.SetValue(targetCurrent, Convert.ChangeType(new Vector2(value, orig.y), fieldCurrent.FieldType));
                    break;
                case 1:
                    fieldCurrent.SetValue(targetCurrent, Convert.ChangeType(new Vector2(orig.x, value), fieldCurrent.FieldType));
                    break;
            }
        }
        catch
        {
            GD.PrintErr(valueIncorrect);
        }
    }

    public void ColorApply(Color color)
    {
        try
        {
            fieldCurrent.SetValue(targetCurrent, Convert.ChangeType(color, fieldCurrent.FieldType));
        }
        catch
        {
            GD.PrintErr(valueIncorrect);
        }
    }

    public void ValueApplyBool(bool button_pressed)
    {
        fieldCurrent.SetValue(targetCurrent, button_pressed);
    }

    public void SaveBookmark()
    {
        debug.SaveBookmark(GetTargetPath());
    }

    public string GetTargetPath()
    {
        string varPath = "";
        string tag = fieldNamesChain.Count != 0 ? tagSymbol : "";
        for (int i = 0; i < fieldNamesChain.Count; i++)
        {
            varPath += fieldNamesChain[i] + (i != fieldNamesChain.Count-1 ? "." : "");
        }
        string path = targetPath + tag + varPath + scriptTypeCurrent.ToString();
        return path;
    }

    public void SetTargetPath(string fullPath)
    {
        try
        {
            string[] fullPathSplit = fullPath.Split(tagSymbol);

            string targetpath = fullPathSplit[0];
            if(fullPathSplit.Length == 1) targetpath = targetpath.Remove(targetpath.Length - 1);
            SetTarget(targetpath);
            Refresh();

            bool deeper = false;
            if (fullPathSplit.Length > 1)
            {
                SetTarget(fullPathSplit[0]);
                if (target == null || !IsInstanceValid(target)) return;

                string[] varPath = fullPathSplit[1].Remove(fullPathSplit[1].Length - 1).Split(".");
                for (int i = 0; i < varPath.Length; i++)
                {
                    int arridx = -1;
                    bool isarray = int.TryParse(varPath[i], out arridx);
                    if (!isarray)
                    {
                        GoDeeper(varPath[i]);
                        deeper = true;
                    }
                    else
                    {
                        GoToArrayEntry(arridx);
                        deeper = true;
                    }
                }
            }
            CreateVariablesList(deeper);
        } 
        catch(Exception e)
        {
            var st = new StackTrace(e, true);
            var file = st.GetFrame(st.FrameCount - 1).GetFileName();
            var line = st.GetFrame(st.FrameCount - 1).GetFileLineNumber();
            GD.PrintErr($"Debug inspector: failed to set the path {fullPath}.\n{e.Message}, {file}:{line}");
        }
    }

    public void PathsRefresh()
    {
        CreateAllPathsList();
    }

    public void DeleteInspector()
    {
        debug.DeleteInspector(name);
    }
    #endregion
}
