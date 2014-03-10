DotNet Better Process
=====================

This code addresses headaches, heartaches, and long nights of lost sleep related to .Net's 
System.Diagnostics.Process. 

It's not really poor Process's fault: we can't expect it to know
some .exe out there is going to return 2 gigs of messages. It can't possibly anticipate that
some random .exe may or may not turn into a zombie 2 hours into running. 

But this code wraps it up a little to save everyone some trouble.

This code is for you if:

* You're having trouble with Process.Start hanging on WaitForExit().
* You have a long running process and you'd like to hook up an event handler and get status updates
* You're invoking a process that runs a long time. But if it runs too long, you'd like to kill it
* You're having trouble with a process that returns a byte-ton of data causing a buffer overflow
* You're starting a lot of .exe's in a Parallel loop and need a graceful way to shut them down with
a cancellationToken

This is a first commit of the code so it's still rough around the edges. More updates coming soon.

