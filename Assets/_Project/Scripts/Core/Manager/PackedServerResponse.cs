using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Scripts.Server;
using IdleKingdomFunction.Common;

namespace Scripts.Core.Manager
{
    internal sealed class PackedServerResponse
    {
        internal const int Version = 1;
        internal const int ChunkSize = 8192;
        private const int RawLimit = 8 * 1024 * 1024;
        private const int CompressedLimit = RawLimit + 64 * 1024;
        private readonly byte[] compressed;
        private readonly int rawBytes;
        private readonly string sha256;
        private readonly long protocolVersion;
        private readonly long serverVersion;
        internal string SnapshotId { get; }
        internal int ChunkCount { get; }
        internal int NextIndex { get; private set; }

        internal PackedServerResponse(JObject envelope)
        {
            var first = Header(envelope);
            rawBytes = checked((int)Number(first, "RawBytes", 1, RawLimit));
            int compressedBytes = checked((int)Number(first, "CompressedBytes", 1, CompressedLimit));
            compressed = new byte[compressedBytes];
            ChunkCount = (compressedBytes + ChunkSize - 1) / ChunkSize;
            protocolVersion = Number(envelope, "ProtocolVersion", ApiVersions.ProtocolVersion, ApiVersions.ProtocolVersion);
            serverVersion = Number(envelope, "ServerVersion", 0, long.MaxValue);
            var snapshot = first["SnapshotId"];
            if (snapshot != null && snapshot.Type != JTokenType.Null && snapshot.Type != JTokenType.String) throw Invalid();
            SnapshotId = (string)snapshot;
            if (SnapshotId?.Length > 128 || ChunkCount > 1 && string.IsNullOrWhiteSpace(SnapshotId)) throw Invalid();
            sha256 = Text(first, "Sha256");
            if (sha256.Length != 64) throw Invalid();
            foreach (char ch in sha256)
                if (!(ch >= '0' && ch <= '9' || ch >= 'a' && ch <= 'f')) throw Invalid();
            Add(envelope);
        }

        internal void Add(JObject envelope)
        {
            var part = Header(envelope);
            if (NextIndex >= ChunkCount) throw Invalid();
            Number(envelope, "ProtocolVersion", protocolVersion, protocolVersion);
            Number(envelope, "ServerVersion", serverVersion, serverVersion);
            Number(part, "Version", Version, Version);
            Number(part, "ChunkIndex", NextIndex, NextIndex);
            Number(part, "ChunkCount", ChunkCount, ChunkCount);
            Number(part, "RawBytes", rawBytes, rawBytes);
            Number(part, "CompressedBytes", compressed.Length, compressed.Length);
            var expectedId = SnapshotId == null ? JValue.CreateNull() : new JValue(SnapshotId);
            if (Text(part, "Encoding") != "gzip" || !JToken.DeepEquals(part["SnapshotId"] ?? JValue.CreateNull(), expectedId) ||
                Text(part, "Sha256") != sha256) throw Invalid();
            int offset = NextIndex * ChunkSize;
            int expected = Math.Min(ChunkSize, compressed.Length - offset);
            string encoded = Text(part, "Data");
            if (encoded.Length != (expected + 2) / 3 * 4) throw Invalid();
            byte[] chunk = Convert.FromBase64String(encoded);
            if (chunk.Length != expected) throw Invalid();
            Buffer.BlockCopy(chunk, 0, compressed, offset, expected);
            NextIndex++;
        }

        internal JObject Decode()
        {
            if (NextIndex != ChunkCount) throw Invalid();
            byte[] data = new byte[rawBytes];
            using (var stream = new GZipStream(new MemoryStream(compressed, false), CompressionMode.Decompress))
            {
                int offset = 0;
                while (offset < data.Length)
                {
                    int n = stream.Read(data, offset, data.Length - offset);
                    if (n == 0) throw Invalid();
                    offset += n;
                }
                if (stream.ReadByte() != -1) throw Invalid();
            }
            using (var hash = SHA256.Create())
                if (!string.Equals(BitConverter.ToString(hash.ComputeHash(data)).Replace("-", ""), sha256, StringComparison.OrdinalIgnoreCase))
                    throw Invalid();
            var body = Parse(new UTF8Encoding(false, true).GetString(data));
            Number(body, "ProtocolVersion", protocolVersion, protocolVersion);
            Number(body, "ServerVersion", serverVersion, serverVersion);
            if (body.Property("PackedResponse") != null) throw Invalid();
            return body;
        }

        internal static JObject ReadBody(object body)
        {
            if (body is JObject json) return json;
            if (body is string text) return Parse(text);
            var serializer = new JsonSerializer { DateParseHandling = DateParseHandling.None, MaxDepth = 64 };
            serializer.Converters.Add(new StrictIntegerConverter());
            return JObject.FromObject(body, serializer);
        }

        private static JObject Parse(string text)
        {
            using var reader = new JsonTextReader(new StringReader(text)) { DateParseHandling = DateParseHandling.None, MaxDepth = 64 };
            var body = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (reader.Read()) throw Invalid();
            return body;
        }

        private static JObject Header(JObject envelope) => envelope?["PackedResponse"] as JObject ?? throw Invalid();
        private static string Text(JObject body, string name) =>
            body[name]?.Type == JTokenType.String ? body.Value<string>(name) : throw Invalid();
        private static long Number(JObject body, string name, long min, long max)
        {
            if (body[name]?.Type != JTokenType.Integer) throw Invalid();
            long n = body.Value<long>(name);
            return n >= min && n <= max ? n : throw Invalid();
        }
        private static JsonSerializationException Invalid() => new JsonSerializationException("분할 응답의 형식 또는 내용이 일치하지 않습니다.");
    }
}
