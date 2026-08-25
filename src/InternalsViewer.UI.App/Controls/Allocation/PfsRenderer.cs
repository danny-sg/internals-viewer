using System;
using System.Drawing;
using InternalsViewer.Internals.Engine.Allocation.Enums;
using InternalsViewer.UI.App.Helpers;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Allocation;

public sealed class PfsRenderer : IDisposable
{
    private const string IamFlag = "I";

    private const float EyeMinRenderSize = 24F;

    private static readonly SKRect LeftEye = new(0.30f, 0.33f, 0.45f, 0.58f);

    private static readonly SKRect RightEye = new(0.55f, 0.33f, 0.70f, 0.58f);

    private static readonly Color SpaceColour = Color.FromArgb(100, 0, 0, 91);

    private static readonly Color AllocatedColour = Color.FromArgb(50, 0, 0, 91);

    private static readonly Color GhostColour = Color.FromArgb(140, 0, 200, 0);

    private static readonly SKTypeface IamFlagTypeface = SKTypeface.FromFamilyName(
        familyName: "Consolas",
        weight: SKFontStyleWeight.SemiBold,
        width: SKFontStyleWidth.Normal,
        slant: SKFontStyleSlant.Upright);

    private readonly SKPath _ghostPath;

    public PfsRenderer(Size pageSize)
    {
        PageSize = pageSize;
        SpaceFreePaint = GetPagePaint(SpaceColour.ToSkColor());
        AllocatedPaint = GetPagePaint(AllocatedColour.ToSkColor());

        IamFlagFont = new SKFont(IamFlagTypeface, size: pageSize.Height);

        IamFlagPaint = new()
        {
            Color = SKColors.Gray,
            IsAntialias = true,
        };

        IamFlagFont.MeasureText(IamFlag, out var textBounds);

        IamFlagSize = new Size((int)textBounds.Width, (int)textBounds.Height);

        GhostPaint = new SKPaint
        {
            Color = GhostColour.ToSkColor(),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        GhostLightenPaint = new SKPaint
        {
            Color = GhostColour.ToSkColor(),
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
            BlendMode = SKBlendMode.Screen
        };

        GhostEyePaint = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        _ghostPath = BuildGhostPath();
    }

    public Size IamFlagSize { get; }

    public Size PageSize { get; }

    private SKPaint SpaceFreePaint { get; }

    private SKPaint AllocatedPaint { get; }

    private SKFont IamFlagFont { get; }

    private SKPaint IamFlagPaint { get; }

    private SKPaint GhostPaint { get; }

    private SKPaint GhostLightenPaint { get; }

    private SKPaint GhostEyePaint { get; }

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

        if (value.IsAllocated)
        {
            canvas.DrawRect(position, AllocatedPaint);
        }

        if (value.IsIam)
        {
            var leftOffset = (position.Width - IamFlagSize.Width) / 2F;
            var bottomOffset = (position.Height + IamFlagSize.Height) / 2F;

            canvas.DrawText("I", position.Left + leftOffset, position.Top + bottomOffset, SKTextAlign.Left, IamFlagFont, IamFlagPaint);
        }

        if (value.GhostRecords)
        {
            const float padding = 1F;

            var width = position.Width - 2 * padding;
            var height = position.Height - 2 * padding;

            if (width > 0 && height > 0)
            {
                canvas.Save();

                canvas.Translate(position.Left + padding, position.Top + padding);
                canvas.Scale(width, height);

                canvas.DrawPath(_ghostPath, GhostPaint);
                canvas.DrawPath(_ghostPath, GhostLightenPaint);

                if (Math.Min(position.Width, position.Height) >= EyeMinRenderSize)
                {
                    canvas.DrawOval(LeftEye, GhostEyePaint);
                    canvas.DrawOval(RightEye, GhostEyePaint);
                }

                canvas.Restore();
            }
        }
    }

    public void Dispose()
    {
        SpaceFreePaint.Dispose();
        IamFlagFont.Dispose();
        IamFlagPaint.Dispose();
        GhostPaint.Dispose();
        GhostLightenPaint.Dispose();
        GhostEyePaint.Dispose();
        _ghostPath.Dispose();
        AllocatedPaint.Dispose();
    }

    private static SKPath BuildGhostPath()
    {
        using var builder = new SKPathBuilder();

        builder.MoveTo(0.15f, 0.45f);

        // Dome over the top (start at the left point, sweep 180 degrees through the top to the right point)
        builder.ArcTo(new SKRect(0.15f, 0.10f, 0.85f, 0.80f), 180, 180, false);

        builder.LineTo(0.85f, 0.82f);

        // Scalloped bottom, right to left
        builder.QuadTo(0.733f, 0.98f, 0.617f, 0.82f);
        builder.QuadTo(0.500f, 0.98f, 0.383f, 0.82f);
        builder.QuadTo(0.267f, 0.98f, 0.150f, 0.82f);

        builder.Close();

        return builder.Detach();
    }

    private static SKPaint GetPagePaint(SKColor colour)
    {
        var paint = new SKPaint
        {
            Color = colour,
            Style = SKPaintStyle.Fill,
        };

        return paint;
    }
}