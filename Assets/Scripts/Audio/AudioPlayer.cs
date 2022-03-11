using System;
using System.Collections.Generic;
using Redcode.Awaiting;
using UnityEngine;

namespace AudioStreaming
{
    public class AudioPlayer : MonoBehaviour
    {
        [SerializeField] private bool constantStreamPlay = true;
        [SerializeField] private CompressionType compressionType = CompressionType.MP3;
        [SerializeField] private AudioStreaming.AudioListener listener;

        private AudioSource _source;
        private Queue<byte[]> _encodedQueue = new Queue<byte[]>();
        private Queue<(float[], int, int, int)> _decodedQueue = new Queue<(float[], int, int, int)>();

        private bool _isAudioSource = false;
        private bool _decoding = false;
        private bool stop = false;

        int frequency = 48000;
        
        int currentBufferBeginIndex;
        int nextLastLoadedIndex;
        
        private int currentPlayerIndex = 0;
        private int currentAudioIndex = 0;
        private int channels = 2;

        private bool outSet = false;

        private List<float> _buffer = new List<float>();
        
        public void AudioListenerDisabled()
        {
            while (listener.GetFrame(out byte[] buffer)) _encodedQueue.Enqueue(buffer);
        }

        public void AddNewFrame()
        {
            if (!constantStreamPlay) return;
            if (listener.GetFrame(out byte[] buffer)) AddNewFrame(buffer);
        }

        public void AddNewFrame(byte[] buffer)
        {
            _encodedQueue.Enqueue(buffer);
        }

        public void SetClipPosition(float playerTime)
        {
            this.currentPlayerIndex = (int)(playerTime * frequency * channels);
            outSet = true;
        }

        void Update()
        {
            if (!_isAudioSource) return;

            if (stop)
            {
                _source.Stop();
                stop = false;
            }

            if (_encodedQueue.Count > 0 && !_decoding)
            {
                _decoding = true;
                byte[] buffer = _encodedQueue.Dequeue();
                DecodeAudio(buffer);
            }
            
            if (_decodedQueue.Count > 0)
            {
                float[] buffer;
                int sampleCount, ch, freq;
                (buffer, sampleCount, ch, freq) = _decodedQueue.Dequeue();
                frequency = freq;
                channels = ch;

                _buffer.AddRange(buffer);

                if (!_source.isPlaying)
                {
                    AudioClip myClip = AudioClip.Create("Decoded clip", sampleCount, ch, freq, true, OnAudioRead);
                    
                    
                    
                    _source.clip = myClip;
                    _source.loop = true;
                    _source.Play();
                }
            }
        }

        void Null(float[] data)
        {
            for (var i = 0; i < data.Length; i++)
            {
                data[i] = 0;
            }
        }
        void OnAudioRead(float[] data)
        {
            int diff = currentPlayerIndex - currentAudioIndex;
            float diffTime = (float) diff / (frequency * channels);

            if (outSet && diff < 0)
            {
                Null(data);
                return;
            }
            
            int newAudioIndex = diffTime <= 0.5f ? currentAudioIndex : currentPlayerIndex;

            if (diffTime < 0.5f)
            {
                diff = 0;
            }

            
            var min = Math.Min(_buffer.Count - diff, data.Length);
            
            for (int i = diff; i < diff + min; i++)
            {
                data[i - diff] = _buffer[i];
            }

            currentAudioIndex = newAudioIndex + min;
            _buffer.RemoveRange(0, diff + min);
        }


        private async void DecodeAudio(byte[] buffer)
        {
            await new WaitForBackgroundThread();
            float[] pcm = AudioClipCoder.Decode(buffer, out int sampleCount, out int freq, out int ch, compressionType);
            await new WaitForUpdate();

            _decodedQueue.Enqueue((pcm, sampleCount, ch, freq));
            _decoding = false;
        }
        void OnEnable()
        {
            _source = this.gameObject.GetComponent<AudioSource>();
            _isAudioSource = _source != null;
            if (_isAudioSource)
            {
                _source.playOnAwake = false;
                _source.loop = false;
            }

            if (listener)
            {
                listener.NewFrame.AddListener(AddNewFrame);
                listener.RecorderDisabled.AddListener(AudioListenerDisabled);
            }
        }
    }
}
