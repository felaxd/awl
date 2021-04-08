namespace awl.Pages
{
    public class Error
    {
        public string key { get; set; }
        public string name { get; set; }
        public string type { get; set; }
        public string samples { get; set; }
        public string help { get; set; }
        public void addError(string key, string err, string type, string help, string samples = null)
        {
            this.key = key;
            this.name = err;
            this.type = type;
            this.samples = samples;
            this.help = help;
        }
    }
}