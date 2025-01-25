using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Leaf.xNet;

namespace Stalker
{
    public class Utils
    {
        public static void addHeaders(HttpRequest httpRequest, string auth, string hash)
        {
            httpRequest.AddHeader("accept-encoding", "gzip");
            httpRequest.AddHeader("Pragma", "no-cache");
            httpRequest.AddHeader("connection", "Keep-Alive");
            httpRequest.AddHeader("Sec-Fetch-Mode", "cors");
            httpRequest.AddHeader("Sec-Fetch-Site", "cross-site");
            httpRequest.AddHeader("sec-ch-ua-mobile", "?0");
            httpRequest.AddHeader("Content-type", "application/json");
            httpRequest.UserAgent = "HTTP Debugger/9.0.0.12";

            httpRequest.AddHeader("product", "llu.android");
            httpRequest.AddHeader("version", "4.12.0");
            httpRequest.AddHeader("Cache-Control", "no-cache");
            httpRequest.AddHeader("Accept-Encoding", "gzip");
            if (auth != null)
                httpRequest.AddHeader("Authorization", $"Bearer {auth}");
            if (hash != null)
                httpRequest.AddHeader("Account-Id", hash);
        }
    }
}
