namespace BananoMonkeyMatchEmulator
{
    using System;
    using System.Diagnostics;
    using Microsoft.Extensions.Logging;

    public class Humanizator
    {
        private static readonly Random Rand = new Random();

        private readonly ILogger logger;

        private readonly Stopwatch playTime = Stopwatch.StartNew();

        private bool started;

        private bool human;

        private TimeSpan toPlay;

        private Humanizator(ILogger<Humanizator> logger)
        {
            this.logger = logger;
        }

        public void Start(bool asHuman)
        {
            started = true;
            human = asHuman;

            if (human)
            {
                playTime.Restart();
                toPlay = TimeSpan.FromMinutes(Rand.Next(60, 120));
            }
        }

        public void WhatNext(int currentScore, out TimeSpan delay, out bool fail)
        {
            if (!started)
            {
                throw new InvalidOperationException("Not started. Call Start(...)");
            }

            if (!human)
            {
                delay = TimeSpan.Zero;
                fail = false;
                return;
            }

            fail = Rand.Next(0, 1000) < 20; // 2%

            if (playTime.Elapsed > toPlay || currentScore % 100 == 0)
            {
                delay = TimeSpan.FromMinutes(Rand.Next(10, 30));
                toPlay = TimeSpan.FromMinutes(Rand.Next(60, 120)) + delay;
                playTime.Restart();
                logger.LogInformation("Playing too long, will rest for " + delay);
            }
            else
            {
                delay = TimeSpan.FromMilliseconds(1000 + Rand.Next(500, 3000));
            }
        }
    }
}
