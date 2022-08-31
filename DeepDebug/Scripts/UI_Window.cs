using Godot;
using System;
using static Godot.Control;
using Array = Godot.Collections.Array;

public class UI_Window : Panel
{
    public delegate void WindowEvent();
    public WindowEvent OnDelete;

    public Control container;

    [Export] Vector2 panelScaleRange = new Vector2(0.5f, 1.5f);

    Button moveButton;
    Button scaleButton;
    Button resizeButton;
    bool move;
    bool scale;
    bool resize;

    PopupPanel deletePanel;
    Button deleteButton;
    bool deleteMouseOut;

    public LineEdit panelName;
    Button closeButton;
    Button minimizeButton;

    float rectX;

    Vector2 posMax;
    Vector2 posDef;
    Vector2 sizeDef;

    public override void _Input(InputEvent @event)
    {
        if (move)
        {
            if (@event is InputEventMouseMotion eventMouseMotion)
            {
                RectPosition += eventMouseMotion.Relative;
                RectPosition = new Vector2(Mathf.Clamp(RectPosition.x, 0, (posMax.x - moveButton.RectSize.x)/RectScale.x), 
                                           Mathf.Clamp(RectPosition.y, 0, (posMax.y - moveButton.RectSize.y)/RectScale.y));
            }
        }
        if (resize)
        {
            if (@event is InputEventMouseMotion eventMouseMotion)
            {
                RectSize += eventMouseMotion.Relative / RectScale;
                RectSize = new Vector2(Mathf.Clamp(RectSize.x, 0, (posMax.x - RectPosition.x)/RectScale.x), 
                                       Mathf.Clamp(RectSize.y, 0, (posMax.y - RectPosition.y)/RectScale.y));
            }
        }
        if (scale)
        {
            if (@event is InputEventMouseMotion eventMouseMotion)
            {
                RectScale += Vector2.One * eventMouseMotion.Relative.x * 0.01f;
                float scale = Mathf.Clamp(RectScale.x, panelScaleRange.x, panelScaleRange.y);
                RectScale = new Vector2(scale, scale);
            }
        }
    }

    public override void _Ready()
    {
        container = GetNode("Container") as Control;

        moveButton   = GetNode("Move")   as Button;
        scaleButton  = container.GetNode("Scale")  as Button;
        resizeButton = container.GetNode("Resize") as Button;
        moveButton.Connect  ("button_down", this, nameof(PanelTransform), new Array {0, true });
        moveButton.Connect  ("button_up",   this, nameof(PanelTransform), new Array {0, false});
        moveButton.Connect  ("gui_input",   this, nameof(PanelReset), new Array {0});
        scaleButton.Connect ("button_down", this, nameof(PanelTransform), new Array {1, true });
        scaleButton.Connect ("button_up",   this, nameof(PanelTransform), new Array {1, false});
        scaleButton.Connect  ("gui_input",  this, nameof(PanelReset), new Array {1});
        resizeButton.Connect("button_down", this, nameof(PanelTransform), new Array {2, true });
        resizeButton.Connect("button_up",   this, nameof(PanelTransform), new Array {2, false});
        resizeButton.Connect  ("gui_input",  this, nameof(PanelReset), new Array {2});

        deletePanel = GetNode("Delete") as PopupPanel;
        deleteButton = deletePanel.GetNode("Panel/Confirm") as Button;
        deleteButton.Connect("pressed", this, nameof(Delete));

        minimizeButton = GetNode("Minimize")  as Button;
        closeButton    = GetNode("Close")     as Button;
        minimizeButton.Connect("pressed", this, nameof(SwitchWindow), new Array { 0 });
        closeButton.Connect("pressed", this, nameof(SwitchWindow), new Array { 1 });

        panelName = GetNode("Name") as LineEdit;

        posMax = OS.WindowSize;
        posDef = RectPosition;
        sizeDef = RectSize;
    }

    public void PanelTransform(int transform, bool start)
    {
        switch (transform)
        {
            case 0:
                move = start;
            break;
            case 1:
                scale = start;
            break;
            case 2:
                resize = start;
            break;
        }
    }

    public void PanelReset(InputEvent InputEvent, int transform)
    {
        if(InputEvent is InputEventMouseButton && InputEvent.IsPressed())
        {
            if (((InputEventMouseButton)InputEvent).ButtonIndex == (int)ButtonList.Right)
            {
                switch (transform)
                {
                    case 0:
                        RectPosition = posDef;
                        RectScale = new Vector2(1, 1);
                        RectSize = sizeDef;
                        break;
                    case 1:
                        RectScale = new Vector2(1, 1);
                        break;
                    case 2:
                        RectSize = sizeDef;
                        break;
                }
            }
        }
    }

    public void SwitchWindow(int set)
    {
        switch (set)
        {
            case 0:
                if (container.Visible) 
                { 
                    rectX = RectSize.x;
                    RectSize = new Vector2(200, RectSize.y);
                }
                else
                {
                    RectSize = new Vector2(rectX, RectSize.y);
                }
                container.Visible = !container.Visible;
                break;
            case 1:
                deleteMouseOut = true;
                deletePanel.PopupCentered();
                break;
        }
    }

    public void Delete()
    {
        OnDelete?.Invoke();
        QueueFree();
    }
}
