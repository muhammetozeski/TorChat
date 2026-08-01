using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Polly;

namespace Chat
{
    public static class Socks5Client
    {
        public static async Task<TcpClient> ConnectAsync(
            string proxyHost, int proxyPort,
            string targetHost, int targetPort,
            CancellationToken ct = default)
        {
            Log($"[START] Socks5Client.ConnectAsync parameters: proxyHost='{proxyHost}', proxyPort={proxyPort}, targetHost='{targetHost}', targetPort={targetPort}");

            var retryPolicy = Policy
                .Handle<SocketException>()
                .Or<IOException>()
                .Or<TimeoutException>()
                .WaitAndRetryAsync(
                    retryCount: 2,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(2),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        Log($"[RETRY] Socks5Client.ConnectAsync attempt {retryCount} failed. Waiting {timeSpan.TotalSeconds}s before retry. Exception: {exception.GetType().Name} - {exception.Message}", LogLevel.Warning);
                    });

            try
            {
                return await retryPolicy.ExecuteAsync(async () =>
                {
                    Log($"[EXECUTE] Attempting SOCKS5 connection to target '{targetHost}:{targetPort}' via proxy '{proxyHost}:{proxyPort}'");
                    var client = new TcpClient();
                    Log("Instantiated new TcpClient.");

                    try
                    {
                        Log($"Connecting TcpClient to proxy '{proxyHost}:{proxyPort}'...");
                        await client.ConnectAsync(proxyHost, proxyPort, ct);
                        Log($"TcpClient connected to proxy '{proxyHost}:{proxyPort}'.");

                        var stream = client.GetStream();
                        Log("Obtained NetworkStream from TcpClient.");

                        // Greeting
                        byte[] greeting = [0x05, 0x01, 0x00];
                        Log($"Sending SOCKS5 greeting [0x05, 0x01, 0x00] (Length: {greeting.Length})...");
                        await stream.WriteAsync(greeting, ct);
                        Log("SOCKS5 greeting sent successfully.");

                        byte[] greetingResp = new byte[2];
                        Log("Reading 2-byte SOCKS5 greeting response...");
                        await stream.ReadExactlyAsync(greetingResp, 0, 2, ct);
                        Log($"SOCKS5 greeting response received: [0x{greetingResp[0]:X2}, 0x{greetingResp[1]:X2}]");

                        if (greetingResp[0] != 0x05 || greetingResp[1] != 0x00)
                        {
                            Log($"SOCKS5 greeting validation failed! VER=0x{greetingResp[0]:X2}, AUTH=0x{greetingResp[1]:X2}", LogLevel.Error);
                            client.Close();
                            client.Dispose();
                            throw new IOException($"SOCKS5 proxy refused connection or unsupported auth (VER=0x{greetingResp[0]:X2}, AUTH=0x{greetingResp[1]:X2}).");
                        }
                        Log("SOCKS5 greeting validated.");

                        // Request
                        byte[] domainBytes = Encoding.ASCII.GetBytes(targetHost);
                        Log($"Encoded target host domain '{targetHost}' ({domainBytes.Length} bytes).");

                        byte[] request = new byte[4 + 1 + domainBytes.Length + 2];
                        request[0] = 0x05;
                        request[1] = 0x01;
                        request[2] = 0x00;
                        request[3] = 0x03;
                        request[4] = (byte)domainBytes.Length;
                        Buffer.BlockCopy(domainBytes, 0, request, 5, domainBytes.Length);
                        request[^2] = (byte)(targetPort >> 8);
                        request[^1] = (byte)(targetPort & 0xFF);
                        Log($"Sending SOCKS5 connect request buffer ({request.Length} bytes)...");

                        await stream.WriteAsync(request, ct);
                        Log("SOCKS5 connect request sent.");

                        // Header response
                        byte[] header = new byte[4];
                        Log("Reading 4-byte SOCKS5 response header...");
                        await stream.ReadExactlyAsync(header, 0, 4, ct);
                        Log($"SOCKS5 response header: VER=0x{header[0]:X2}, REP=0x{header[1]:X2}, RSV=0x{header[2]:X2}, ATYP=0x{header[3]:X2}");

                        if (header[0] != 0x05)
                        {
                            Log($"Invalid SOCKS5 version in header: 0x{header[0]:X2}", LogLevel.Error);
                            client.Close();
                            client.Dispose();
                            throw new IOException($"Invalid SOCKS5 response version 0x{header[0]:X2}.");
                        }

                        if (header[1] != 0x00)
                        {
                            string reason = header[1] switch
                            {
                                0x01 => "general failure",
                                0x02 => "connection not allowed",
                                0x03 => "network unreachable",
                                0x04 => "host unreachable",
                                0x05 => "connection refused",
                                0x06 => "TTL expired",
                                0x07 => "command not supported",
                                0x08 => "address type not supported",
                                _ => $"unknown (0x{header[1]:X2})"
                            };
                            Log($"SOCKS5 connect failed with REP=0x{header[1]:X2} ({reason}). Closing socket.", LogLevel.Error);
                            client.Close();
                            client.Dispose();
                            throw new IOException($"SOCKS5 connect failed: {reason}");
                        }
                        Log("SOCKS5 connect response validated: Success.");

                        // Drain bound address
                        int addrLen = header[3] switch
                        {
                            0x01 => 4,
                            0x04 => 16,
                            0x03 => await ReadDomainLengthAsync(stream, ct),
                            _ => throw new IOException($"Unknown SOCKS5 address type 0x{header[3]:X2}")
                        };
                        Log($"Calculated bound address length to drain: {addrLen} bytes.");

                        byte[] drain = new byte[addrLen + 2];
                        Log($"Reading {drain.Length} bytes to drain bound address & port...");
                        await stream.ReadExactlyAsync(drain, 0, drain.Length, ct);
                        Log("Bound address & port drained successfully.");

                        Log($"[END] Socks5Client.ConnectAsync succeeded for '{targetHost}:{targetPort}'");
                        return client;
                    }
                    catch (Exception ex)
                    {
                        Log($"[ERROR] Inner exception in Socks5Client connection: {ex.GetType().Name} - {ex.Message}. Disposing client.", LogLevel.Error);
                        try
                        {
                            client.Close();
                            client.Dispose();
                            Log("TcpClient disposed successfully.");
                        }
                        catch (Exception cleanupEx)
                        {
                            Log($"[CLEANUP ERROR] Failed disposing TcpClient: {cleanupEx.Message}", LogLevel.Error);
                        }
                        throw;
                    }
                });
            }
            catch (Exception ex)
            {
                Log($"[FATAL] Socks5Client.ConnectAsync failed after all retries exhausted: {ex.GetType().Name} - {ex.Message}", LogLevel.Error);
                throw;
            }
        }

        private static async Task<int> ReadDomainLengthAsync(NetworkStream stream, CancellationToken ct)
        {
            Log("[START] ReadDomainLengthAsync started.");

            var retryPolicy = Policy
                .Handle<IOException>()
                .Or<SocketException>()
                .WaitAndRetryAsync(3, attempt => TimeSpan.FromMilliseconds(200 * attempt),
                    (exception, span, attempt, ctx) =>
                    {
                        Log($"[RETRY] ReadDomainLengthAsync attempt {attempt} failed: {exception.Message}", LogLevel.Warning);
                    });

            try
            {
                return await retryPolicy.ExecuteAsync(async () =>
                {
                    byte[] buf = new byte[1];
                    Log("Reading 1 domain length byte from stream...");
                    await stream.ReadExactlyAsync(buf, 0, 1, ct);
                    Log($"Domain length byte read: {buf[0]}");
                    Log($"[END] ReadDomainLengthAsync returning length={buf[0]}");
                    return (int)buf[0];
                });
            }
            catch (Exception ex)
            {
                Log($"[ERROR] ReadDomainLengthAsync failed after retries: {ex.GetType().Name} - {ex.Message}", LogLevel.Error);
                throw;
            }
        }
    }
}
