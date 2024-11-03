using Vintagestory.API.Client;

namespace ChestOrganizer;
public class HoverText {
    private readonly double delay;
    private readonly GuiElementHoverText element;
    private string text;
    private double hoverTime = 0.0;
    private bool dirty = false;

    public HoverText(ICoreClientAPI api, string text, double delay = 1.0, int maxWidth = 200) {
        this.text = text;
        this.delay = delay;

        var bounds = ElementBounds.Fixed(0.0, 0.0, 1.0, 1.0).WithParent(ElementBounds.Empty);
        element = new(api, text, CairoFont.WhiteDetailText(), maxWidth, bounds);
        element.SetAutoDisplay(false);
        element.SetVisible(true);
        element.SetAutoWidth(true);
    }

    public string Text {
        get => text; 
        set {
            if (text != value) {
                text = value;
                dirty = true;
            }
        } 
    }

    public void Render(bool inside, float deltaTime) {
        if (inside) {
            hoverTime += deltaTime;
        } else {
            hoverTime = 0.0;
        }
        if (hoverTime >= delay) {
            if (dirty) {
                element.SetNewText(text);
            }
            element.RenderInteractiveElements(deltaTime);
        }
    }

    public void Dispose() 
        => element.Dispose();
}
