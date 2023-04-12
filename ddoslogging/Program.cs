using System;
using System.Diagnostics;
using SharpPcap;
using SharpPcap.LibPcap;

namespace DdosDetection
{
    class Program
    {
        // Bandwidth threshold in bits per second (bps)
        private const long BandwidthThreshold = 500 * 1000 * 1000; // 500 Mbps

        // Monitoring interval in milliseconds
        private const int MonitoringInterval = 1000;

        // PCAP output file path
        private static string PcapOutputFile => $"captured_packets_{DateTime.Now:yyyyMMdd_HHmmss}.pcap";

        private static bool _capturePackets = false;
        private static PcapDevice _captureDevice;
        private static CaptureFileWriterDevice _captureFileWriter;

        static void Main(string[] args)
        {
            var networkUsageTask = Task.Run(() => MonitorNetworkUsage());
            var packetCaptureTask = Task.Run(() => CapturePackets());

            Task.WaitAll(networkUsageTask, packetCaptureTask);
        }

        private static void MonitorNetworkUsage()
        {
            var networkInterfacePerformanceCounter = new PerformanceCounter("Network Interface", "Bytes Total/sec");
            networkInterfacePerformanceCounter.InstanceName = GetNetworkInterfaceInstanceName();

            bool wasCapturing = false;

            while (true)
            {
                double bytesPerSecond = networkInterfacePerformanceCounter.NextValue();
                double bitsPerSecond = bytesPerSecond * 8;

                Console.WriteLine($"Current network usage: {bitsPerSecond / 1_000_000} Mbps");

                if (bitsPerSecond >= BandwidthThreshold)
                {
                    Console.WriteLine("High network usage detected.");
                    _capturePackets = true;
                    wasCapturing = true;
                }
                else
                {
                    if (wasCapturing)
                    {
                        // Save captured packets and reset the capture file writer
                        _captureFileWriter?.Close();
                        _captureFileWriter = new CaptureFileWriterDevice(PcapOutputFile);
                        wasCapturing = false;
                    }
                    _capturePackets = false;
                }

                Thread.Sleep(MonitoringInterval);
            }
        }

        private static string GetNetworkInterfaceInstanceName()
        {
            return "Microsoft Hyper-V Network Adapter"; // Your network interface instance name
        }

        private static void CapturePackets()
        {
            Console.WriteLine("CAPTURE PACKETS IS BEING EXECUTED");
            Console.WriteLine("CAPTURE PACKETS IS BEING EXECUTED");
            Console.WriteLine("CAPTURE PACKETS IS BEING EXECUTED");
            Console.WriteLine("CAPTURE PACKETS IS BEING EXECUTED"); 
            Console.WriteLine("CAPTURE PACKETS IS BEING EXECUTED");
            Console.WriteLine("CAPTURE PACKETS IS BEING EXECUTED");

            var devices = CaptureDeviceList.Instance;

            if (devices.Count < 1)
            {
                Console.WriteLine("No devices were found.");
                return;
            }

            // Use the first available device
            _captureDevice = (PcapDevice)devices[0];
            // Print the device's name and description
            Console.WriteLine($"Selected device: {_captureDevice.Name}");
            Console.WriteLine($"Device description: {_captureDevice.Description}");

            _captureDevice.Open(DeviceModes.Promiscuous, 1000);
            _captureDevice.OnPacketArrival += Device_OnPacketArrival;

            // Initialize the capture file writer
            var filePath = PcapOutputFile;
            _captureFileWriter = new CaptureFileWriterDevice(filePath);
            Console.WriteLine($"Initialized capture file writer with file path: {filePath}");

            _captureDevice.StartCapture();
        }

        private static void Device_OnPacketArrival(object sender, PacketCapture e)
        {
            if (!_capturePackets || _captureFileWriter == null)
            {
                return;
            }
            Console.WriteLine("Packet arrived."); // Add this line
            _captureFileWriter.Write(e.GetPacket());
        }
    }
}