using System;
using System.Drawing;
using InternalsViewer.Internals.Engine.Allocation.Enums;
using InternalsViewer.UI.App.Helpers;
using SkiaSharp;
using static System.Net.Mime.MediaTypeNames;

namespace InternalsViewer.UI.App.Controls.Allocation;

public class PfsRenderer : IDisposable
{
    private const string IamFlag = "I";

    public Size IamFlagSize { get; }

    public Size PageSize { get; }

    private SKPaint SpaceFreePaint { get; }

    private SKPaint IamFlagPaint { get; }

    public PfsRenderer(Size pageSize)
    {
        PageSize = pageSize;
        SpaceFreePaint = GetSpaceFreePaint(Color.FromArgb(120, 255, 255, 255));

        IamFlagPaint = new()
        {
            Color = SKColors.Gray,
            IsAntialias = true,
            TextSize = pageSize.Height,
            Typeface = SKTypeface.FromFamilyName(
                familyName: "Consolas",
                weight: SKFontStyleWeight.SemiBold,
                width: SKFontStyleWidth.Normal,
                slant: SKFontStyleSlant.Upright),
        };

        SKRect textSize = new();

        IamFlagPaint.MeasureText(IamFlag, ref textSize);

        IamFlagSize = new Size((int)textSize.Width, (int)textSize.Height);
    }

    public void DrawPfs(SKCanvas canvas, SKRect position, PfsByte value)
    {
        var pageRect = position;

        switch (value.PageSpaceFree)
        {
            case SpaceFree.Empty:
                break;
            case SpaceFree.NinetyFivePercent:
                pageRect = position with { Top = position.Top + (position.Height * .05F) };
                break;
            case SpaceFree.EightyPercent:
                pageRect = position with { Top = position.Top + (position.Height * .2F) };
                break;
            case SpaceFree.FiftyPercent:
                pageRect = position with { Top = position.Top + (position.Height * .5F) };
                break;
            case SpaceFree.OneHundredPercent:
                pageRect = position;
                break;
        }

        if (value.PageSpaceFree != SpaceFree.Empty)
        {
            canvas.DrawRect(pageRect, SpaceFreePaint);
        }

        if (value.IsIam)
        {
            var leftOffset = (position.Width - IamFlagSize.Width) / 2F;
            var bottomOffset = (position.Height + IamFlagSize.Height) / 2F;

            canvas.DrawText("I", position.Left + leftOffset, position.Top + bottomOffset, IamFlagPaint);
        }
    }

    private SKPaint GetSpaceFreePaint(Color colour)
    {
        var paint = new SKPaint
        {
            Color = colour.ToSkColor(),
            Style = SKPaintStyle.Fill,
        };

        return paint;
    }

    public void Dispose()
    {
        SpaceFreePaint.Dispose();
        IamFlagPaint.Dispose();
    }
}