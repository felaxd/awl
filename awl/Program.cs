using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace awl
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (Directory.Exists(@"wwwroot/TempFiles"))
            {
                DirectoryInfo di = new DirectoryInfo("wwwroot/TempFiles");
                if (di.GetFiles().Length > 0) foreach (FileInfo file in di.GetFiles()) file.Delete();
            }
            else Directory.CreateDirectory(@"wwwroot/TempFiles");
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
