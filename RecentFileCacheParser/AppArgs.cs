namespace RecentFileCacheParser
{
    public class AppArgs
    {
        public string File { get; set; }
        public string JsonDirectory { get; set; }
        public bool JsonPretty { get; set; }

        public string CsvDirectory { get; set; }

        public bool Quiet { get; set; }

        public bool CsvSeparator { get; set; }
    }
}