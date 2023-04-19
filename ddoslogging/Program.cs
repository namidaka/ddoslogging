using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using SharpPcap;
using SharpPcap.LibPcap;

namespace DdosCatcher
{
    class Program
    {
        // Bandwidth threshold in bits per second (bps)
        private const long BandwidthThreshold = 75 * 1000 * 1000; // 500 Mbps

        // Monitoring interval in milliseconds
        private const int MonitoringInterval = 1000;

        // PCAP output file path
        private const string PcapOutputFile = "captured_packets.pcap";

        private static bool _capturePackets;

        static void Main()
        {
            var networkUsageTask = Task.Run(MonitorNetworkUsage);
            var packetCaptureTask = Task.Run(CapturePackets);

            Task.WaitAny(networkUsageTask, packetCaptureTask);
        }

        private static ICaptureDevice GetFirstConnectedAdapter()
        {
            var allNetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up)
                .ToList();

            foreach (var ni in allNetworkInterfaces)
            {
                var connectedAdapter = CaptureDeviceList.Instance.FirstOrDefault(d => d.MacAddress?.ToString() == ni.GetPhysicalAddress()?.ToString());
                if (connectedAdapter != null)
                {
                    return connectedAdapter;
                }
            }
            return null;
        }

        private static void MonitorNetworkUsage()
        {
            var connectedAdapter = GetFirstConnectedAdapter();
            if (connectedAdapter == null)
            {
                Console.WriteLine("No connected network adapter found.");
                return;
            }

            PerformanceCounter networkInterfacePerformanceCounter = new("Network Interface", "Bytes Total/sec", connectedAdapter.Description);

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
            var connectedAdapter = GetFirstConnectedAdapter();
            if (connectedAdapter == null)
            {
                Console.WriteLine("No connected network adapter found.");
                return;
            }

            connectedAdapter.Open(DeviceModes.Promiscuous);

            CaptureFileWriterDevice captureFileWriter = new(PcapOutputFile, FileMode.Truncate);
            captureFileWriter.Open(connectedAdapter);

            int i = 0;
            connectedAdapter.OnPacketArrival += (sender, e) =>
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

            connectedAdapter.StartCapture();

            Console.WriteLine("Press Enter to stop...");
            Console.ReadLine();

            connectedAdapter.StopCapture();
            connectedAdapter.Close();
            captureFileWriter.Close();
        }
    }
}
