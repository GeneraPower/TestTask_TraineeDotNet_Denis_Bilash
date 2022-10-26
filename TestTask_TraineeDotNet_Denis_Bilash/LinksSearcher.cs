using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Mime;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace TestTask_TraineeDotNet_Denis_Bilash
{
    public class LinksSearcher
    {
        private static readonly string[] allowedSchemes = { "http", "https" };

        string verticalBoundary = "----------------------------------------------------------------------------------" +
                "--------------------------------------------------------------------";

        private Uri initialLink = null;
        private string initialLinkPath = null;

        private readonly HttpClient httpClient = new HttpClient();

        private readonly HashSet<WebPage> crawledLinks = new HashSet<WebPage>();
        private readonly HashSet<WebPage> sitemapLinks = new HashSet<WebPage>();

        public bool CanPrint { get; set; } = false;

        public LinksSearcher()
        {
            initialLink = null;
        }

        public void CrawlSiteLinks(string webPageUrl)
        {
            string inputLink = webPageUrl.TrimEnd('/');

            Clear();

            try
            {
                SetLink(inputLink);
                CrawlPageLinks();
                GetAllLinksFromSitemap();
            }
            catch (ArgumentException) 
            {
                throw;
            }
            catch (System.AggregateException ex) 
            {
                throw new ArgumentException("Custom encodings are not supported", ex);
            }

            CanPrint = true;
        }

        public void Clear() 
        {
            initialLink = null;
            initialLinkPath = null;

            crawledLinks.Clear();
            sitemapLinks.Clear();

            CanPrint = false;
        }

        private bool SetLink(string inputLink)
        {
            if (!CreateUriFromGivenLink(inputLink))
            {
                throw new ArgumentException("Error! Invalid link given", "webPageUrl");
            }

            if (!CheckLinkScheme())
            {
                throw new ArgumentException("Error! Link must support http or https scheme and must not be a file.", "webPageUrl");
            }

            initialLinkPath = initialLink.Scheme + @"://" + initialLink.Host;
            

            return true;
        }

        private bool CreateUriFromGivenLink(string webPageUrl)
        {
            return Uri.TryCreate(webPageUrl, UriKind.Absolute, out initialLink);
        }

        private bool CheckLinkScheme()
        {
            if (!initialLink.IsFile)
            {
                foreach (string scheme in allowedSchemes)
                {
                    if (scheme.Equals(initialLink.Scheme))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void CrawlPageLinks()
        {
            if (initialLink == null) 
            {
                throw new InvalidOperationException("You must call SetLink() before using this method");
            }

            try
            {
                GetLinksFromGivenUrl(initialLink.OriginalString);
            }
            catch (System.AggregateException) 
            {
                throw;
            }
        }

        private void GetLinksFromGivenUrl(string pageLink)
        {
            string htmlString;
            string linkString;

            HashSet<string> linksToCheck = new HashSet<string>();
            linksToCheck.Add(pageLink);

                do
                {
                    linkString = linksToCheck.Last();
                    linksToCheck.Remove(linkString);

                    if (linkString.StartsWith(@"//"))
                    {
                        continue;
                    }
                    else if (linkString.StartsWith(@"/"))
                    {
                        linkString = initialLinkPath + linkString;
                    }

                    if (!linkString.StartsWith(initialLinkPath))
                    {
                        continue;
                    }

                    linkString = linkString.TrimEnd('/');

                    if (!crawledLinks.Contains(new WebPage(linkString)))
                    {
                         try
                        {
                            htmlString = GetHtmlSource(linkString);
                        }
                         catch (System.AggregateException) 
                        {
                            throw;
                        
                    }
                        ParseHtmlForLinks(htmlString, linksToCheck);
                    }
                }
                while (linksToCheck.Count != 0);
        }

        Stopwatch timer = new Stopwatch();
        private string GetHtmlSource(string url)
        {
            timer.Start();
            var response = httpClient.GetAsync(url);
            timer.Stop();

            long accessTime = timer.ElapsedMilliseconds;

            if (response.Result.IsSuccessStatusCode)
            {
                if (IsHtmlDocument(response))
                {
                    string htmlPage = null;
                    try
                    {
                        var responseBody = response.Result.Content.ReadAsStringAsync();
                        htmlPage = responseBody.Result;
                    }
                    catch (System.AggregateException)
                    {
                        throw;
                    }

                    crawledLinks.Add(new WebPage(url, accessTime));
                    return htmlPage;
                }
            }
            else 
            {
                AddBadWebPagesToHashSet(response, crawledLinks, url);
            }

            return null;
        }

        private bool IsHtmlDocument(Task<HttpResponseMessage> response) 
        {
            string contentTypeString = response.Result.Content.Headers.ContentType.ToString();
            ContentType contetnType = new ContentType(contentTypeString);

            if (contetnType.MediaType == MediaTypeNames.Text.Html)
            {
                return true;
            }
            return false;
        }

        private void AddBadWebPagesToHashSet(Task<HttpResponseMessage> response, HashSet<WebPage> set, string url) 
        {
            switch (response.Result.StatusCode) 
            {
                case System.Net.HttpStatusCode.InternalServerError:

                    set.Add(new WebPage(url, -(int)response.Result.StatusCode));
                    break;
            }
            
        }

        private void ParseHtmlForLinks(string htmlString, HashSet<string> linksToCheck) 
        {
            if (!string.IsNullOrEmpty(htmlString))
            {

                Regex reHref = new Regex(@"(?inx)
                                <a \s [^>]*
                                    href \s* = \s*
                                        (?<q> ['""] )
                                            (?<url> [^""]+ )
                                        \k<q>
                                [^>]* >");

                MatchCollection matches = reHref.Matches(htmlString);

                ProcessMatchedLinks(matches, linksToCheck);
            }
        }

        private void ProcessMatchedLinks(MatchCollection matches, HashSet<string> linksToCheck) 
        {
            foreach (Match match in matches)
            {
                string pageUrl = match.Groups["url"].ToString();
                bool parsed = Uri.TryCreate(pageUrl, UriKind.RelativeOrAbsolute, out Uri pageUri);

                if (parsed)
                {
                    if (!pageUri.IsAbsoluteUri)
                    {
                        Uri.TryCreate(initialLinkPath + "/" + pageUrl.TrimStart('/'), UriKind.Absolute, out pageUri);
                    }

                    linksToCheck.Add(pageUri.GetLeftPart(UriPartial.Path).TrimEnd('/'));
                }
                else
                {
                    linksToCheck.Add(pageUrl.TrimEnd('/'));
                }

                // linksToCheck.Add(match.Groups["url"].ToString().TrimEnd('/'));
            }
        }

        private void GetAllLinksFromSitemap() 
        {
            string sitemapUrl = GetSitemapUrl();

            if (sitemapUrl == null) 
            {
                return;
            }

            bool isSiteMapIndex;
            ParseXML(sitemapUrl, out isSiteMapIndex);

            if (isSiteMapIndex)
            {
                ProcessSitemapIndices();
            }
        }

        private string GetSitemapUrl() 
        {
            if (initialLink == null)
            {
                throw new InvalidOperationException("You must call SetLink() before using this method");
            }

            string linkToRobots = initialLinkPath + @"/robots.txt";
            string sitemapFromRobots = GetTextSource(linkToRobots);

            string sitemapName = "Sitemap:";
            string sitemapUrl = null;

            if (!String.IsNullOrWhiteSpace(sitemapFromRobots)) 
            {
                int sitemapOccurance = sitemapFromRobots.IndexOf(sitemapName);

                if (sitemapOccurance != 0) 
                {
                    int startIndex = sitemapOccurance;
                    sitemapFromRobots = sitemapFromRobots.Substring(startIndex + sitemapName.Length);
                    sitemapFromRobots = sitemapFromRobots.TrimStart(' ');

                    sitemapUrl = sitemapFromRobots.Split('\n')[0].TrimEnd(' ');
                }
            }

            return sitemapUrl;

        }

        private string GetTextSource(string url)
        {
            var response = httpClient.GetAsync(url);

            if (response.Result.IsSuccessStatusCode)
            {
                var responseBody = response.Result.Content.ReadAsStringAsync();
                string text = responseBody.Result;

                return text;
            }

            return null;
        }

        private bool ParseXML(string url, out bool isSiteMapIndex)
        {
            XmlTextReader xmlReader = new XmlTextReader(url);
            isSiteMapIndex = false;

            bool adding = false;
            
            while (xmlReader.Read()) 
            {
                if (xmlReader.NodeType == XmlNodeType.Element && xmlReader.NodeType != XmlNodeType.XmlDeclaration) 
                {
                    if (xmlReader.Name == "urlset")
                    {
                        isSiteMapIndex = false;
                        break;
                    }
                    else if (xmlReader.Name == "sitemapindex")
                    {
                        isSiteMapIndex = true;
                        break;
                    }
                    else 
                    {
                        isSiteMapIndex = false;
                        return false;
                    }
                }
            }

            Uri uriLink;
            while (xmlReader.Read())
            {
                switch (xmlReader.NodeType)
                {
                    case XmlNodeType.Element:
                        if (xmlReader.Name == "loc") 
                        {
                            adding = true;
                        }
                        break;
                    case XmlNodeType.Text: 
                        if (adding)
                        {
                            Uri.TryCreate(xmlReader.Value.TrimEnd('/'), UriKind.Absolute, out uriLink);
                            sitemapLinks.Add(new WebPage(initialLinkPath + 
                                (uriLink.AbsolutePath.Length > 1 ? uriLink.AbsolutePath : "")
                                ));
                        }
                        break;
                    case XmlNodeType.EndElement: 
                        if (xmlReader.Name == "loc")
                        {
                            adding = false;
                        }
                        break;
                }
            }

            return true;
        }

        private void ProcessSitemapIndices() 
        {
            WebPage[] sitemapIndices = sitemapLinks.ToArray();
            sitemapLinks.Clear();

            foreach (WebPage sitemapIndex in sitemapIndices)
            {
                ParseXML(sitemapIndex.Url, out _);
            }
        }

        public void PrintCrawledExceptSitemap() 
        {
            if (CanPrint)
            {
                HashSet<WebPage> links = crawledLinks.Except(sitemapLinks, new WebPage.WebPageUrlComparer()).ToHashSet();
                string tableHeader = "Urls FOUNDED BY CRAWLING THE WEBSITE but not in sitemap.xml";
                PrintWebPages(links, tableHeader);
            }
            else 
            {
                throw new InvalidOperationException("This function can not be called until CrawlSiteLinks() is called");
            }

        }

        private void PrintWebPages(IEnumerable<WebPage> pages, string tableHeader)
        {
            int count = 1;

            Console.WriteLine(verticalBoundary);
            Console.WriteLine("|{0,-148}|", tableHeader);
            Console.WriteLine(verticalBoundary);

            int urlSpace = 141;

            foreach (WebPage page in pages)
            {
                string url = page.Url;
                bool isNumbered = false;

                while (url.Length > urlSpace)
                {
                    string part = url.Substring(0, urlSpace);
                    Console.WriteLine("|{0,-6} {1,-141}|", count + ")", part);
                    isNumbered = true;
                    url = url.Substring(urlSpace);
                }

                Console.WriteLine("|{0,-6} {1,-141}|", isNumbered ? "" : count + ")", url);
                Console.WriteLine(verticalBoundary);
                count++;
            }
        }

        public void PrintSitemapExceptCrawled()
        {
            if (CanPrint)
            {   
                HashSet<WebPage> links = sitemapLinks.Except(crawledLinks, new WebPage.WebPageUrlComparer()).ToHashSet();
                string tableHeader = "Urls FOUNDED IN SITEMAP.XML but not founded after crawling a web site";
                PrintWebPages(links, tableHeader);
            }
            else
            {
                throw new InvalidOperationException("This function can not be called until CrawlSiteLinks() is called");
            }

        }

        public void PrintAllLinksWithUptime()
        {
            WebPage.WebPageUrlComparer urlComparer = new WebPage.WebPageUrlComparer();
            HashSet<WebPage> unmeasuredSitemapLinks = sitemapLinks.Except(crawledLinks, urlComparer).ToHashSet();

            foreach (WebPage page in unmeasuredSitemapLinks) 
            {
                MeasureLinksUptime(page);
            }

            HashSet<WebPage> links = crawledLinks.Union(unmeasuredSitemapLinks, urlComparer).ToHashSet();
            ImmutableSortedSet<WebPage> sortedLinks = links.ToImmutableSortedSet(new WebPage.WebPageComparer());


            int count = 1;

            Console.WriteLine(verticalBoundary);
            Console.WriteLine("|{0,-120}|{1,-27}|", "Url", "Timing (ms)");
            Console.WriteLine(verticalBoundary);

            int urlSpace = 113;

            foreach (var page in sortedLinks)
            {
                string url = page.Url;
                bool isNumbered = false;

                while (url.Length > urlSpace)
                {
                    string part = url.Substring(0, urlSpace);
                    Console.WriteLine("|{0,-6} {1,-113}|{2,-27}|", count + ")", part, "");
                    isNumbered = true;
                    url = url.Substring(urlSpace);
                }

                Console.WriteLine("|{0,-6} {1,-113}|{2,-27}|", isNumbered ? "" : count + ")", url, page.AccessTime + "ms");
                Console.WriteLine(verticalBoundary);
                count++;
            }
        }

        private void MeasureLinksUptime(WebPage page)
        {
            timer.Start();
            var response = httpClient.GetAsync(page.Url);
            timer.Stop();

            long accessTime = timer.ElapsedMilliseconds;

            if (response.Result.IsSuccessStatusCode)
            {
                if (IsHtmlDocument(response))
                {
                    page.AccessTime = accessTime;
                }
            }
            else 
            {
                if (response.Result.StatusCode == System.Net.HttpStatusCode.InternalServerError)
                {
                    page.AccessTime = -(int)System.Net.HttpStatusCode.InternalServerError;
                }
                else 
                {
                    page.AccessTime = -1;
                }
            }
        }

        public void PrintStats() 
        {
            Console.WriteLine($"Urls found after crawling a website: {crawledLinks.Count}");
            Console.WriteLine($"Urls found in sitemap: {sitemapLinks.Count}");
        }

    }
}
