using System;
using System.Collections.Generic;
using System.Text;

namespace TestTask_TraineeDotNet_Denis_Bilash
{
    public class WebPage
    {
        public string Url { get; private set;}
        public long AccessTime { get; set; }

        public WebPage(string url) : this(url, 0) { } 

        public WebPage(string url, long accessTime)
        {
            Url = url;
            AccessTime = accessTime;
        }
        public override int GetHashCode()
        {
            if (Url == null) return 0;
            return Url.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as WebPage);
        }

        public bool Equals(WebPage other)
        {
            return other != null &&
                   Url == other.Url;
        }

        public class WebPageComparer : IComparer<WebPage>
        {
            public int Compare(WebPage x, WebPage y)
            {
                if (x.AccessTime > y.AccessTime)
                    return 1;

                if (x.AccessTime == y.AccessTime)
                {
                    if (x.Url == y.Url)
                        return 0;
                }
                return -1;
            }
        }

        public class WebPageUrlComparer : IEqualityComparer<WebPage>
        {
            public bool Equals(WebPage x, WebPage y)
            {
                return x.Equals(y);
            }

            public int GetHashCode(WebPage obj)
            {
                return obj.GetHashCode();
            }
        }
    }
}
