// Décodeur minimal de journal btsnoop (HCI H4) centré sur les connexions LE : dotnet run btsnoop.cs -- <fichier> [adresse]
using System.Buffers.Binary;

var path = args[0];
var filter = args.Length > 1 ? args[1].Replace(":", "").ToUpperInvariant() : null;
var data = File.ReadAllBytes(path);
if (!data.AsSpan(0, 8).SequenceEqual("btsnoop\0"u8))
    throw new InvalidDataException("Pas un fichier btsnoop.");

var handles = new Dictionary<ushort, string>();   // handle -> adresse
var offset = 16;
while (offset + 24 <= data.Length)
{
    var included = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset + 4));
    var flags = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset + 8));
    var micros = BinaryPrimitives.ReadInt64BigEndian(data.AsSpan(offset + 16));
    var packet = data.AsSpan(offset + 24, Math.Min(included, data.Length - offset - 24));
    offset += 24 + included;
    if (packet.IsEmpty)
        continue;

    // Microsecondes depuis le 1er janvier de l'an 0 ; 0x00dcddb30f2f8000 = écart jusqu'à 1970.
    var time = DateTimeOffset.FromUnixTimeMilliseconds((micros - 0x00dcddb30f2f8000) / 1000).ToLocalTime().DateTime;
    var sent = (flags & 1) == 0;  // bit 0 : 0 = hôte → contrôleur
    var line = Describe(packet, sent);
    if (line is null)
        continue;
    Console.WriteLine($"{time:HH:mm:ss.fff} {(sent ? "→" : "←")} {line}");
}

string? Describe(ReadOnlySpan<byte> p, bool sent)
{
    switch (p[0])
    {
        case 0x01 when p.Length >= 4: // commande
        {
            var opcode = BinaryPrimitives.ReadUInt16LittleEndian(p[1..]);
            var args = p[4..];
            return opcode switch
            {
                0x200D or 0x2043 => $"CMD LE Create Connection {Mac(args.Slice(opcode == 0x200D ? 6 : 5, 6))}",
                0x2013 => $"CMD LE Connection Update h={U16(args)} int={U16(args[2..])}-{U16(args[4..])} lat={U16(args[6..])} to={U16(args[8..])}",
                0x2020 => $"CMD LE Remote Conn Param Request Reply h={U16(args)} int={U16(args[2..])}-{U16(args[4..])} lat={U16(args[6..])} to={U16(args[8..])}",
                0x2021 => $"CMD LE Remote Conn Param Request NEGATIVE h={U16(args)} reason=0x{args[2]:X2}",
                0x2022 => $"CMD LE Set Data Length h={U16(args)} tx={U16(args[2..])}",
                0x2032 => $"CMD LE Set PHY h={U16(args)} tx=0x{args[3]:X2} rx=0x{args[4]:X2}",
                0x0406 => $"CMD Disconnect h={U16(args)} reason=0x{args[2]:X2}",
                0x2019 => $"CMD LE Enable Encryption h={U16(args)}",
                _ => null,
            };
        }
        case 0x04 when p.Length >= 3: // événement
        {
            var code = p[1];
            var e = p[3..];
            if (code == 0x05)
                return Track(U16(e[1..])) ? $"EVT Disconnection Complete h={U16(e[1..])} status=0x{e[0]:X2} reason=0x{e[3]:X2} ({Reason(e[3])})" : null;
            if (code == 0x08)
                return $"EVT Encryption Change h={U16(e[1..])} status=0x{e[0]:X2} on={e[3]}";
            if (code == 0x0E && e.Length >= 4)
            {
                var opcode = U16(e[1..]);
                return opcode is 0x2013 or 0x2020 or 0x2021 or 0x2022 or 0x2032 ? $"EVT Command Complete 0x{opcode:X4} status=0x{e[3]:X2}" : null;
            }
            if (code == 0x0F && e.Length >= 4)
            {
                var opcode = U16(e[2..]);
                return opcode is 0x2013 or 0x2032 or 0x200D or 0x2043 or 0x2019 ? $"EVT Command Status 0x{opcode:X4} status=0x{e[0]:X2}" : null;
            }
            if (code != 0x3E)
                return null;
            var sub = e[0];
            var le = e[1..];
            switch (sub)
            {
                case 0x01 or 0x0A:
                {
                    var handle = U16(le[1..]);
                    var mac = Mac(le.Slice(5, 6));
                    if (filter is not null && mac.Replace(":", "") != filter) return null;
                    handles[handle] = mac;
                    var tail = sub == 0x01 ? le[11..] : le[23..];
                    return $"EVT LE Connection Complete status=0x{le[0]:X2} h={handle} role={le[3]} peer={mac} int={U16(tail)} lat={U16(tail[2..])} to={U16(tail[4..])}";
                }
                case 0x03:
                    return Track(U16(le[1..])) ? $"EVT LE Connection Update Complete status=0x{le[0]:X2} h={U16(le[1..])} int={U16(le[3..])} lat={U16(le[5..])} to={U16(le[7..])}" : null;
                case 0x06:
                    return Track(U16(le)) ? $"EVT LE Remote Connection Parameter Request h={U16(le)} int={U16(le[2..])}-{U16(le[4..])} lat={U16(le[6..])} to={U16(le[8..])}" : null;
                case 0x07:
                    return Track(U16(le)) ? $"EVT LE Data Length Change h={U16(le)} tx={U16(le[2..])} rx={U16(le[6..])}" : null;
                case 0x0C:
                    return Track(U16(le[1..])) ? $"EVT LE PHY Update Complete status=0x{le[0]:X2} h={U16(le[1..])} tx={le[3]} rx={le[4]}" : null;
                case 0x04:
                    return Track(U16(le[1..])) ? $"EVT LE Read Remote Features status=0x{le[0]:X2}" : null;
                default:
                    return null;
            }
        }
        case 0x02 when p.Length >= 9: // ACL
        {
            var handle = (ushort)(U16(p[1..]) & 0x0FFF);
            if (!Track(handle)) return null;
            var cid = U16(p[7..]);
            var payload = p[9..];
            return cid switch
            {
                0x0005 => $"L2CAP signalisation {Signal(payload)}",
                0x0006 => $"SMP {Smp(payload)}",
                0x0004 => $"ATT {Att(payload)}",
                _ => $"L2CAP cid=0x{cid:X4} {Convert.ToHexString(payload)}",
            };
        }
        default:
            return null;
    }
}

bool Track(ushort handle) => handles.ContainsKey(handle);

static ushort U16(ReadOnlySpan<byte> b) => BinaryPrimitives.ReadUInt16LittleEndian(b);

static string Mac(ReadOnlySpan<byte> b) => $"{b[5]:X2}:{b[4]:X2}:{b[3]:X2}:{b[2]:X2}:{b[1]:X2}:{b[0]:X2}";

static string Signal(ReadOnlySpan<byte> s) => s[0] switch
{
    0x12 => $"Connection Parameter Update REQUEST int={U16(s[4..])}-{U16(s[6..])} lat={U16(s[8..])} to={U16(s[10..])}",
    0x13 => $"Connection Parameter Update RESPONSE result={U16(s[4..])}",
    0x01 => $"Command Reject reason={U16(s[4..])}",
    _ => $"code=0x{s[0]:X2} {Convert.ToHexString(s)}",
};

static string Smp(ReadOnlySpan<byte> s) => s[0] switch
{
    0x01 => $"Pairing Request {Convert.ToHexString(s)}",
    0x02 => $"Pairing Response {Convert.ToHexString(s)}",
    0x05 => $"Pairing Failed reason=0x{s[1]:X2}",
    0x0B => $"Security Request auth=0x{s[1]:X2}",
    _ => $"code=0x{s[0]:X2}",
};

static string Att(ReadOnlySpan<byte> a) => a[0] switch
{
    0x01 => $"Error op=0x{a[1]:X2} handle=0x{U16(a[2..]):X4} err=0x{a[4]:X2}",
    0x02 => $"Exchange MTU Request {U16(a[1..])}",
    0x03 => $"Exchange MTU Response {U16(a[1..])}",
    0x08 => "Read By Type Request",
    0x09 => "Read By Type Response",
    0x0A => $"Read Request handle=0x{U16(a[1..]):X4}",
    0x0B => $"Read Response {Convert.ToHexString(a[1..])}",
    0x10 => "Read By Group Type Request",
    0x11 => "Read By Group Type Response",
    0x12 => $"Write Request handle=0x{U16(a[1..]):X4} {Convert.ToHexString(a[3..])}",
    0x13 => "Write Response",
    0x52 => $"Write Command handle=0x{U16(a[1..]):X4} {Convert.ToHexString(a[3..])}",
    0x1D => $"Handle Value Indication handle=0x{U16(a[1..]):X4}",
    0x1E => "Handle Value Confirmation",
    _ => $"op=0x{a[0]:X2} {Convert.ToHexString(a)}",
};

static string Reason(byte r) => r switch
{
    0x08 => "supervision timeout",
    0x13 => "remote user terminated",
    0x16 => "local host terminated",
    0x22 or 0x28 => "LL response timeout / instant passed",
    0x23 => "LL procedure collision",
    0x3B => "unacceptable connection parameters",
    0x3D => "MIC failure",
    0x3E => "connection failed to be established",
    _ => "?",
};
