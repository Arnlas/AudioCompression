using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using NAudio.Lame;
using UnityEngine;
using UnityEngine.Events;

using CSCore.SoundIn;
using CSCore.CoreAudioAPI;
using Redcode.Awaiting;
using Debug = UnityEngine.Debug;

namespace AudioStreaming
{
    public enum CompressionType
    {
        WAV,
        ZIP,
        MP3
    }
    
    public enum CaptureMode
    {
        Capture,
        LoopbackCapture
    }

    public class AudioListener : MonoBehaviour
    {
        public UnityEvent NewFrame = new UnityEvent();
        public UnityEvent RecorderDisabled = new UnityEvent();

        [SerializeField] private bool autoCompress = true;
        [SerializeField] private CompressionType compressionType = CompressionType.MP3;
        [SerializeField] private NAudio.Lame.LAMEPreset compressionQuality = LAMEPreset.V2;
        public float frameSize = -1.0f;
        private float streamingFrameSize;

        private AudioClip _clip;
        private Stopwatch _frameTimer = new Stopwatch();
        private List<float> _frame = new List<float>();
        private WasapiCapture _capture;
        private int _lastPos = 0;

        protected List<byte> _buffer = new List<byte>();
        private int bytesPerFrame = 0;
        private Queue<byte[]> _queue = new Queue<byte[]>();

        [SerializeField] private bool _isPlaying = false;


        public void Play()
        {
            InitialiseMicrophone();
            
            _isPlaying = true;
        }

        public void Stop()
        {
            if (_capture != null)
            {
                _capture.Stop();
                _capture.Dispose();
                _capture = null;
            }
            
            _isPlaying = false;
        }

        public bool GetFrame(out byte[] frame)
        {
            frame = new byte[0];
            if (_queue.Count <= 0)
            {
                Debug.LogError("Not enough frames. Do something!!!");
                return false;
            }

            frame = _queue.Dequeue();
            return true;
        }
        
        private async Task ProcessAudio()
        {
            lock (_buffer)
            {
                if (_buffer.Count > bytesPerFrame)
                {
                    byte[] data = new byte[bytesPerFrame];
                    Array.Copy(_buffer.ToArray(), 0, data, 0, bytesPerFrame);
                    _buffer.RemoveRange(0, bytesPerFrame);
                    _buffer = new List<byte>();

                    _frameTimer.Restart();

                    if (autoCompress)
                    {
                        EncodeAudio(data);
                        return;
                    }

                    _queue.Enqueue(data);
                    
                    NewFrame.Invoke();
                }
            }
        }

        public async Task EncodeAudio(byte[] data, string path = "")
        {
            await new WaitForBackgroundThread();

            byte[] reduced = data;
            int channels = _capture.WaveFormat.Channels;
            int blockAlign = _capture.WaveFormat.BlockAlign;
            
            if (_capture.WaveFormat.Channels > 2)
            {
                reduced = AudioClipCoder.ReduceChannels(data, _capture.WaveFormat.BytesPerSample, _capture.WaveFormat.Channels, 2);
                channels = 2;
                blockAlign = _capture.WaveFormat.BytesPerSample * 2;
            }
            
            Debug.LogError($"About to stream {reduced.Length}");
            
            byte[] buffer = AudioClipCoder.Encode(reduced, _capture.WaveFormat.SampleRate, channels, _capture.WaveFormat.BytesPerSecond,
                blockAlign, _capture.WaveFormat.BitsPerSample, compressionQuality, compressionType, path);

            if (path != "") return;
            await new WaitForUpdate();
             Debug.LogError("Done encoding");
            _queue.Enqueue(buffer);
            NewFrame.Invoke();
        }

        private void InitialiseMicrophone()
        {
            MMDevice device = RefreshDevices();
            
            Debug.LogError(device.FriendlyName);

            if (device == null)
            {
                return;
            }

            streamingFrameSize = frameSize;
            //if (streamingFrameSize < 0) streamingFrameSize = CdnStreamPlayer.segmentTimeInterval;
            
            _frame.Clear();
            _queue.Clear();
        
            _capture = new WasapiCapture();
            _capture.Device = device;
            _capture.Initialize();

            _capture.DataAvailable += DataAvailable;

            bytesPerFrame = (int) (streamingFrameSize * _capture.WaveFormat.SampleRate * _capture.WaveFormat.BytesPerSample * _capture.WaveFormat.Channels);
            Debug.LogError($"Streaming frame size = {streamingFrameSize} with bytes per frame = {bytesPerFrame}");

            _capture.Start();
            _frameTimer.Restart();
        }
        
        private MMDevice RefreshDevices()
        {
            using (var deviceEnumerator = new MMDeviceEnumerator())
            using (var deviceCollection = deviceEnumerator.EnumAudioEndpoints(
                AudioStreaming.CaptureMode.Capture == AudioStreaming.CaptureMode.Capture ? DataFlow.Capture : DataFlow.Render, DeviceState.Active))
            {
                foreach (var device in deviceCollection)
                {
                    if (device.FriendlyName.Contains("USB Audio")) return device;
                    if (device.FriendlyName.Contains("GXT 256")) return device;
                    if (device.FriendlyName.Contains("Azure")) return device;
                }

                if (deviceCollection.Count <= 0) return null;
                
                return deviceCollection[0];
            }
        }

        private void DataAvailable(object s, DataAvailableEventArgs e)
        {
            if (!_isPlaying) return;
            byte[] tmp = new byte[e.ByteCount];
            Array.Copy(e.Data, e.Offset, tmp, 0, e.ByteCount);
            lock (_buffer)
            {
                _buffer.AddRange(tmp);
            }

            ProcessAudio();
        }

        void OnEnable()
        {
            if (_isPlaying) InitialiseMicrophone();
        }

        void OnDisable()
        {
            Stop();
            RecorderDisabled.Invoke();
        }
    }
}
