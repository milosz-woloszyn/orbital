using Godot;

namespace Orbital;

/// <summary>Shared visual vocabulary; layout and simulation stay independent.</summary>
public static class Style
{
    public static readonly Color Background = new("080e19");
    public static readonly Color Panel = new("101c2b");
    public static readonly Color Border = new("27394b");
    public static readonly Color Text = new("e1ebef");
    public static readonly Color Muted = new("8194a8");
    public static readonly Color Cyan = new("68e5df");
    public static readonly Color Amber = new("ffc66b");
    public static readonly Color Green = new("95ef95");
    public static readonly Color Red = new("ff7b86");
    public static Font Display { get; private set; } = null!;
    public static Font Mono { get; private set; } = null!;

    public static Theme CreateTheme()
    {
        Display = GD.Load<Font>("res://Assets/Fonts/Display.ttf");
        Mono = GD.Load<Font>("res://Assets/Fonts/Mono.ttf");
        var theme = new Theme { DefaultFont = Display, DefaultFontSize = 16 };
        foreach (var type in new[] { "Button", "OptionButton", "LineEdit", "SpinBox" })
        {
            theme.SetStylebox("normal", type, Box(Panel, Border));
            theme.SetStylebox("hover", type, Box(new Color("1d3443"), Cyan.Darkened(.45f)));
            theme.SetStylebox("pressed", type, Box(new Color("24474e"), Cyan));
            theme.SetStylebox("focus", type, Box(Colors.Transparent, Cyan, 2));
            theme.SetStylebox("disabled", type, Box(Panel.Darkened(.25f), Border.Darkened(.4f)));
            theme.SetColor("font_color", type, Text);
            theme.SetColor("font_hover_color", type, Colors.White);
            theme.SetColor("font_pressed_color", type, Cyan);
            theme.SetColor("font_disabled_color", type, Muted.Darkened(.35f));
        }
        theme.SetColor("font_color", "Label", Text);
        theme.SetStylebox("panel", "PanelContainer", Box(Panel, Border));
        theme.SetStylebox("panel", "PopupMenu", Box(Panel, Border));
        theme.SetStylebox("panel", "TooltipPanel", Box(new Color("203040"), Cyan.Darkened(.5f)));
        theme.SetFontSize("font_size", "TooltipLabel", 15);
        return theme;
    }

    public static StyleBoxFlat Box(Color color, Color border, int thickness = 1)
    {
        return new StyleBoxFlat
        {
            BgColor = color, BorderColor = border,
            BorderWidthLeft = thickness, BorderWidthTop = thickness,
            BorderWidthRight = thickness, BorderWidthBottom = thickness,
            CornerRadiusTopLeft = 7, CornerRadiusTopRight = 7,
            CornerRadiusBottomLeft = 7, CornerRadiusBottomRight = 7,
            ContentMarginLeft = 12, ContentMarginRight = 12,
            ContentMarginTop = 8, ContentMarginBottom = 8
        };
    }

    public static T Rect<T>(T node, float x, float y, float w, float h) where T : Control
    { node.Position = new(x, y); node.Size = new(w, h); return node; }

    // Preserve panel and font sizes while the available canvas grows.
    public static T Right<T>(T node) where T : Control
    {
        float x=node.Position.X, width=node.Size.X, parent=((Control)node.GetParent()).Size.X;
        node.AnchorLeft=node.AnchorRight=1;node.OffsetLeft=x-parent;node.OffsetRight=x+width-parent;
        return node;
    }
    public static T Bottom<T>(T node) where T : Control
    {
        float y=node.Position.Y, height=node.Size.Y, parent=((Control)node.GetParent()).Size.Y;
        node.AnchorTop=node.AnchorBottom=1;node.OffsetTop=y-parent;node.OffsetBottom=y+height-parent;
        return node;
    }
    public static T StretchRight<T>(T node) where T : Control
    {
        float end=node.Position.X+node.Size.X;
        node.AnchorRight=1;node.OffsetRight=end-((Control)node.GetParent()).Size.X;return node;
    }
    public static T StretchBottom<T>(T node) where T : Control
    {
        float end=node.Position.Y+node.Size.Y;
        node.AnchorBottom=1;node.OffsetBottom=end-((Control)node.GetParent()).Size.Y;return node;
    }
    public static Control Group(Control parent,float x,float y,float width,float height)
    {
        var group=Rect(new Control { MouseFilter=Control.MouseFilterEnum.Ignore },x,y,width,height);
        parent.AddChild(group);return group;
    }

    public static Label Label(Control parent, string text, float x, float y, float w, float h = 26,
        int size = 16, Color? color = null, bool mono = false)
    {
        var label = Rect(new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore }, x,y,w,h);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color ?? Text);
        if (mono) label.AddThemeFontOverride("font", Mono);
        parent.AddChild(label);
        return label;
    }

    public static Button Button(Control parent, string text, float x,float y,float w,float h, Action action,
        bool primary = false)
    {
        var button = Rect(new Button { Text = text, MouseDefaultCursorShape = Control.CursorShape.PointingHand }, x,y,w,h);
        if (primary)
        {
            button.AddThemeStyleboxOverride("normal", Box(Cyan, Cyan));
            button.AddThemeStyleboxOverride("hover", Box(Cyan.Lightened(.15f), Cyan));
            button.AddThemeColorOverride("font_color", Background);
            button.AddThemeColorOverride("font_hover_color", Background);
        }
        button.Pressed += action;
        parent.AddChild(button);
        return button;
    }

    public static Panel PanelAt(Control parent,float x,float y,float w,float h)
    {
        var panel=Rect(new Panel { MouseFilter=Control.MouseFilterEnum.Ignore },x,y,w,h);
        panel.AddThemeStyleboxOverride("panel",Box(Panel,Border));
        parent.AddChild(panel); return panel;
    }
}
