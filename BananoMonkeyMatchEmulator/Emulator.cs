namespace BananoMonkeyMatchEmulator
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Drawing;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Newtonsoft.Json;

    public class Emulator : IEmulator
    {
        private readonly Random rand = new Random();

        private readonly ILogger logger;

        private readonly HttpClient httpClient;

        private readonly HttpClient httpClient2;

        private readonly EmulatorOptions options;

        private readonly HashSet<string> knownImages = new HashSet<string>();

        private int nextAnswer = 0;

        public Emulator(IOptions<EmulatorOptions> options, ILogger<Emulator> logger)
        {
            this.options = options.Value;
            this.logger = logger;

            var uri = new Uri(options.Value.Website);

            var handler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            this.httpClient = new HttpClient(handler) { BaseAddress = uri };
            this.httpClient.DefaultRequestHeaders.Accept.TryParseAdd("application/json, text/javascript, */*; q=0.01");
            this.httpClient.DefaultRequestHeaders.AcceptEncoding.TryParseAdd("gzip, deflate, br");
            this.httpClient.DefaultRequestHeaders.AcceptLanguage.TryParseAdd("ru,ru-RU;q=0.8,en;q=0.5,en-US;q=0.3");
            this.httpClient.DefaultRequestHeaders.CacheControl = CacheControlHeaderValue.Parse("no-cache");
            ////this.httpClient.DefaultRequestHeaders.Connection.TryParseAdd("keep-alive");
            this.httpClient.DefaultRequestHeaders.Add("DNT", "1");
            this.httpClient.DefaultRequestHeaders.Host = uri.Host;
            this.httpClient.DefaultRequestHeaders.Pragma.TryParseAdd("no-cache");
            this.httpClient.DefaultRequestHeaders.Referrer = uri;
            this.httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:60.0) Gecko/20100101 Firefox/60.0");
            this.httpClient.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");

            var handler2 = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            this.httpClient2 = new HttpClient(handler2);
            this.httpClient2.DefaultRequestHeaders.AcceptEncoding.TryParseAdd("gzip, deflate, br");
        }

        public int ExceptionCount { get; set; }

        public async Task RunAsync()
        {
            logger.LogInformation("Starting...");

            var wins = -999;
            var oopsCount = 0;
            var roundTimer = Stopwatch.StartNew();
            var playTimer = Stopwatch.StartNew();
            var toPlay = TimeSpan.FromMinutes(rand.Next(60, 120));

            while (true)
            {
                if (playTimer.Elapsed > toPlay)
                {
                    var rest = TimeSpan.FromMinutes(rand.Next(10, 30));
                    logger.LogInformation("Playing too long. Let's have a rest for " + rest);
                    await Task.Delay(rest);
                    toPlay = TimeSpan.FromMinutes(rand.Next(60, 120));
                    logger.LogInformation("Next rest after " + toPlay);
                    playTimer.Restart();
                }

                var newTask = await SendChoiceAsync(nextAnswer);
                if (newTask == null)
                {
                    break;
                }

                this.ExceptionCount = 0;
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

                var minimumDelay = TimeSpan.FromMilliseconds(1000 + rand.Next(500, 3000));
                var actualDelay = minimumDelay - roundTimer.Elapsed;
                if (actualDelay > TimeSpan.Zero)
                {
                    await Task.Delay(actualDelay);
                }
            }
        }

        public async Task<ServerResponse> SendChoiceAsync(int answer)
        {
            var url = $"/game.json?account={options.Wallet}&choice={answer}&discord={options.Discord}&bot=maybe".Split('#')[0];

            logger.LogDebug("Sending: " + url);

            using (var req = new HttpRequestMessage(HttpMethod.Get, url))
            {
                using (var resp = await httpClient.SendAsync(req))
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
                    response.choices[new[] { 0, 4, 8, 12 }[rand.Next(0, 3)]].L,
                    response.choices[new[] { 1, 5, 9, 13 }[rand.Next(0, 3)]].L,
                    response.choices[new[] { 2, 6, 10, 14 }[rand.Next(0, 3)]].L,
                    response.choices[new[] { 3, 7, 11, 15 }[rand.Next(0, 3)]].L,
                };
            var r = new[]
                {
                    response.expected.R,
                    response.choices[new[] { 0, 1, 2, 3 }[rand.Next(0, 3)]].R,
                    response.choices[new[] { 4, 5, 6, 7 }[rand.Next(0, 3)]].R,
                    response.choices[new[] { 8, 9, 10, 11 }[rand.Next(0, 3)]].R,
                    response.choices[new[] { 12, 13, 14, 15 }[rand.Next(0, 3)]].R,
                };
            return (l, r);
#pragma warning restore SA1013
        }

        public async Task<int> FindMatchAsync(string prefix, string expected, string[] candidates)
        {
            var url = prefix + expected;
            using (var img = Image.FromStream(await httpClient2.GetStreamAsync(prefix + expected)))
            {
                for (var i = 0; i < candidates.Length; i++)
                {
                    using (var cand = Image.FromStream(await httpClient2.GetStreamAsync(prefix + candidates[i])))
                    {
                        var ratio = CompareImages(img, cand);
                        var match = ratio > 0.95;
                        logger.LogDebug($"Compare {expected} with {candidates[i]}: {ratio}{(match ? " MATCH" : string.Empty)}");
                        if (match)
                        {
                            return i;
                        }
                    }
                }
            }

            return -1;
        }

        public float CompareImages(Image firstImage, Image secondImage)
        {
            float equal = 0;
            float diff = 0;
            using (var firstBitmap = new Bitmap(firstImage))
            {
                using (var secondBitmap = new Bitmap(secondImage))
                {
                    for (var i = 0; i < firstBitmap.Width; i++)
                    {
                        for (var j = 0; j < firstBitmap.Height; j++)
                        {
                            var eq = firstBitmap.GetPixel(i, j) == secondBitmap.GetPixel(i, j);
                            if (eq)
                            {
                                equal++;
                            }
                            else
                            {
                                diff++;
                            }
                        }
                    }
                }
            }

            return equal / (diff + equal);
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
