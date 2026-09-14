using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace SquiFlow.ApplicationKernel.Features;

public static class StableExperimentAssignment
{
    public static int Bucket(ExperimentId experimentId, string subjectId, int bucketCount = 10_000)
    {
        if (string.IsNullOrWhiteSpace(subjectId))
        {
            throw new ArgumentException("Experiment subject must be non-empty.", nameof(subjectId));
        }

        if (bucketCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bucketCount));
        }

        var payload = Encoding.UTF8.GetBytes($"{experimentId.Value}\n{subjectId.Trim()}");
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(payload, hash);
        var value = BinaryPrimitives.ReadUInt64BigEndian(hash[..8]);
        return (int)(value % (ulong)bucketCount);
    }
}
