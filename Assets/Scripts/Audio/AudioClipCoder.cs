using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NAudio.Wave;
using NAudio.Lame;
using NLayer.NAudioSupport;
using UnityEngine;

namespace AudioStreaming
{
    public static class AudioClipCoder
    {

        public static byte[] ReduceChannels(byte[] buffer, int bytesPerSample = 4, int channels = 7, int newChannels = 6)
        {
            if (channels <= newChannels) return buffer;
            
            byte[] sampled = new byte[buffer.Length / channels * newChannels];

            for (int i = 0; i < buffer.Length / (bytesPerSample * channels); i++)
            {
                for (int j = 0; j < newChannels; j++)
                {
                    int id = i * channels * bytesPerSample + j * bytesPerSample;

                    Array.Copy(buffer, id, sampled, i * newChannels * bytesPerSample + j * bytesPerSample,
                        bytesPerSample);
                }
            }

            return sampled;
        }
        public static float[] AudioClipToBuffer(AudioClip clip, out int freq, out int channels)
        {
            float[] pcm = new float[clip.samples * clip.channels];
            clip.GetData(pcm, 0);
            freq = clip.frequency;
            channels = clip.channels;
            return pcm;
        }

        private static int clipCount = 0;
        public static AudioClip BufferToAudioClip(float[] buffer, int sampleCount, int channels, int sampleRate)
        {

            AudioClip clip = AudioClip.Create($"Decoded clip {clipCount}", sampleCount, channels, sampleRate, false);
            clipCount += 1;
            clip.SetData(buffer, 0);

            return clip;
        }

        public static byte[] Encode(float[] pcm, int freq, int channels, LAMEPreset quality, CompressionType compressionType)
        {
            switch (compressionType)
            {
                case CompressionType.MP3:
                    return EncodeMp3(pcm, freq, channels, quality);
                    break;
                case CompressionType.WAV:
                    return EncodeWav(pcm, freq, channels);
                    break;
                case CompressionType.ZIP:
                    return EncodeZip(pcm, freq, channels);
                    break;
            }

            return null;
        }
        public static byte[] Encode(byte[] pcm, int rate, int channels, int bytesPerSecond, int blockAlign, int bitsPerFrame,
            LAMEPreset quality, CompressionType compressionType, string path = "")
        {
            switch (compressionType)
            {
                case CompressionType.MP3:
                    if (path == "") return EncodeMp3(pcm, rate, channels, bytesPerSecond, blockAlign, bitsPerFrame, quality);
                    EncodeMp3OnDisk(pcm, rate, channels, path);
                    return new byte[0];
                    break;
                case CompressionType.WAV:
                    return EncodeWav(pcm, rate, channels, bytesPerSecond, blockAlign, bitsPerFrame);
                    break;
                case CompressionType.ZIP:
                    return EncodeZip(pcm, rate, channels, bytesPerSecond, blockAlign, bitsPerFrame);
                    break;
            }

            return null;
        }
        
        public static float[] Decode(byte[] buffer, out int sampleCount, out int sampleRate, out int channels, CompressionType compressionType)
        {
            sampleCount = sampleRate = channels = 0;
            switch (compressionType)
            {
                case CompressionType.MP3:
                    return DecodeMp3(buffer, out sampleCount, out sampleRate, out channels);
                    break;
                case CompressionType.WAV:
                    return DecodeWav(buffer, out sampleCount, out sampleRate, out channels);
                    break;
                case CompressionType.ZIP:
                    return DecodeZip(buffer, out sampleCount, out sampleRate, out channels);
                    break;
            }

            return null;
        }

        private static byte[] EncodeMp3(float[] pcm, int freq, int channels, LAMEPreset quality)
        {
            var pcmData = new byte[pcm.Length * 4];
            Buffer.BlockCopy(pcm, 0, pcmData, 0, pcmData.Length);
            var pcm16 = new byte[0];
            
            using (MemoryStream readStream = new MemoryStream(pcmData))
            {
                var s = new RawSourceWaveStream(readStream, WaveFormat.CreateIeeeFloatWaveFormat(freq, channels));
                using (var wave = new Wave32To16Stream(s))
                {
                    pcm16 = new byte[wave.Length];
                    wave.Read(pcm16, 0, (int) wave.Length);
                }
            }
            
            using (MemoryStream output = new MemoryStream())
            {
                using (var mp3 = new LameMP3FileWriter(output,
                    new WaveFormat(freq, channels), quality))
                {
                    mp3.Write(pcm16, 0, pcm16.Length);
                    mp3.Close();
                    return output.GetBuffer();
                }
            }
        }

        private static byte[] EncodeMp3(byte[] ieee, int rate, int channels, int bytesPerSecond, int blockAlign,
            int bitsPerFrame, LAMEPreset quality)
        {
            var pcm16 = new byte[0];

            using (MemoryStream readStream = new MemoryStream(ieee))
            {
                var s = new RawSourceWaveStream(readStream, WaveFormat.CreateIeeeFloatWaveFormat(rate, channels));
                using (var wave = new Wave32To16Stream(s))
                {
                    pcm16 = new byte[wave.Length];
                    wave.Read(pcm16, 0, (int) wave.Length);
                }
            }

            Debug.LogError("1");
            using (MemoryStream output = new MemoryStream())
            {
                Debug.LogError("2");
                WaveFormat format = new WaveFormat(rate, channels);
                using (var mp3 = new LameMP3FileWriter(output, format, rate))
                {
                    Debug.LogError("3");
                    mp3.Write(pcm16, 0, pcm16.Length);
                    mp3.Close();
                    Debug.LogError("4");
                    return output.GetBuffer();
                }
            }
        }

        private static void EncodeMp3OnDisk(byte[] ieee, int rate, int channels, string path)
        {
            var pcm16 = new byte[0];

            using (MemoryStream readStream = new MemoryStream(ieee))
            {
                var s = new RawSourceWaveStream(readStream, WaveFormat.CreateIeeeFloatWaveFormat(rate, channels));
                using (var wave = new Wave32To16Stream(s))
                {
                    pcm16 = new byte[wave.Length];
                    wave.Read(pcm16, 0, (int) wave.Length);
                }
            }

            string finalPath = Path.Combine(path, "recordedAudio.mp3");

            WaveFormat format = new WaveFormat(rate, channels);
            using (var mp3 = new LameMP3FileWriter(finalPath, format, rate))
            {
                mp3.Write(pcm16, 0, pcm16.Length);
                mp3.Close();
            }
            
            Debug.LogError($"Data encoded on dist. Check path {finalPath}");
        }

        private static byte[] EncodeWav(float[] pcm, int freq, int channels)
        {
            float rescaleFactor = 32767; //to convert Int16 to Float

            Int16[] intData = new Int16[pcm.Length];
            byte[] pcmData = new byte[pcm.Length * 2];

            for (int i = 0; i < pcm.Length; i++)
            {
                intData[i] = (short) (pcm[i] * rescaleFactor);
                Byte[] byteArr = BitConverter.GetBytes(intData[i]);
                byteArr.CopyTo(pcmData, i * 2);
            }

            using (MemoryStream output = new MemoryStream())
            {

                using (var wav = new WaveFileWriter(output,
                    new WaveFormat(freq, channels)))
                {
                    wav.Write(pcmData, 0, pcmData.Length);
                    wav.Close();
                    return output.GetBuffer();
                }
            }
        }
        private static byte[] EncodeWav(byte[] ieee, int rate, int channels, int bytesPerSecond, int blockAlign, int bitsPerFrame)
        {
            using (MemoryStream output = new MemoryStream())
            {
                WaveFormat format = WaveFormat.CreateCustomFormat(WaveFormatEncoding.IeeeFloat,
                    rate, //SampleRate
                    channels, bytesPerSecond, blockAlign, bitsPerFrame);
                
                using (var wav = new WaveFileWriter(output,
                    format))
                {
                    wav.Write(ieee, 0, ieee.Length);
                    wav.Close();
                    return output.GetBuffer();
                }
            }
        }

        private static byte[] EncodeZip(float[] pcm, int freq, int channels)
        {
            byte[] compressed;
            var dataByte = new byte[pcm.Length * 4];
            Buffer.BlockCopy(pcm, 0, dataByte, 0, dataByte.Length);
            using (var memoryStream = new MemoryStream())
            {
                using (DeflateStream compressionStream = new DeflateStream(memoryStream,
                    System.IO.Compression.CompressionLevel.Fastest))
                {
                    compressionStream.Write(dataByte, 0, dataByte.Length);
                }
                compressed = memoryStream.ToArray();
            }

            byte[] result = new byte[4 * 4 + compressed.Length];
            
            byte[] streamLength = BitConverter.GetBytes(dataByte.Length);
            byte[] sampleCount = BitConverter.GetBytes(pcm.Length);
            byte[] sampleRate = BitConverter.GetBytes(freq);
            byte[] channelsArr = BitConverter.GetBytes(channels);

            streamLength.CopyTo(result, 0);
            sampleCount.CopyTo(result, 4);
            sampleRate.CopyTo(result, 8);
            channelsArr.CopyTo(result, 12);
            compressed.CopyTo(result, 16);
            return result;
        }
        private static byte[] EncodeZip(byte[] ieee, int rate, int channels, int bytesPerSecond, int blockAlign, int bitsPerFrame)
        {
            byte[] wav = EncodeWav(ieee, rate, channels, bytesPerSecond, blockAlign, bitsPerFrame);
            
            byte[] compressed;
            
            using (var memoryStream = new MemoryStream())
            {
                using (DeflateStream compressionStream = new DeflateStream(memoryStream,
                    System.IO.Compression.CompressionLevel.Fastest))
                {
                    compressionStream.Write(wav, 0, wav.Length);
                    compressed = memoryStream.GetBuffer();
                }
            }

            byte[] result = new byte[4 + compressed.Length];
            
            byte[] streamLength = BitConverter.GetBytes(compressed.Length);

            streamLength.CopyTo(result, 0);
            compressed.CopyTo(result, 4);
            return result;
        }

        private static float[] DecodeMp3(byte[] buffer, out int sampleCount, out int sampleRate, out int channels)
        {
            //AudioClip properties
            int bytesPerSample = 2;
            //Resultant buffer
            byte[] pcmBuffer;

            Debug.LogError($"Initial mp3 Length = {buffer.Length}");
            //Decode MP3 to IEEE and then to PCM (Pulse code Modulation)
            using (MemoryStream memStream = new MemoryStream(buffer))
            {
                using (var reader =
                    new Mp3FileReaderBase(memStream, waveFormat => new Mp3FrameDecompressor(waveFormat)))
                {
                    Debug.LogError($"Size = {reader.Length} Block Align error = {reader.Length % reader.WaveFormat.BlockAlign} encoding = {reader.WaveFormat.Encoding}");

                    pcmBuffer = new byte[reader.Length];
                    reader.Read(pcmBuffer, 0, (int)reader.Length);

                    var format = reader.WaveFormat;
                    sampleRate = format.SampleRate;
                    channels = format.Channels;
                    bytesPerSample = format.BitsPerSample / 8;
                    sampleCount = pcmBuffer.Length / (bytesPerSample * channels);
                }
            }

            return IeeeToFloat(pcmBuffer);
        }

        private static float[] DecodeWav(byte[] buffer, out int sampleCount, out int sampleRate, out int channels)
        {
            int bytesPerSample = 2;
            byte[] pcmBuffer;
            
            //Decode WAV to PCM (Pulse code Modulation)
            using (MemoryStream memStream = new MemoryStream(buffer))
            {
                using (var reader =
                    new WaveFileReader(memStream))
                {
                    long size = reader.Length - reader.Length % reader.WaveFormat.BlockAlign;
                    pcmBuffer = new byte[size];
                    reader.Read(pcmBuffer, 0, (int) size);

                    var format = reader.WaveFormat;
                    sampleRate = format.SampleRate;
                    channels = format.Channels;
                    bytesPerSample = format.BitsPerSample / 8;
                    sampleCount = pcmBuffer.Length / (bytesPerSample * channels);
                }
            }

            if (bytesPerSample == 4) return IeeeToFloat(pcmBuffer);

            return PCMToFloat(pcmBuffer, sampleCount, channels);
        }

        private static float[] DecodeZip(byte[] buffer, out int sampleCount, out int sampleRate, out int channels)
        {
            int streamLength = BitConverter.ToInt32(buffer, 0);
            
            float[] result;

            byte[] data = buffer.Skip(4).ToArray();

            using (var memoryStream = new MemoryStream(data))
            {
                var dataByte = new byte[streamLength];
                using (DeflateStream compressionStream = new DeflateStream(memoryStream,
                    CompressionMode.Decompress))
                {
                    compressionStream.Read(dataByte, 0, dataByte.Length);

                    result = DecodeWav(dataByte, out sampleCount, out sampleRate, out channels);
                }
            }

            return result;
        }


        private static float[] IeeeToFloat(byte[] buffer)
        {
            float[] recoveredData = new float[buffer.Length / 4];
            Buffer.BlockCopy(buffer, 0, recoveredData, 0, buffer.Length);
            return recoveredData;
        }
        private static float[] PCMToFloat(byte[] buffer,int sampleCount, int channels)
        {
            float rescaleFactor16 = 32767; //to convert Int16 to Float

            float[] recoveredData = new float[sampleCount * channels];

            for (int i = 0; i < sampleCount * channels; i++)
            {
                int id = i * 2;
                float sample = BitConverter.ToInt16(buffer, id) / rescaleFactor16;

                recoveredData[i] = sample;
            }

            return recoveredData;
        }
    }
}
