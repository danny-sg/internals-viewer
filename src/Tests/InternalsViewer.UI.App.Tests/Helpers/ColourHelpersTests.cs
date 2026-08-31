using System.Drawing;
using InternalsViewer.UI.App.Helpers;

namespace InternalsViewer.UI.App.Tests.Helpers;

[Trait("Category", "Unit")]
[Trait("Area", "Helpers")]
public class ColourHelpersTests
{
    [Fact]
    public void A_Colour_Far_From_Its_Background_Is_Kept()
    {
        var slot = Color.Red.ContrastingWith(Color.Navy);

        Assert.Equal(Color.Red.ToArgb(), slot.ToArgb());
    }

    [Fact]
    public void A_Colour_Drawn_On_Itself_Falls_Back_To_A_Contrasting_One()
    {
        var slot = Color.Red.ContrastingWith(Color.Red);

        Assert.NotEqual(Color.Red.ToArgb(), slot.ToArgb());
    }

    [Fact]
    public void A_Dark_Background_Takes_White()
    {
        var slot = Color.Red.ContrastingWith(Color.DarkRed);

        Assert.Equal(Color.FromArgb(255, 255, 255, 255).ToArgb(), slot.ToArgb());
    }

    [Fact]
    public void A_Light_Background_Takes_Black()
    {
        var slot = Color.White.ContrastingWith(Color.White);

        Assert.Equal(Color.FromArgb(255, 0, 0, 0).ToArgb(), slot.ToArgb());
    }

    [Theory]
    [InlineData(nameof(Color.Navy))]
    [InlineData(nameof(Color.Green))]
    [InlineData(nameof(Color.Teal))]
    [InlineData(nameof(Color.Orange))]
    public void Every_Object_Colour_Leaves_The_Marker_Visible(string background)
    {
        var colour = Color.FromName(background);

        var slot = Color.Red.ContrastingWith(colour);

        Assert.True(Distance(slot, colour) > 150, $"{slot.Name} is not visible on {background}");
    }

    private static double Distance(Color left, Color right)
    {
        var redMean = (left.R + right.R) / 2.0;

        double red = left.R - right.R;
        double green = left.G - right.G;
        double blue = left.B - right.B;

        return Math.Sqrt((((512 + redMean) * red * red) / 256)
                         + (4 * green * green)
                         + (((767 - redMean) * blue * blue) / 256));
    }
}
