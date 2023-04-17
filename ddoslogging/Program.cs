using System.Diagnostics;
using SharpPcap;
using SharpPcap.LibPcap;

namespace DdosCatcher
{
    class Program
    {
        // Bandwidth threshold in bits per second (bps)
        private const long BandwidthThreshold = 500 * 1000 * 1000; // 500 Mbps

        // Monitoring interval in milliseconds
        private const int MonitoringInterval = 1000;

        // PCAP output file path
        private const string PcapOutputFile = "captured_packets.pcap";

        private const string Interface = "Microsoft Hyper-V Network Adapter";

        private static bool _capturePackets;

        static void Main()
        {
            var networkUsageTask = Task.Run(MonitorNetworkUsage);
            var packetCaptureTask = Task.Run(CapturePackets);

            Task.WaitAny(networkUsageTask, packetCaptureTask);
        }

        private static void MonitorNetworkUsage()
        {
            PerformanceCounter networkInterfacePerformanceCounter = new("Network Interface", "Bytes Total/sec", Interface);

            while (true)
            {
                double bytesPerSecond = networkInterfacePerformanceCounter.NextValue();
                double bitsPerSecond = bytesPerSecond * 8;

                Console.WriteLine($"Current network usage: {bitsPerSecond / 1_000_000} Mbps");

                if (bitsPerSecond >= BandwidthThreshold)
                {
                    Console.WriteLine("High network usage detected.");
                    _capturePackets = true;
                }
                else
                {
                    _capturePackets = false;
                }

                Thread.Sleep(MonitoringInterval);
            }
        }

        private static void CapturePackets()
        {
            var captureDevice = CaptureDeviceList.Instance.First(d => d.Description == Interface);
            captureDevice.Open(DeviceModes.Promiscuous);

            CaptureFileWriterDevice captureFileWriter = new(PcapOutputFile, FileMode.Truncate);
            captureFileWriter.Open(captureDevice);

            int i = 0;
            captureDevice.OnPacketArrival += (sender, e) =>
            {
                if (!_capturePackets)
                {
                    return;
                }

                i += 1;
                if (i % 100 != 0)
                {
                    return;
                }

                captureFileWriter.Write(e.GetPacket());
            };

            captureDevice.StartCapture();

            Console.WriteLine("Press Enter to stop...");
            Console.ReadLine();

            captureDevice.StopCapture();
            captureDevice.Close();
            captureFileWriter.Close();
        }
    }
}