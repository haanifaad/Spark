using System;

namespace OpenClaw.Shared.Audio;

/// <summary>
/// High-performance thread-safe circular ring buffer for streaming audio samples.
/// Designed for low latency, zero allocations during streaming, and sliding window audio inspection.
/// </summary>
public sealed class AudioRingBuffer
{
    private readonly float[] _buffer;
    private readonly object _gate = new();
    private int _writeIndex;
    private int _count;
    private long _totalWritten;

    public int Capacity => _buffer.Length;
    public int AvailableSamples
    {
        get
        {
            lock (_gate)
            {
                return _count;
            }
        }
    }
    public long TotalWritten
    {
        get
        {
            lock (_gate)
            {
                return _totalWritten;
            }
        }
    }

    public AudioRingBuffer(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");

        _buffer = new float[capacity];
    }

    /// <summary>
    /// Writes audio samples into the ring buffer.
    /// If samples exceed available space, oldest samples are overwritten.
    /// </summary>
    public void Write(ReadOnlySpan<float> samples)
    {
        if (samples.IsEmpty)
            return;

        lock (_gate)
        {
            int capacity = _buffer.Length;
            int samplesToWrite = samples.Length;

            if (samplesToWrite >= capacity)
            {
                // If writing more than capacity, keep only the latest 'capacity' samples
                var slice = samples.Slice(samplesToWrite - capacity);
                slice.CopyTo(_buffer.AsSpan());
                _writeIndex = 0;
                _count = capacity;
                _totalWritten += samplesToWrite;
                return;
            }

            int firstChunk = Math.Min(samplesToWrite, capacity - _writeIndex);
            samples.Slice(0, firstChunk).CopyTo(_buffer.AsSpan(_writeIndex, firstChunk));

            int remaining = samplesToWrite - firstChunk;
            if (remaining > 0)
            {
                samples.Slice(firstChunk, remaining).CopyTo(_buffer.AsSpan(0, remaining));
                _writeIndex = remaining;
            }
            else
            {
                _writeIndex = (_writeIndex + firstChunk) % capacity;
            }

            _count = Math.Min(capacity, _count + samplesToWrite);
            _totalWritten += samplesToWrite;
        }
    }

    /// <summary>
    /// Reads and consumes up to 'destination.Length' samples from the head of the buffer.
    /// Returns the number of samples read.
    /// </summary>
    public int Read(Span<float> destination)
    {
        if (destination.IsEmpty)
            return 0;

        lock (_gate)
        {
            int available = _count;
            if (available == 0)
                return 0;

            int toRead = Math.Min(destination.Length, available);
            int capacity = _buffer.Length;
            int readIndex = (_writeIndex - _count + capacity) % capacity;

            int firstChunk = Math.Min(toRead, capacity - readIndex);
            _buffer.AsSpan(readIndex, firstChunk).CopyTo(destination.Slice(0, firstChunk));

            int remaining = toRead - firstChunk;
            if (remaining > 0)
            {
                _buffer.AsSpan(0, remaining).CopyTo(destination.Slice(firstChunk, remaining));
            }

            _count -= toRead;
            return toRead;
        }
    }

    /// <summary>
    /// Peeks up to 'count' latest written samples without consuming them (sliding window inspection).
    /// Returns the number of samples copied to destination.
    /// </summary>
    public int PeekLatest(Span<float> destination)
    {
        if (destination.IsEmpty)
            return 0;

        lock (_gate)
        {
            int available = _count;
            if (available == 0)
                return 0;

            int toRead = Math.Min(destination.Length, available);
            int capacity = _buffer.Length;
            int startIndex = (_writeIndex - toRead + capacity) % capacity;

            int firstChunk = Math.Min(toRead, capacity - startIndex);
            _buffer.AsSpan(startIndex, firstChunk).CopyTo(destination.Slice(0, firstChunk));

            int remaining = toRead - firstChunk;
            if (remaining > 0)
            {
                _buffer.AsSpan(0, remaining).CopyTo(destination.Slice(firstChunk, remaining));
            }

            return toRead;
        }
    }

    /// <summary>
    /// Clears all buffered samples and resets positions.
    /// </summary>
    public void Clear()
    {
        lock (_gate)
        {
            _writeIndex = 0;
            _count = 0;
            Array.Clear(_buffer, 0, _buffer.Length);
        }
    }

    /// <summary>
    /// Reads all currently available samples into a newly allocated float array.
    /// </summary>
    public float[] ToArray()
    {
        lock (_gate)
        {
            if (_count == 0)
                return Array.Empty<float>();

            var result = new float[_count];
            PeekLatest(result.AsSpan());
            return result;
        }
    }
}
