using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using AForge.Video;
using AForge.Video.DirectShow;
using System.IO;
using System.Drawing.Imaging;
using EasyModbus;

namespace FeedControl
{
    delegate void SendRfidDelegate();

    public partial class MainForm : Form
    {
        private CameraService cameraService;
        private Cardreader cardreader;
        public String cardNumber;
        private Diary diary;
        private Timer modbusTimer;
        ModbusClient modbusClient = new ModbusClient("127.0.0.1", 502);

        const string CardreaderPortNum = "COM5"; //카드리더기 포트번호
        const int ControllerReadTimeInterval = 1; //PLC의 값 읽어오는 주기 (단위:초)
        const int RegisterCount = 6; //PLC에서 읽어올 레지스터 수

        private int commandRfidRead = 0;
        private int sendEndRfid = 0;
        private int checkFeedFinish = 0;
        private double firstWeight = 0, feedWeight = 0, totalWeight = 0;



        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            SendRfidDelegate del1 = WriteRfidRegisters;

            cameraService = new CameraService();
            cameraService.StartStream(1920);
            cameraService.NewFrame += new NewFrameEventHandler(FinalFrame_NewFrame);

            cardreader = new Cardreader(CardreaderPortNum, this, del1);


            cardreader.setSerialPort();
            diary = new Diary();

            modbusTimer = new Timer();
            modbusTimer.Interval = ControllerReadTimeInterval * 1000;
            modbusTimer.Tick += readDevice; // 타이머 이벤트 핸들러로 기존의 btnReadDevice_Click 사용
            modbusTimer.Start();
        }


        void FinalFrame_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            try
            {
                this.Invoke((MethodInvoker)delegate
                {
                    SetPictureBoxImage((Bitmap)eventArgs.Frame.Clone());
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SetPictureBoxImage(Image image)
        {
            if (pictureBox.Image != null)
            {
                pictureBox.Image.Dispose();
            }

            pictureBox.Image = image;
        }

        private async void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true; // Form 종료를 일시적으로 취소합니다.
            Task task = Task.Run(() =>
            {
                if (cameraService.IsRunning())
                {
                    cameraService.Stop();
                }
            });

            await task; // Task가 완료될 때까지 기다립니다.
            cameraService.Dispose();
            this.FormClosing -= MainForm_FormClosing; // 이벤트 핸들러를 제거하여 이 메서드가 다시 호출되지 않도록 합니다.
            this.Close(); // Form을 정상적으로 종료합니다.
        }

        private void SaveImage(Image image)
        {
            // Get the path of the local application data folder
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            // Define the snapshot directory
            string snapshotDirectory = Path.Combine(localAppData, "FeedControl", "Snapshot");

            // If the directory does not exist, create it
            if (!Directory.Exists(snapshotDirectory))
            {
                Directory.CreateDirectory(snapshotDirectory);
            }

            // Define the full file path
            string filePath = Path.Combine(snapshotDirectory, "Snapshot.bmp");

            if (File.Exists(filePath)) // If the file exists, prevent a memory leak.
            {
                // Delete the file to prevent a memory leak.
                File.Delete(filePath);
            }

            // Set the path and format to save the image.
            ImageFormat format = ImageFormat.Bmp; // Set the image format to PNG.
            ImageCodecInfo encoder = GetEncoder(format); // Get the ImageCodecInfo object.
            EncoderParameters encoderParams = GetEncoderParameters(format); // Get the EncoderParameter object array.

            image.Save(filePath, encoder, encoderParams);
        }

        private ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();

            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }

            return null;
        }

        private EncoderParameters GetEncoderParameters(ImageFormat format)
        {
            EncoderParameters encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 100L); // 이미지 품질 설정
            return encoderParams;
        }

        private void ReadModbusRegisters()
        {
            try
            {
                // 연결
                if (!modbusClient.Connected)
                    modbusClient.Connect();

                // 홀딩 레지스터에 접근해서 값 받아오기
                int startAddress = 9; // 시작 주소는 환경에 따라 변경해주세요.
                int[] holdingRegister = modbusClient.ReadHoldingRegisters(startAddress, RegisterCount);

                commandRfidRead = holdingRegister[0];
                sendEndRfid = holdingRegister[1];
                checkFeedFinish = holdingRegister[2];
                firstWeight = holdingRegister[3] / 100;
                feedWeight = holdingRegister[4] / 100;
                totalWeight = holdingRegister[5] / 100;


                //RFID 읽기 & 스냅샷 촬영
                if (commandRfidRead == 1 && sendEndRfid == 0)
                {
                    //RFID리딩 조건 
                    try
                    {
                        cardreader.read();
                        if (cameraService.IsRunning())
                        {
                            Task.Run(() =>
                            {
                                SaveImage(pictureBox.Image);
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }

                //사육일지 작성
                if (checkFeedFinish == 1)
                {
                    //TODO 급이에 맞는 API로 변경
                    diary.post("FEED", cardreader.getCardNumber(), 123, 123, 123, 123);
                    WriteMeasureFinishRegisters();
                }

            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        private void WriteRfidRegisters()
        {
            try
            {
                // 연결
                if (!modbusClient.Connected)
                    modbusClient.Connect();

                // 홀딩 레지스터에 접근해서 값 받아오기
                int startAddress = 0; // 시작 주소는 환경에 따라 변경해주세요.

                string cardNumber = cardreader.getCardNumber();
                int[] asciiArray = new int[9];
                if (cardNumber.Length > 0)
                {
                    for (int i = 0; i <= 7; i++)
                    {
                        asciiArray[i] = cardNumber[i];
                    }
                    if (cardNumber.Length == 8)
                    {
                        asciiArray[8] = 0;
                    }
                    else
                    {
                        asciiArray[8] = (int)cardNumber[8];
                    }
                    modbusClient.WriteMultipleRegisters(0, asciiArray);
                    modbusClient.WriteSingleRegister(10, 1);
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        private void WriteSnapshotRegisters()
        {
            try
            {
                // 연결
                if (!modbusClient.Connected)
                    modbusClient.Connect();
                modbusClient.WriteSingleRegister(12, 1);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        private void WriteMeasureFinishRegisters()
        {
            try
            {
                // 연결
                if (!modbusClient.Connected)
                    modbusClient.Connect();
                modbusClient.WriteSingleRegister(11, 0);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private async void readDevice(object sender, EventArgs e)
        {
            try
            {
                await Task.Run(() => ReadModbusRegisters());
            }
            catch (Exception ex)
            {
                Console.WriteLine("Modbus 연결에 실패했습니다: " + ex.Message);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
