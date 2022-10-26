using System;

namespace TestTask_TraineeDotNet_Denis_Bilash
{
    public class Program
    {

        

        static void Main(string[] args)
        {
            string separator = "++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++" +
            "++++++++++++++++++++++++++++++++++++++++++++++++++++++";

            LinksSearcher searcher = new LinksSearcher();

            while (true)
            {
                Console.WriteLine("\nPlease, enter the link below:\n");
                string inputString = Console.ReadLine();
                Console.WriteLine(separator);

                try
                {
                    searcher.CrawlSiteLinks(inputString);
                }
                catch (ArgumentException ex)
                {
                    Console.WriteLine(ex.Message);
                }
                catch (AggregateException ex) 
                {
                    Console.WriteLine(ex.Message);
                }

                if (searcher.CanPrint)
                {
                    searcher.PrintCrawledExceptSitemap();
                    Console.WriteLine("\n");

                    searcher.PrintSitemapExceptCrawled();
                    Console.WriteLine("\n-");

                    searcher.PrintAllLinksWithUptime();
                    Console.WriteLine("\n");

                    searcher.PrintStats();
                }
                searcher.Clear();
            }

        }
    }
}
