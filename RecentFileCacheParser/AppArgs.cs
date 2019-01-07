namespace RecentFileCacheParser
{
    public class AppArgs
    {
        public string File { get; set; }
        public string JsonDirectory { get; set; }
        public bool JsonPretty { get; set; }

        public string CsvDirectory { get; set; }
        public string CsvName { get; set; }

        public bool Quiet { get; set; }

        
    }
}