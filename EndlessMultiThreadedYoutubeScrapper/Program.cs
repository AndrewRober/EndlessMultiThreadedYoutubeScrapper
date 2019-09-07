using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;

namespace EndlessMultiThreadedYoutubeScrapper
{
    class Program
    {
        static ConcurrentBag<string> HarvestedEmails = new ConcurrentBag<string>(), InitialRandomSeeds, PagesScrapped = new ConcurrentBag<string>();
        static List<BackgroundWorker> workers = new List<BackgroundWorker>();
        static object locker = new object();
        static Uri baseUri = new Uri("https://www.youtube.com"), baseUriWithQueryParam = new Uri("https://www.youtube.com/watch?v=");
        static Stopwatch sw;

        static void Main(string[] args)
        {
            sw = new Stopwatch();
            sw.Start();
            using (var client = new WebClient())
            {
                InitialRandomSeeds = new ConcurrentBag<string>(Regex.Matches(client.DownloadString(baseUri), "watch\\?v=[\\w -]+",
                    RegexOptions.Compiled).Select(m => m.Value).Distinct().Select(releativeUrl => new Uri(baseUri: baseUri, relativeUri: releativeUrl).AbsoluteUri).Take(20));
                for (int i = 0; i < InitialRandomSeeds.Count; i++)
                {
                    var closure = i;
                    var bgw = new BackgroundWorker();
                    bgw.DoWork += (s,e) =>
                    {
                        if (!InitialRandomSeeds.TryTake(out var initialSeed)) UpdateConsoleLine($"[Thread {closure + 1}] Could not find initial seeds? yt updated their structure?", closure + 1);
                        try
                        {
                            var nextVideoURL = initialSeed;
                            do
                            {
                                using (var wb = new WebClient())
                                {
                                    PagesScrapped.Add(nextVideoURL);
                                    var pageHtml = wb.DownloadString(new Uri(nextVideoURL));
                                    var emails = Regex.Matches(pageHtml, @"[A-Za-z0-9_\-\+]+@[A-Za-z0-9\-]+\.([A-Za-z]{2,3})(?:\.[a-z]{2})?", RegexOptions.Compiled);
                                    if (emails.Any()) emails.ToList().ForEach(email =>
                                    {
                                        if (!HarvestedEmails.Contains(email.Value))
                                        {
                                            HarvestedEmails.Add(email.Value);
                                            UpdateScrappedEmailsOnConsole();
                                        }
                                    });
                                    var nextVideoId = Regex.Match(pageHtml.Replace("\n", string.Empty), "play next.*?<a href=.*?=(.*?)\"").Groups[1].Value;
                                    nextVideoURL = $"https://www.youtube.com/watch?v={nextVideoId}";
                                    if (PagesScrapped.Contains(nextVideoURL))
                                        UpdateConsoleLine($"[Thread {closure + 1}] Scrapping {nextVideoId} [Scrapped before by another thread]", closure + 1);
                                    else
                                        UpdateConsoleLine($"[Thread {closure + 1}] Scrapping {nextVideoId} ...", closure + 1);
                                }
                            } while (!string.IsNullOrEmpty(nextVideoURL));
                            UpdateConsoleLine($"[Thread {closure + 1}] has stopped working: Could not find the next video!", closure + 1);
                        }
                        catch (Exception ex)
                        {
                            UpdateConsoleLine($"[Thread {closure + 1}] has stopped working: {ex.Message}", closure + 1);
                        }
                    };
                    workers.Add(bgw);
                    bgw.RunWorkerAsync();
                }
            }
            while (true)
                Thread.Sleep(0);
        }
        
        static void UpdateConsoleLine(string line, int lineNum)
        {
            lock (locker)
            {
                TimeSpan t = TimeSpan.FromMilliseconds(sw.ElapsedMilliseconds);
                var timer = string.Format("{0:D2}h:{1:D2}m:{2:D2}s:{3:D3}ms",
                        t.Hours,
                        t.Minutes,
                        t.Seconds,
                        t.Milliseconds);
                Console.Title = $"Harvested {HarvestedEmails.Count} emails so far, scrapped pages so far {PagesScrapped.Count}, been running for {timer}";
                Console.SetCursorPosition(0, lineNum - 1);
                Console.Write(new string(' ', Console.BufferWidth));
                Console.SetCursorPosition(0, lineNum - 1);
                Console.Write(line.Length > Console.BufferWidth ? line.Substring(0, Console.BufferWidth) : line);
            }
        }

        static void UpdateScrappedEmailsOnConsole()
        {
            lock (locker)
            {
                Console.SetCursorPosition(0, 22);
                Console.WriteLine(new string('*', Console.WindowWidth));
                for (int i = 0; i < HarvestedEmails.Count && i < 10; i++)
                {
                    var oldpos = Console.CursorTop;
                    Console.Write(new string(' ', Console.BufferWidth));
                    Console.SetCursorPosition(0, oldpos);
                    Console.WriteLine(HarvestedEmails.ElementAt(i));
                }
            }
        }
    }
}
