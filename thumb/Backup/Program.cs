using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Diagnostics;

namespace GetSiteThumbnail
{
    class Program
    {


        public delegate void WebBrowserDocumentCompletedEventHandler(object sender, WebBrowserDocumentCompletedEventArgs e);
        
        [STAThread]
        static void Main(string[] args)
        {
            
            if (args.Length < 2)
            {
                MessageBox.Show("Usage:\nGetSiteThumbnail.exe http://www.yoursite.com/ thumbnail.jpg [browser_width(defaults to 800) browser_height (defaults to 600) ] [thumbnail_width thumbnail_height]\n\n"+
                    "Sample:\nGetSiteThumbnail.exe http://www.cognifide.com/ cognifide.jpg 1280 1024 640 480\n\n",
                    "Get Site Thumbnail");               
                return;
            }
            
            int width = 800;
            int height = 600;
            if (args.Length > 2)
            {
                width = Int32.Parse(args[2]);
                height = Int32.Parse(args[3]);
            }

            int thumbwidth = width;
            int thumbheight = height;

            if (args.Length > 4)
            {
                thumbwidth = Int32.Parse(args[4]);
                thumbheight = Int32.Parse(args[5]);
            }
            
            WebPageBitmap webBitmap = new WebPageBitmap(args[0], width, height, false, 10000);
            if (webBitmap.IsOk)
            {
                webBitmap.Fetch();
                Bitmap thumbnail = webBitmap.GetBitmap(thumbwidth, thumbheight);
                thumbnail.Save(args[1], ImageFormat.Jpeg);
                thumbnail.Dispose();
            }
            else
            {
                MessageBox.Show(webBitmap.ErrorMessage);
            }
        }
    }
}
