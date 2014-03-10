using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using log4net;
using Timer = System.Timers.Timer;

namespace BetterProcess
{
    public class CommandEventArgs : EventArgs
    {
        public string Message { get; set; }
    }

    public class Command
    {
        private static ILog log = LogManager.GetLogger(typeof(Command));
        private const int LONG_TIMEOUT = 10800000 /* 3 hours! */;
        private string fileName;
        private string args;
        private int pid;
        private CancellationToken cancellationToken;
        public bool IsRunning { get; private set; }

        public event EventHandler<CommandEventArgs> StatusReport = delegate { };

        private Command() { throw new ArgumentOutOfRangeException("Please use paramaterized constructors"); }

        public Command(string fileName, string args, int timeout = LONG_TIMEOUT, CancellationToken? cancellationToken = null)
        {
            this.IsRunning = false;
            this.fileName = fileName;
            this.args = args;
            this.cancellationToken = cancellationToken ?? CancellationToken.None;
        }

        public List<string> Run()
        {
            List<string> output = null;
            Process process = new Process();
            try
            {
                output = Strategy2(this.fileName, this.args, process, this.cancellationToken);
            }
            finally
            {
                process.Dispose();
                this.IsRunning = false;
                this.pid = Int32.MaxValue;
            }
            return output;
        }

        //TODO: is there an issue if the command doesn't output anything (or is this a pipe problem?)
        private List<string> Strategy2(string fileName, string args, Process process, CancellationToken cancellationToken)
        {
            var stdout = new List<string>();
            var stderr = new List<string>();
            long processLastActiveMs = 0;
            bool success = false;
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            //why do it this way? see 
            //http://stackoverflow.com/questions/139593/processstartinfo-hanging-on-waitforexit-why
            //NOTE: This code hangs when stepped through in SharpDevelop

            Timer heartBeat = new Timer(120000/*2 minutes*/);
            heartBeat.Elapsed += delegate
            {
                // prefer stderr messages (some exes only return status messages on stderr)
                string status = stderr.Count > 0 ? stderr.Last() : stdout.DefaultIfEmpty("(none)").LastOrDefault();

                string message = "I own PID " + this.pid + " and I'm not dead yet! " +
                            "My process last reported " + status +
                             "(" + fileName + " " + args + ")";

                StatusReport(this, new CommandEventArgs { Message = message });

                if (stopWatch.ElapsedMilliseconds - processLastActiveMs > 300000/*5 minutes*/)
                {
                    log.Error("Background heartbeat observed that process pid=" + this.pid + " hasn't " +
                        " returned anything in 5 minutes. Shutting down zombie process");
                    heartBeat.Stop();

                    new Command("taskkill", " /F /PID " + this.pid).Run();
                    
                    process.Dispose();
                    this.IsRunning = false;
                }

            };
            heartBeat.Start();

            process.StartInfo.FileName = fileName;
            process.StartInfo.Arguments = args;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            //for plink.exe, see http://social.msdn.microsoft.com/Forums/vstudio/en-US/697324fa-6ce6-4324-971a-61f6eec075be/redirecting-output-from-plink
            process.StartInfo.RedirectStandardInput = true; //redirecting standard input b/c plink.exe requires it
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            using (AutoResetEvent stdOutResetEvent = new AutoResetEvent(false))
            using (AutoResetEvent stdErrResetEvent = new AutoResetEvent(false))
            {
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data == null || cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            stdOutResetEvent.Set();
                        }
                        catch (ObjectDisposedException) { }
                    }
                    else if (!String.IsNullOrEmpty(e.Data))
                    {
                        processLastActiveMs = stopWatch.ElapsedMilliseconds;
                        stdout.Add(e.Data);
                 
                        StatusReport(this, new CommandEventArgs { Message = e.Data });
                    }
                };
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data == null || cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            stdErrResetEvent.Set();
                        }
                        catch (ObjectDisposedException) { }
                    }
                    else if (!String.IsNullOrEmpty(e.Data))
                    {
                        processLastActiveMs = stopWatch.ElapsedMilliseconds;
                        stderr.Add(e.Data);

                        StatusReport(this, new CommandEventArgs { Message = e.Data });
                    }
                };

                //-- Here we go ...
                process.Start();
                this.IsRunning = true;
                this.pid = process.Id;

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                success = process.WaitForExit(LONG_TIMEOUT) &&
                               stdErrResetEvent.WaitOne(LONG_TIMEOUT) &&
                               stdOutResetEvent.WaitOne(LONG_TIMEOUT);
            }
            heartBeat.Stop();

            if (!success)
            {
                string message = "Timed or caught out while executing command " +
                                fileName + " " + args + ". Agressively killing the process by PID=" + process.Id;

                new Command("taskkill", "/F /PID " + process.Id).Run();

                throw new TimeoutException(message);
            }
            if (cancellationToken.IsCancellationRequested)
            {
                string message = "ABORTING process at cancellation request. Process was:" +
                                fileName + " " + args + ". Agressively killing the process by PID=" + process.Id;

                new Command("taskkill", "/F /PID " + process.Id).Run();

                throw new ThreadInterruptedException(message);
            }

            stdout.AddRange(stderr);
            return stdout;
        }
    }
}
