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
        private const long BandwidthThreshold = 50 * 1000 * 1000; // 50 Mbps

        // Monitoring interval in milliseconds
        private const int MonitoringInterval = 1000;

        // PCAP output file path
        private const string PcapOutputFile = "captured_packets.pcap";

        private static string _interface;
        private static bool _capturePackets;

        static void Main()
        {
            ChooseNetworkInterface();
            var networkUsageTask = Task.Run(MonitorNetworkUsage);
            var packetCaptureTask = Task.Run(CapturePackets);

            Task.WaitAll(networkUsageTask, packetCaptureTask);
        }

        private static void ChooseNetworkInterface()
        {
            Console.WriteLine("Please choose the correct network interface:");
            var devices = NetworkInterface.GetAllNetworkInterfaces();
            for (int i = 0; i < devices.Length; i++)
            {
                Console.WriteLine($"{i}. {devices[i].Description}");
            }

            int selectedIndex;
            while (true)
            {
                Console.Write("Enter the number of the correct network interface: ");
                if (int.TryParse(Console.ReadLine(), out selectedIndex) && selectedIndex >= 0 && selectedIndex < devices.Length)
                {
                    break;
                }

                Console.WriteLine("Invalid input. Please try again.");
            }

            _interface = devices[selectedIndex].Description;
        }


        private static void MonitorNetworkUsage()
        {
            NetworkInterface? networkInterface = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(d => d.Description == _interface);

            if (networkInterface == null)
            {
                Console.WriteLine($"Could not find the network interface {_interface}");
                return;
            }

            long prevBytesReceived = -1;

            while (true)
            {
                IPv4InterfaceStatistics stats = networkInterface.GetIPv4Statistics();
                long bytesReceived = stats.BytesReceived;

                if (prevBytesReceived == -1)
                {
                    prevBytesReceived = bytesReceived;
                    continue;
                }

                double bitsPerSecond = (bytesReceived - prevBytesReceived) * 8;

                Console.WriteLine($"Current network usage (received): {bitsPerSecond / 1_000_000} Mbps");

                if (bitsPerSecond >= BandwidthThreshold)
                {
                    Console.WriteLine("High network usage detected.");
                    _capturePackets = true;
                }
                else
                {
                    _capturePackets = false;
                }

                prevBytesReceived = bytesReceived;

                Thread.Sleep(MonitoringInterval);
            }
        }




        private static void CapturePackets()
        {
            var captureDevice = CaptureDeviceList.Instance.First(d => d.Description == _interface);
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
            Console.WriteLine("enter pressed");
            captureDevice.StopCapture();
            captureDevice.Close();
            captureFileWriter.Close();
        }
    }
}
