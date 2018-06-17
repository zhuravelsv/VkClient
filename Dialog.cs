using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VkClient
{
    public class Dialog
    {
        public string Url { get; set; }
        public string Name { get; set; }
        public List<Message> Messages { get; set; }

        public Dialog()
        {
            Messages = new List<Message>();
            Url = "";
            Name = "";
        }

        public override string ToString()
        {
            return $"{Name} ({Url})";
        }

        public override bool Equals(object obj)
        {
            Dialog d = obj as Dialog;

            if (d == null)
                return false;

            return (d.Name?.Equals(Name) ?? Name == null) && (d.Url?.Equals(Url) ?? Url == null);
        }
    }
}
