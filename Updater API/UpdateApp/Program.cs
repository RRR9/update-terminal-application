using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using log4net.Config;
using System.IO;
using System;

namespace UpdateApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            XmlConfigurator.Configure(new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log4net.config")));
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
