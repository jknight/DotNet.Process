using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;
using Timer = System.Timers.Timer;

namespace BetterProcessTests
{

    //This is a helper exe to simulate long running commands. 
    //It builds in the projects's build tasks.

    public class LongRunningCommandSimulation
    {
        public static void Main(string[] args)
        {
            //zombie mode will return nothing, just hang. normal mode returns messages every 30 seconds
            if (args.Length > 0)
            {
                Console.WriteLine("Usage: LongRunningCommandSimulation.exe [runtime mins] [zombie]");
            }
            int runtimeMinutes = Int32.Parse(args[0]);
            bool actDead = false;
            if (args.Length >= 2)
            {
                actDead = args[1].ToUpper().Contains("ZOMBIE");
            }

            if (!actDead)
            {
                Timer timer = new Timer(3000/*30seconds*/);
                timer.Elapsed += delegate
                {
                    Console.WriteLine(DateTime.Now.ToString() + "stdout>> Still running ... ");
                    Console.Error.WriteLine(DateTime.Now.ToString() + "stderr>> Here's an error ...");
                };
                timer.Enabled = true;
                timer.Start();
            }

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            while (stopwatch.Elapsed.Minutes < runtimeMinutes)
            {
                Thread.Sleep(60000/*1 min*/);
            }
        }
    }
}
