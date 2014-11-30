using System;
using System.Net;

namespace VBCBBot
{
    public class CookieWebClient : WebClient
    {
        public CookieContainer CookieJar;

        public CookieWebClient()
            : base()
        {
            CookieJar = new CookieContainer();
        }

        public void ClearCookieJar()
        {
            CookieJar = new CookieContainer();
        }

        protected override WebRequest GetWebRequest(Uri address)
        {
            var req = base.GetWebRequest(address);
            if (req is HttpWebRequest)
            {
                ((HttpWebRequest)req).CookieContainer = CookieJar;
            }
            return req;
        }
    }
}
