using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class WebSocketConnection
{
    public string Id { get; private set; }
    public bool IsConnected { get; private set; }

    public event Action<string> OnMessage;
    public event Action OnDisconnected;

    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly object _sendLock = new object();

    public WebSocketConnection(TcpClient client, NetworkStream stream)
    {
        _client = client;
        _stream = stream;
        Id = Guid.NewGuid().ToString("N").Substring(0, 8);
        IsConnected = true;
    }

    public void StartReceiving()
    {
        var thread = new Thread(ReceiveLoop) { IsBackground = true };
        thread.Start();
    }

    // ── Receive ──────────────────────────────────

    private void ReceiveLoop()
    {
        try
        {
            while (IsConnected && _client.Connected)
            {
                byte[] header = ReadBytes(2);
                int opcode = header[0] & 0x0F;
                bool masked = (header[1] & 0x80) != 0;
                long payloadLen = header[1] & 0x7F;

                if (payloadLen == 126)
                {
                    byte[] ext = ReadBytes(2);
                    payloadLen = (ext[0] << 8) | ext[1];
                }
                else if (payloadLen == 127)
                {
                    byte[] ext = ReadBytes(8);
                    payloadLen = 0;
                    for (int i = 0; i < 8; i++)
                        payloadLen = (payloadLen << 8) | ext[i];
                }

                byte[] maskKey = masked ? ReadBytes(4) : null;
                byte[] payload = ReadBytes((int)payloadLen);

                if (masked && maskKey != null)
                    for (int i = 0; i < payload.Length; i++)
                        payload[i] ^= maskKey[i % 4];

                switch (opcode)
                {
                    case 0x1: // text frame
                        OnMessage?.Invoke(Encoding.UTF8.GetString(payload));
                        break;
                    case 0x8: // close
                        SendCloseFrame();
                        return;
                    case 0x9: // ping
                        SendPong(payload);
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            if (IsConnected)
                Debug.Log($"[WS] Connection {Id} read error: {ex.Message}");
        }
        finally
        {
            Close();
        }
    }

    private byte[] ReadBytes(int count)
    {
        byte[] buf = new byte[count];
        int read = 0;
        while (read < count)
        {
            int n = _stream.Read(buf, read, count - read);
            if (n == 0) throw new IOException("Connection closed");
            read += n;
        }
        return buf;
    }

    // ── Send ─────────────────────────────────────

    public void Send(string message)
    {
        if (!IsConnected) return;
        lock (_sendLock)
        {
            try
            {
                byte[] payload = Encoding.UTF8.GetBytes(message);
                byte[] frame = BuildTextFrame(payload);
                _stream.Write(frame, 0, frame.Length);
                _stream.Flush();
            }
            catch (Exception)
            {
                Close();
            }
        }
    }

    private static byte[] BuildTextFrame(byte[] payload)
    {
        int headerLen;
        byte[] frame;

        if (payload.Length <= 125)
        {
            headerLen = 2;
            frame = new byte[headerLen + payload.Length];
            frame[1] = (byte)payload.Length;
        }
        else if (payload.Length <= 65535)
        {
            headerLen = 4;
            frame = new byte[headerLen + payload.Length];
            frame[1] = 126;
            frame[2] = (byte)(payload.Length >> 8);
            frame[3] = (byte)(payload.Length & 0xFF);
        }
        else
        {
            headerLen = 10;
            frame = new byte[headerLen + payload.Length];
            frame[1] = 127;
            long len = payload.Length;
            for (int i = 7; i >= 0; i--)
            {
                frame[2 + i] = (byte)(len & 0xFF);
                len >>= 8;
            }
        }

        frame[0] = 0x81; // FIN + text opcode
        Array.Copy(payload, 0, frame, headerLen, payload.Length);
        return frame;
    }

    private void SendPong(byte[] payload)
    {
        lock (_sendLock)
        {
            try
            {
                byte[] frame = new byte[2 + payload.Length];
                frame[0] = 0x8A; // FIN + pong opcode
                frame[1] = (byte)payload.Length;
                Array.Copy(payload, 0, frame, 2, payload.Length);
                _stream.Write(frame, 0, frame.Length);
            }
            catch { /* ignore pong failures */ }
        }
    }

    private void SendCloseFrame()
    {
        lock (_sendLock)
        {
            try
            {
                byte[] frame = { 0x88, 0x00 }; // FIN + close opcode, zero length
                _stream.Write(frame, 0, frame.Length);
            }
            catch { /* best-effort */ }
        }
    }

    // ── Lifecycle ────────────────────────────────

    public void Close()
    {
        if (!IsConnected) return;
        IsConnected = false;

        try { _stream?.Close(); } catch { }
        try { _client?.Close(); } catch { }

        OnDisconnected?.Invoke();
    }
}
