using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AudioStreaming;
using UnityEngine;
using UnityEngine.UI;

namespace AudioStreaming
{
    public class AudioRecorder : MonoBehaviour
    {
        [SerializeField] private AudioStreaming.AudioListener listener;

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                Record(5);
            }
        }

        private bool done = false;

        public void Record(InputField text)
        {
            bool done = float.TryParse(text.text, out float data);
            if (!done) text.text = "5";
            else
            {
                listener.frameSize = data;
                listener.NewFrame.AddListener(DataRecorded);
                listener.Play();
            }
        }

        public void Record(float time)
        {
            listener.frameSize = time;
            listener.NewFrame.AddListener(DataRecorded);
            listener.Play();
        }

        private async void DataRecorded()
        {
            byte[] data = new byte[0];
            while (listener.GetFrame(out byte[] piece))
            {
                data = data.Concat(piece).ToArray();
            }

            Debug.LogError(Application.streamingAssetsPath);
            await listener.EncodeAudio(data, Application.streamingAssetsPath);

            listener.Stop();
            listener.NewFrame.RemoveAllListeners();
            Debug.LogError("Record is done!!!");
        }
    }
}
