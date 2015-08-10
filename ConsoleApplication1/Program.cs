using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WpfApplication1;
using System.IO;

namespace ConsoleApplication1
{
    class Program
    {
        static void Main(string[] args)
        {
            string url = "https://koti.kapsi.fi/darkon/polloeskadroona/repo/updater.json";
            Repository r = new WpfApplication1.Repository(url);
            r.Sync();
            
        }
    }
}
