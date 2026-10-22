using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;

namespace QingMonitor
{
    // FPS is the application's submission rate, measured from real Present events.
    // This deliberately disables GPU, display and input timing to keep ETW work small.
    public sealed class FpsSource : IDisposable
    {
        private const int MaximumSwapChains = 128;
        private const int BucketCount = 16;
        private const double BucketSeconds = 0.25;
        private const double WindowSeconds = 2.0;
        private const double StaleSeconds = 5.0;
        private readonly string executable;
        private readonly object gate = new object();
        private readonly object lifecycle = new object();
        private readonly Dictionary<ChainKey, ChainState> chains = new Dictionary<ChainKey, ChainState>();
        private readonly int[] fieldStart = new int[64];
        private readonly int[] fieldLength = new int[64];
        private Process capture;
        private string sessionName;
        private string errorText = "";
        private string status = "FPS 已关闭";
        private bool running;
        private bool disposed;
        private int childPid;
        private long receivedFrames;
        private int headerFields;
        private int pidColumn = -1;
        private int chainColumn = -1;
        private int timeColumn = -1;
        private double timeScale = 1.0;

        public FpsSource(string baseDirectory)
        {
            if (baseDirectory == null) throw new ArgumentNullException("baseDirectory");
            executable = Path.Combine(baseDirectory, "PresentMon.exe");
        }

        public bool Running { get { lock (gate) return running; } }
        public string Status { get { lock (gate) return status; } }
        public int ChildPid { get { lock (gate) return childPid; } }
        public long ReceivedFrames { get { lock (gate) return receivedFrames; } }

        public void Start()
        {
            lock (lifecycle)
            {
                lock (gate)
                {
                    if (disposed) return;
                    if (running) return;
                }
                StopCore();
                if (!File.Exists(executable))
                {
                    lock (gate) status = "缺少 PresentMon.exe，无法采集 FPS";
                    return;
                }
                string ownSession = "QingMonitor-" + Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture)
                    + "-" + Guid.NewGuid().ToString("N");
                Process child = new Process();
                child.StartInfo = CreateInfo("--output_stdout --v1_metrics --no_track_display --no_track_gpu"
                    + " --no_track_input --no_console_stats --session_name " + ownSession);
                child.EnableRaisingEvents = true;
                child.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data == null) return;
                    lock (gate)
                    {
                        if (capture == child && running) AcceptLine(e.Data);
                    }
                };
                child.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data == null) return;
                    lock (gate)
                    {
                        if (capture != child) return;
                        if (errorText.Length < 2048) errorText += e.Data + " ";
                        if (e.Data.IndexOf("access denied", StringComparison.OrdinalIgnoreCase) >= 0)
                            status = PermissionMessage();
                    }
                };
                child.Exited += delegate { CaptureExited(child, ownSession); };
                lock (gate)
                {
                    chains.Clear();
                    pidColumn = chainColumn = timeColumn = -1;
                    headerFields = 0;
                    receivedFrames = 0;
                    errorText = "";
                    status = "FPS 已启用，等待应用帧";
                    sessionName = ownSession;
                    capture = child;
                    running = true;
                }
                try
                {
                    child.Start();
                    lock (gate) if (capture == child) childPid = child.Id;
                    child.BeginOutputReadLine();
                    child.BeginErrorReadLine();
                }
                catch (Exception ex)
                {
                    lock (gate)
                    {
                        capture = null;
                        running = false;
                        childPid = 0;
                        status = "FPS 启动失败：" + ex.Message;
                    }
                    CloseOwnSession(ownSession);
                    FinishProcess(child);
                    lock (gate) sessionName = null;
                }
            }
        }

        public void Stop()
        {
            lock (lifecycle) StopCore();
        }

        private void StopCore()
        {
            Process child;
            string ownSession;
            lock (gate)
            {
                child = capture;
                ownSession = sessionName;
                capture = null;
                sessionName = null;
                running = false;
                childPid = 0;
                status = "FPS 已关闭";
                chains.Clear();
            }
            // PresentMon's stop command uses ControlTrace on exactly this GUID session.
            // Wait for the consumer to exit gracefully, then only kill our own child if needed.
            CloseOwnSession(ownSession);
            FinishProcess(child);
        }

        private void CaptureExited(Process child, string ownSession)
        {
            lock (lifecycle)
            {
                lock (gate) if (capture != child) return;
                try { child.WaitForExit(); } catch (InvalidOperationException) { }
                lock (gate)
                {
                    if (capture != child) return;
                    running = false;
                    childPid = 0;
                    status = errorText.IndexOf("access denied", StringComparison.OrdinalIgnoreCase) >= 0
                        ? PermissionMessage() : "FPS 采集器已退出，请重新打开 FPS 开关";
                    capture = null;
                    sessionName = null;
                    chains.Clear();
                }
                // An externally terminated child can leave its real-time trace behind.
                CloseOwnSession(ownSession);
                child.Dispose();
            }
        }

        private static string PermissionMessage()
        {
            return "FPS 权限不足，请以管理员身份重新打开程序";
        }

        private ProcessStartInfo CreateInfo(string arguments)
        {
            return new ProcessStartInfo(executable, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(executable)
            };
        }

        private void CloseOwnSession(string ownSession)
        {
            if (String.IsNullOrEmpty(ownSession) || !File.Exists(executable)) return;
            try
            {
                using (Process stopper = new Process())
                {
                    stopper.StartInfo = CreateInfo("--session_name " + ownSession + " --terminate_existing_session");
                    stopper.Start();
                    // The short control command has bounded output; never create a CSV.
                    if (!stopper.WaitForExit(2000))
                    {
                        stopper.Kill();
                        stopper.WaitForExit(1000);
                    }
                }
            }
            catch (Exception) { /* A missing/inaccessible session is already stopped. */ }
        }

        private static void FinishProcess(Process child)
        {
            if (child == null) return;
            try
            {
                if (!child.HasExited && !child.WaitForExit(4000))
                {
                    child.Kill();
                    child.WaitForExit(1000);
                }
            }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
            finally { child.Dispose(); }
        }

        public double? Sample(int targetPid)
        {
            lock (gate)
            {
                if (!running || targetPid <= 0) return null;
                long now = Stopwatch.GetTimestamp();
                long bestCount = 0;
                double bestFps = 0;
                foreach (KeyValuePair<ChainKey, ChainState> item in chains)
                {
                    ChainState state = item.Value;
                    if (item.Key.ProcessId != targetPid || Seconds(now - state.LastArrival) > StaleSeconds) continue;
                    double traceNow = state.LastTime + Seconds(now - state.LastArrival);
                    long count = 0;
                    double seconds = 0;
                    for (int i = 0; i < BucketCount; i++)
                    {
                        Bucket bucket = state.Buckets[i];
                        double age = traceNow - bucket.Number * BucketSeconds;
                        if (age >= 0 && age <= WindowSeconds)
                        {
                            count += bucket.Intervals;
                            seconds += bucket.Seconds;
                        }
                    }
                    // Choose one dominant active swap chain. Summing chains overstates FPS.
                    if (count > bestCount && count >= 2 && seconds > 0)
                    {
                        bestCount = count;
                        bestFps = count / seconds;
                    }
                }
                status = bestCount > 0 ? "FPS 正在采集" : "FPS 已启用，目标暂无帧数据";
                return bestCount > 0 ? (double?)bestFps : null;
            }
        }

        private void AcceptLine(string line)
        {
            int count = ReadFields(line);
            if (count <= 0) return;
            if (line.StartsWith("Application,", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("\"Application\",", StringComparison.OrdinalIgnoreCase))
            {
                pidColumn = chainColumn = timeColumn = -1;
                headerFields = count;
                timeScale = 1.0;
                for (int i = 0; i < count; i++)
                {
                    string name = line.Substring(fieldStart[i], fieldLength[i]).Trim();
                    if (String.Equals(name, "ProcessID", StringComparison.OrdinalIgnoreCase)) pidColumn = i;
                    if (String.Equals(name, "SwapChainAddress", StringComparison.OrdinalIgnoreCase)) chainColumn = i;
                    if (String.Equals(name, "TimeInSeconds", StringComparison.OrdinalIgnoreCase)
                        || String.Equals(name, "CPUStartTimeInSeconds", StringComparison.OrdinalIgnoreCase)) timeColumn = i;
                }
                return;
            }
            if (pidColumn < 0 || chainColumn < 0 || timeColumn < 0 || count < headerFields) return;
            // PresentMon 2.6 prints Application without CSV quoting. Its .exe name can
            // itself contain a comma; remaining fields are numeric and stay aligned from the right.
            int offset = count - headerFields;
            int pid;
            ulong swap;
            double timestamp;
            if (!Int32.TryParse(Field(line, pidColumn + offset), NumberStyles.Integer, CultureInfo.InvariantCulture, out pid)
                || pid <= 0 || !TrySwap(Field(line, chainColumn + offset), out swap)
                || !Double.TryParse(Field(line, timeColumn + offset), NumberStyles.Float, CultureInfo.InvariantCulture, out timestamp)
                || Double.IsNaN(timestamp) || Double.IsInfinity(timestamp) || timestamp < 0) return;
            timestamp *= timeScale;
            ChainKey key = new ChainKey(pid, swap);
            ChainState state;
            if (!chains.TryGetValue(key, out state))
            {
                if (chains.Count >= MaximumSwapChains) RemoveOldest();
                state = new ChainState();
                chains.Add(key, state);
            }
            long now = Stopwatch.GetTimestamp();
            if (state.HasFrame && timestamp <= state.LastTime) return;
            double delta = state.HasFrame ? timestamp - state.LastTime : 0;
            if (delta > WindowSeconds) Array.Clear(state.Buckets, 0, BucketCount);
            if (state.HasFrame && delta > 0 && delta <= WindowSeconds)
            {
                long number = (long)Math.Floor(timestamp / BucketSeconds);
                int slot = (int)(number % BucketCount);
                Bucket bucket = state.Buckets[slot];
                if (bucket.Number != number) bucket = new Bucket { Number = number };
                bucket.Intervals++;
                bucket.Seconds += delta;
                state.Buckets[slot] = bucket;
            }
            state.LastTime = timestamp;
            state.LastArrival = now;
            state.HasFrame = true;
            receivedFrames++;
        }

        private int ReadFields(string line)
        {
            int count = 0;
            int position = 0;
            while (position <= line.Length && count < fieldStart.Length)
            {
                int start = position;
                int length;
                if (position < line.Length && line[position] == '"')
                {
                    start = ++position;
                    bool closed = false;
                    while (position < line.Length)
                    {
                        if (line[position++] != '"') continue;
                        if (position < line.Length && line[position] == '"') { position++; continue; }
                        closed = true;
                        break;
                    }
                    if (!closed) return -1;
                    length = position - start - 1;
                    while (position < line.Length && Char.IsWhiteSpace(line[position])) position++;
                    if (position < line.Length && line[position] != ',') return -1;
                }
                else
                {
                    while (position < line.Length && line[position] != ',') position++;
                    length = position - start;
                }
                fieldStart[count] = start;
                fieldLength[count++] = length;
                if (position == line.Length) return count;
                position++;
            }
            return -1;
        }

        private string Field(string line, int column)
        {
            return line.Substring(fieldStart[column], fieldLength[column]);
        }

        private static bool TrySwap(string value, out ulong swap)
        {
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return UInt64.TryParse(value.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out swap);
            return UInt64.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out swap);
        }

        private void RemoveOldest()
        {
            ChainKey oldestKey = new ChainKey();
            long oldest = Int64.MaxValue;
            foreach (KeyValuePair<ChainKey, ChainState> item in chains)
            {
                if (item.Value.LastArrival < oldest) { oldest = item.Value.LastArrival; oldestKey = item.Key; }
            }
            chains.Remove(oldestKey);
        }

        private static double Seconds(long ticks) { return ticks / (double)Stopwatch.Frequency; }

        public void Dispose()
        {
            lock (lifecycle)
            {
                StopCore();
                lock (gate) disposed = true;
            }
        }

        private struct ChainKey : IEquatable<ChainKey>
        {
            public readonly int ProcessId;
            public readonly ulong Swap;
            public ChainKey(int processId, ulong swap) { ProcessId = processId; Swap = swap; }
            public bool Equals(ChainKey other) { return ProcessId == other.ProcessId && Swap == other.Swap; }
            public override bool Equals(object obj) { return obj is ChainKey && Equals((ChainKey)obj); }
            public override int GetHashCode() { return ProcessId ^ (int)Swap ^ (int)(Swap >> 32); }
        }

        private struct Bucket
        {
            public long Number;
            public long Intervals;
            public double Seconds;
        }

        private sealed class ChainState
        {
            public readonly Bucket[] Buckets = new Bucket[BucketCount];
            public bool HasFrame;
            public double LastTime;
            public long LastArrival;
        }
    }
}
