using System;
using System.IO;
using System.Threading;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BetterProcess;

namespace Cmod.Migration.Tests
{
    [TestClass]
    public class ProcessRunTests
    {
        [TestMethod]
        public void Test_Most_Basic_Scenario_Works()
        {
            var output = new Command("cmd", "/c echo hi").Run();
            Assert.IsTrue(string.Join(" ", output).Contains("hi"));
        }

        [TestMethod]
        public void Test_No_Ouput_Process_Works()
        {
            string file = "test.txt";
            Command command = new Command("cmd", "/c echo hi > " + file);
            var output = command.Run();

            Assert.IsTrue(output.Count == 0);

            string got = File.ReadAllText(file).Trim();
            Assert.AreEqual("hi", got);

            File.Delete(file);
        }

        [TestMethod]
        public void Test_We_Will_Shut_Down_After_5_Minutes_No_Activity()
        {
            Debug.WriteLine("Kicking off zombie process to run for 6 hours");

            Command command = new Command("LongRunningCommandSimulation.exe", "360 zombie");

            Task.Factory.StartNew(() =>
            {
                command.Run();
            });

            Thread.Sleep(60000/*1min*/);
            Assert.IsTrue(command.IsRunning);

            Thread.Sleep(300000/*5mins*/);
            Assert.IsFalse(command.IsRunning);

            Assert.IsTrue(Process.GetProcessesByName("LongRunningCommandSimulation").Count() == 0);
        }

        [TestMethod]
        public void TEST_PROCESS_CAN_RUN_AN_HOUR()
        {
            Debug.WriteLine("Kicking off a healthy process to run for 60 minutes");
            List<string> output = null;

            Task task = Task.Factory.StartNew(() =>
            {
                var command = new Command("LongRunningCommandSimulation.exe", "60");
                command.StatusReport += (object sender, CommandEventArgs e) =>
                {
                    Debug.WriteLine("Received:" + e.Message);
                };

                output = command.Run();

            });

            task.Wait();

            Assert.IsTrue(output.Count > 10);

            Assert.IsTrue(Process.GetProcessesByName("LongRunningCommandSimulation").Count() == 0);
        }

        [TestMethod]
        public void TEST_WE_CAN_MANUALLY_CANCEL_A_PROCESS()
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            CountdownEvent countdownEvent = new CountdownEvent(1);

            Task.Factory.StartNew(() =>
            {
                var command = new Command("LongRunningCommandSimulation.exe", "360", cancellationToken:cancellationTokenSource.Token);
                command.StatusReport += (object sender, CommandEventArgs e) =>
                {
                    Debug.WriteLine("Received:" + e.Message);
                };

                try
                {
                    List<string> output = command.Run();
                }
                catch (TimeoutException ex)
                {
                    countdownEvent.Signal();
                }
            });

            Thread.Sleep(300000);//let it run for a few minutes then send cancel 
            cancellationTokenSource.Cancel();

            countdownEvent.Wait();

            Assert.IsTrue(Process.GetProcessesByName("LongRunningCommandSimulation").Count() == 0);
        }
    }
}
