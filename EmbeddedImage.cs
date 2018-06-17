using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VkClient
{
    public class EmbeddedImage
    {
        public string SourceUrl { get; set; }
        public string SourceID { get; set; }
        public string Url { get; set; }
        public Bitmap Image { get; set; }
    }
}
