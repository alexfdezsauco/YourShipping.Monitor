// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CookiesHelper.cs" company="WildGums">
//   Copyright (c) 2008 - 2020 WildGums. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace YourShipping.Monitor.Server.Services
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text.Encodings.Web;
    using System.Text.RegularExpressions;

    using Serilog;

    internal static class CookiesHelper
    {
        public static CookieCollection GetCollectiton()
        {
            var collection = new CookieCollection();
            if (File.Exists("cookies.txt"))
            {
                var regex = new Regex(
                    @"([^]\s]+)\s+([^]\s]+)\s+([^]\s]+)\s+([^]\s]+)\s+([^]\s]+)\s+([^]\s]+)\s+([^]\s]+)",
                    RegexOptions.IgnoreCase | RegexOptions.Multiline);
                var readAllText = File.ReadAllLines("cookies.txt").Where(s => !s.TrimStart().StartsWith("#"));
                foreach (var line in readAllText)
                {
                    var match = regex.Match(line);
                    if (match.Success)
                    {
                        try
                        {
                            var name = match.Groups[6].Value;
                            var value = match.Groups[7].Value;

                            if (name == "myCookie")
                            {
                                value = "username=&userPsw=";
                            }

                            collection.Add(
                                new Cookie(
                                    name,
                                    value,
                                    match.Groups[3].Value,
                                    match.Groups[1].Value));
                        }
                        catch (Exception e)
                        {
                            Log.Warning(e.Message);
                        }
                    }
                }
            }

            return collection;
        }
    }
}