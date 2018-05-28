using System.Drawing;
using System.Windows.Forms;
using stdole;

namespace InternalsViewer.SSMSAddIn
{
    class ImageConverter : AxHost
    {
        internal ImageConverter()

            : base("52D64AAC-29C1-CAC8-BB3A-115F0D3D77CB")
        {
        }

        public static IPictureDisp GetIPictureDispFromImage(Image image)
        {
            return (IPictureDisp)AxHost.GetIPictureDispFromPicture(image);
        }

    }
}
