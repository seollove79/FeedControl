using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using AForge.Video;
using AForge.Video.DirectShow;
using System.Drawing.Imaging;

namespace FeedControl
{
    public class CameraService : IDisposable
    {
        private FilterInfoCollection CaptureDevice;
        private VideoCaptureDevice FinalFrame;

        // This event will be invoked when a new frame is ready
        public event NewFrameEventHandler NewFrame;

        public CameraService()
        {
            CheckCaptureDevice();
            FinalFrame = new VideoCaptureDevice();
        }

        private void CheckCaptureDevice()
        {
            CaptureDevice = new FilterInfoCollection(FilterCategory.VideoInputDevice);

            if (CaptureDevice.Count == 0)
            {
                throw new Exception("No camera detected");
            }
        }

        public void StartStream(int resolution)
        {
            if (CaptureDevice.Count == 0)
            {
                return;
            }

            FinalFrame = new VideoCaptureDevice(CaptureDevice[CaptureDevice.Count - 1].MonikerString);


            if (FinalFrame.VideoCapabilities.Length > 0)
            {
                foreach (VideoCapabilities capability in FinalFrame.VideoCapabilities)
                {
                    if (capability.FrameSize.Width == resolution)
                    {
                        FinalFrame.VideoResolution = capability;
                        break;
                    }
                }
            }

            FinalFrame.NewFrame += FinalFrame_NewFrame;

            try
            {
                FinalFrame.Start();
            }
            catch (Exception ex)
            {
                throw new Exception("Camera could not be started. Maybe it is already in use.");
            }
        }

        private void FinalFrame_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            // Invoke the NewFrame event
            NewFrame?.Invoke(sender, eventArgs);
        }

        public bool IsRunning()
        {
            return FinalFrame != null && FinalFrame.IsRunning;
        }

        public void Stop()
        {
            if (IsRunning())
            {
                FinalFrame.SignalToStop();
                FinalFrame.WaitForStop();
            }
        }

        public Size getResolution()
        {
            return FinalFrame.VideoResolution.FrameSize;
        }

        public void Dispose()
        {
            if (IsRunning())
            {
                Stop();
            }

            if (FinalFrame != null)
            {
                FinalFrame.NewFrame -= FinalFrame_NewFrame;
                FinalFrame = null;
            }
        }


    }
}
