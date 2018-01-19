using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RecentFileCacheParser
{
  public  class AppArgs
    {
        public string File { get; set; }
        public string JsonDirectory { get; set; }
        public bool JsonPretty { get; set; }

        public string CsvDirectory { get; set; }
        public string XmlDirectory { get; set; }

        public bool Quiet { get; set; }
    }
}
