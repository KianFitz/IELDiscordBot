using OpenQA.Selenium.Chrome;
using System;
using System.Collections.Generic;

namespace WebAutomation
{
    public static class Selenium
    {
        public static string GetTRNData(string url)
        {
            var chromeOptions = new ChromeOptions();
            chromeOptions.AddArgument("headless");
            chromeOptions.AddArgument("user-agent=Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/84.0.4147.125 Safari/537.36");

            using (var browser = new ChromeDriver(chromeOptions))
            {
                browser.Navigate().GoToUrl(url);
                return browser.PageSource;
            }
        }
    }
}
