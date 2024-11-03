using Cairo;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace ChestOrganizer;
using IconFunc = Action<Context, double, double, double, double, double[]>;
using DrawFunc = Action<Context, Action<Context, double, double, double, double, double[]>, Rectangled>;

public static class Icons {
    private static ICoreClientAPI api;

    private static double[] mainColor = GuiStyle.DialogDefaultTextColor;
    private static readonly double[] shadowColor = new double[4] { 0.0, 0.0, 0.0, 0.3 };
    private static readonly double[] hoverColor1 = new double[4] { 0.8, 0.0, 0.0, 1.0 };
    private static readonly double[] hoverColor2 = new double[4] { 0.8, 0.0, 0.0, 0.6 };

    public static void Setup(ICoreClientAPI api) {
        Icons.api = api;
    }

    public static void Draw(Context context, IconFunc icon, Rectangled rect) { 
        context.Operator = Operator.Over;
        icon(context, rect.X + 4.0, rect.Y + 4.0, rect.Width - 4.0, rect.Height - 4.0, shadowColor);
        context.Operator = Operator.Source;
        icon(context, rect.X + 2.0, rect.Y + 2.0, rect.Width - 4.0, rect.Height - 4.0, mainColor);
        context.Operator = Operator.Over;
    }

    public static void DrawHover(Context context, IconFunc icon, Rectangled rect) { 
        context.Operator = Operator.Source;
        icon(context, rect.X + 1.5, rect.Y + 1.5, rect.Width - 4.0, rect.Height - 4.0, hoverColor1);
        icon(context, rect.X + 2.0, rect.Y + 2.0, rect.Width - 4.0, rect.Height - 4.0, hoverColor2);
    }

    public static void MakeContext(int width, int height, out Context context, out ImageSurface surface) {
        surface = new ImageSurface(Format.Argb32, width, height);
        context = new Context(surface);
        context.SetSourceRGBA(0.0, 0.0, 0.0, 0.0);
        context.Paint();
        context.Antialias = Antialias.Best;
    }

    public static void MakeTexture(ref LoadedTexture texture, Action<Context> draw, int width, int height, bool white = true) {
        mainColor = white ? GuiStyle.DialogDefaultTextColor : GuiStyle.ColorSchematic;
        MakeContext(width, height, out var context, out var surface);
        draw(context);
        CompleteTexture(ref texture, context, surface);
    }

    public static Pattern CopyFrom(ImageSurface surface, double x, double y) {
        float scale = RuntimeEnv.GUIScale;
        var pattern = new SurfacePattern(surface);
        Matrix matrix = pattern.Matrix;
        matrix.Scale(scale, scale);
        matrix.Translate(x, y);
        pattern.Matrix = matrix;
        return pattern;
    }

    public static void CompleteTexture(ref LoadedTexture texture, Context context, ImageSurface surface) {
        api.Gui.LoadOrUpdateCairoTexture(surface, true, ref texture);
        surface.Dispose();
        context.Dispose();
    }

    public static void MakeTexture(ref LoadedTexture texture, DrawFunc draw, IconFunc icon, int size, bool white = true) 
        => MakeTexture(ref texture, ctx => draw(ctx, icon, new(0.0, 0.0, size, size)), size, size, white);

    public static void MakeTexture(ref LoadedTexture texture, DrawFunc draw, IconFunc icon, int width, int height, bool white = true) 
        => MakeTexture(ref texture, ctx => draw(ctx, icon, new(0.0, 0.0, width, height)), width, height, white);

    private static Pattern SetupContext(Context context, double x, double y, double width, double height, double[] color, double iconWidth, double iconHeight) {
        Pattern pattern = new SolidPattern(color[0], color[1], color[2], color[3]);
        Matrix matrix = context.Matrix;

        double scale = Math.Min(width / iconWidth, height / iconHeight);
        matrix.Translate(x + Math.Max(0, (width - iconWidth * scale) / 2), y + Math.Max(0f, (height - iconHeight * scale) / 2));
        matrix.Scale(scale, scale);

        context.Matrix = matrix;
        context.LineWidth = 20.0;
        context.MiterLimit = 10.0;
        context.LineCap = LineCap.Butt;
        context.LineJoin = LineJoin.Miter;
        context.Tolerance = 0.1;
        context.Antialias = Antialias.Default;
        context.SetSource(pattern);

        return pattern;
    }

    public static void Cross(Context context, double x, double y, double size, double _, double[] color) {
        double lineWidth = size / 6;
        context.SetSourceRGBA(color);
        api.Gui.Icons.DrawCross(context, x, y, lineWidth, size - 4);
    }

    public static void Sort(Context context, double x, double y, double width, double height, double[] color) {
        context.Save();
        Pattern pattern = SetupContext(context, x, y, width, height, color, 158, 144);

        context.NewPath();
        for (int i = 0; i < 3; i++) {
            double lineX = 158 - i * 52;
            double lineY =  14 + i * 58;

            context.MoveTo(    0, lineY);
            context.LineTo(lineX, lineY);
        }
        context.StrokePreserve();

        pattern.Dispose();
        context.Restore();
    }

    public static void Find(Context context, double x, double y, double width, double height, double[] color) {
        context.Save();
        Pattern pattern = SetupContext(context, x, y, width, height, color, 158, 158);

        context.NewPath();
        context.MoveTo(154, 154);
        context.LineTo(112, 112);
        context.Arc(56, 56, 56, 0.25 * Math.PI, 2.25 * Math.PI);
        context.StrokePreserve();

        pattern.Dispose();
        context.Restore();
    }

    public static void Merge(Context context, double x, double y, double width, double height, double[] color) {
        context.Save();
        Pattern pattern = SetupContext(context, x, y, width, height, color, 158, 128);

        context.NewPath();
        context.MoveTo( 79,   0);
        context.LineTo( 79, 128);
        context.MoveTo(  0,  64);
        context.LineTo( 16,  64);
        context.MoveTo(142,  64);
        context.LineTo(158,  64);
        context.Stroke();

        context.MoveTo( 16,  23);
        context.LineTo( 69,  64);
        context.LineTo( 16, 105);
        context.Fill();

        context.MoveTo(142,  23);
        context.LineTo( 89,  64);
        context.LineTo(142, 105);
        context.Fill();

        pattern.Dispose();
        context.Restore();
    }

    public static void Split(Context context, double x, double y, double width, double height, double[] color) {
        context.Save();
        Pattern pattern = SetupContext(context, x, y, width, height, color, 158, 128);

        context.NewPath();
        context.MoveTo( 79,   0);
        context.LineTo( 79, 128);
        context.MoveTo( 53,  64);
        context.LineTo(105,  64);
        context.Stroke();

        context.MoveTo( 53,  23);
        context.LineTo(  0,  64);
        context.LineTo( 53, 105);
        context.Fill();

        context.MoveTo(105,  23);
        context.LineTo(168,  64);
        context.LineTo(105, 105);
        context.Fill();

        pattern.Dispose();
        context.Restore();
    }

    public static void Divider(Context context, double x, double y, double width, double height, double[] color) {
        context.Save();
        Pattern pattern = SetupContext(context, x, y, width, height, color, 1000, 100);
        context.LineWidth = 16;

        context.NewPath();
        context.MoveTo( 10, 10);
        context.LineTo( 30, 50);
        context.LineTo( 10, 90);
        context.MoveTo( 30, 50);
        context.LineTo(970, 50);
        context.LineTo(990, 10);
        context.MoveTo(970, 50);
        context.LineTo(990, 90);
        context.Stroke();

        pattern.Dispose();
        context.Restore();
    }
}
