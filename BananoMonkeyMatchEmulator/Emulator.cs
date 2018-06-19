namespace BananoMonkeyMatchEmulator
{
    using System;
    using System.Diagnostics;
    using System.Drawing;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class Emulator
    {
        private const string Server = "https://bananosecure.coranos.io/";

        private static readonly Random Rand = new Random();

        private static HttpClient secondaryHttpClient;

        private readonly Humanizator humanizator;

        private readonly ILogger logger;

        private HttpClient mainHttpClient;

        private int nextAnswer = 0;

        private int exceptionsCount = 0;

        public Emulator(ILogger<Emulator> logger, Humanizator humanizator)
        {
            this.logger = logger;
            this.humanizator = humanizator;
        }

        public string Wallet { get; private set; }

        public string Discord { get; private set; }

        public async Task RunAsync(string wallet, string discord, bool asHuman)
        {
            logger.LogInformation($"Starting for {wallet} of {discord} (human: {asHuman})");

            Wallet = wallet;
            Discord = discord;

            var handler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            var uri = new Uri(Server);

            mainHttpClient = new HttpClient(handler) { BaseAddress = uri };
            mainHttpClient.DefaultRequestHeaders.Accept.TryParseAdd("application/json, text/javascript, */*; q=0.01");
            mainHttpClient.DefaultRequestHeaders.AcceptEncoding.TryParseAdd("gzip, deflate, br");
            mainHttpClient.DefaultRequestHeaders.AcceptLanguage.TryParseAdd("ru,ru-RU;q=0.8,en;q=0.5,en-US;q=0.3");
            mainHttpClient.DefaultRequestHeaders.CacheControl = CacheControlHeaderValue.Parse("no-cache");
            mainHttpClient.DefaultRequestHeaders.Connection.TryParseAdd("close");
            mainHttpClient.DefaultRequestHeaders.Add("DNT", "1");
            mainHttpClient.DefaultRequestHeaders.Host = uri.Host;
            mainHttpClient.DefaultRequestHeaders.Pragma.TryParseAdd("no-cache");
            mainHttpClient.DefaultRequestHeaders.Referrer = uri;
            mainHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:60.0) Gecko/20100101 Firefox/60.0");
            mainHttpClient.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");

            if (secondaryHttpClient == null)
            {
                var handler2 = new HttpClientHandler()
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                    MaxConnectionsPerServer = 10,
                };

                secondaryHttpClient = new HttpClient(handler2);
                secondaryHttpClient.DefaultRequestHeaders.AcceptEncoding.TryParseAdd("gzip, deflate, br");
            }

            humanizator.Start(asHuman);
            while (true)
            {
                try
                {
                    await PlayAsync();
                    break;
                }
                catch (HttpRequestException ex)
                {
                    exceptionsCount++;
                    if (exceptionsCount > 10)
                    {
                        throw;
                    }
                    else if (exceptionsCount < 5)
                    {
                        logger.LogWarning("Exception: " + ex.Message);
                        await Task.Delay(TimeSpan.FromSeconds(exceptionsCount));
                    }
                    else
                    {
                        logger.LogWarning("Exception: " + ex.Message);
                        await Task.Delay(TimeSpan.FromSeconds(30));
                    }
                }
            }

            logger.LogInformation("Completed.");
        }

        public async Task PlayAsync()
        {
            var wins = -999;
            var oopsCount = 0;

            var roundTimer = Stopwatch.StartNew();
            var failNextRound = false;

            while (true)
            {
                if (failNextRound)
                {
                    nextAnswer = Rand.Next(0, 15);
                    logger.LogWarning("Will (try to) fail this round (thanks Humanizator)");
                }

                var newTask = await SendChoiceAsync(nextAnswer);
                if (newTask == null)
                {
                    break;
                }

                exceptionsCount = 0;
                roundTimer.Restart();

                if (newTask.wins <= wins)
                {
                    logger.LogError("WINs not increased. Something wrong...");
                    oopsCount++;
                    if (oopsCount > 3)
                    {
                        logger.LogCritical("STOPPING.");
                        break;
                    }
                }
                else
                {
                    oopsCount = 0;
                }

                wins = newTask.wins;
                var bans = newTask.wins - newTask.losses;
                logger.LogInformation($"{newTask.wins} wins, {newTask.losses} loses, {bans} bananos. Winners: {newTask.totalWinners}. Payout {newTask.totalPayout} of {newTask.maxPayout}");

                if (bans > newTask.winnerThreshold)
                {
                    logger.LogInformation("WE WON! Stopping!");
                    break;
                }

                var (l, r) = GetImagesForGuess(newTask);
                var matchL = await FindMatchAsync(newTask.prefix, l.First(), l.Skip(1).ToArray());
                var matchR = await FindMatchAsync(newTask.prefix, r.First(), r.Skip(1).ToArray());
                nextAnswer = matchL + (4 * matchR);

                humanizator.WhatNext(newTask.wins, out var delay, out failNextRound);
                var actualDelay = delay - roundTimer.Elapsed;
                if (actualDelay > TimeSpan.Zero)
                {
                    await Task.Delay(actualDelay);
                }
            }
        }

        public async Task<ServerResponse> SendChoiceAsync(int answer)
        {
            var url = $"/game.json?account={Wallet}&choice={answer}&discord={Discord}&bot=maybe".Split('#')[0];

            logger.LogDebug("Sending: " + url);

            using (var req = new HttpRequestMessage(HttpMethod.Get, url))
            {
                using (var resp = await mainHttpClient.SendAsync(req))
                {
                    resp.EnsureSuccessStatusCode();
                    var respText = await resp.Content.ReadAsStringAsync();
                    logger.LogDebug(respText);

                    var response = JsonConvert.DeserializeObject<ServerResponse>(respText);

                    if (response.slowDownFlag)
                    {
                        logger.LogWarning("SlowDown FLAG is set. Waiting for 5 sec...");
                        await Task.Delay(5000);
                    }

                    if (response.accountIsInvalidFlag)
                    {
                        logger.LogCritical("Account is Invalid. Stopping.");
                        return null;
                    }

                    return response;
                }
            }
        }

        public (string[] l, string[] r) GetImagesForGuess(ServerResponse response)
        {
#pragma warning disable SA1013
            var l = new[]
                {
                    response.expected.L,
                    response.choices[new[] { 0, 4, 8, 12 }[Rand.Next(0, 3)]].L,
                    response.choices[new[] { 1, 5, 9, 13 }[Rand.Next(0, 3)]].L,
                    response.choices[new[] { 2, 6, 10, 14 }[Rand.Next(0, 3)]].L,
                    response.choices[new[] { 3, 7, 11, 15 }[Rand.Next(0, 3)]].L,
                };
            var r = new[]
                {
                    response.expected.R,
                    response.choices[new[] { 0, 1, 2, 3 }[Rand.Next(0, 3)]].R,
                    response.choices[new[] { 4, 5, 6, 7 }[Rand.Next(0, 3)]].R,
                    response.choices[new[] { 8, 9, 10, 11 }[Rand.Next(0, 3)]].R,
                    response.choices[new[] { 12, 13, 14, 15 }[Rand.Next(0, 3)]].R,
                };
            return (l, r);
#pragma warning restore SA1013
        }

        public async Task<int> FindMatchAsync(string prefix, string expected, string[] candidates)
        {
            var url = prefix + expected;
            using (var img = Image.FromStream(await secondaryHttpClient.GetStreamAsync(prefix + expected)))
            {
                for (var i = 0; i < candidates.Length; i++)
                {
                    using (var cand = Image.FromStream(await secondaryHttpClient.GetStreamAsync(prefix + candidates[i])))
                    {
                        var match = ImageComparer.AreEqual(img, cand, out var similarity);
                        logger.LogDebug($"Compare {expected} with {candidates[i]}: {similarity}{(match ? " MATCH" : string.Empty)}");
                        if (match)
                        {
                            return i;
                        }
                    }
                }
            }

            return -1;
        }

#pragma warning disable SA1300
        public class ServerResponse
        {
            public int wins { get; set; }

            public string serverVersion { get; set; }

            public string prefix { get; set; }

            public LR expected { get; set; }

            public int totalPayout { get; set; }

            public int losses { get; set; }

            public int bytes_used { get; set; }

            public int winnerThreshold { get; set; }

            public int totalWinners { get; set; }

            public bool accountIsInvalidFlag { get; set; }

            public int time { get; set; }

            public bool slowDownFlag { get; set; }

            public int maxWinners { get; set; }

            public LR[] choices { get; set; }

            public int maxPayout { get; set; }
        }

        public class LR
        {
            public string R { get; set; }

            public string L { get; set; }
        }
#pragma warning restore SA1300
    }
}
