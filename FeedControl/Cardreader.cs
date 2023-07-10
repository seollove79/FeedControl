using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Text.RegularExpressions;

namespace FeedControl
{
    class Cardreader
    {
        private SerialPort serialPort = new SerialPort();
        private string serialPortName;
        private List<int> receiveData = new List<int>();
        private MainForm ownerForm;
        private string cardNumber = "";

        private SendRfidDelegate sendRfidDelegate;


        public Cardreader(string strSerialPortName, MainForm ownerForm, SendRfidDelegate del1)
        {
            serialPortName = strSerialPortName;
            this.ownerForm = ownerForm;
            sendRfidDelegate = del1;
        }

        public void setSerialPort()
        {
            try
            {
                serialPort.PortName = serialPortName;
                serialPort.BaudRate = 9600;
                serialPort.DataBits = 8;
                serialPort.StopBits = StopBits.One;
                serialPort.Parity = Parity.None;
                serialPort.DataReceived += new SerialDataReceivedEventHandler(serialPort_DataReceived);
                serialPort.Open();
            }
            catch (Exception ex)
            {
                ownerForm.Invoke(new Action(() =>
                {
                    MessageBox.Show(ownerForm, "RFID 리더기 연결에 실패했습니다: " + ex.Message);
                }));
            }
        }


        int count = 0;

        private void serialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)  //수신 이벤트가 발생하면 이 부분이 실행된다.
        {
            int i_recv_size = serialPort.BytesToRead;
            byte[] b_tmp_buf = new byte[i_recv_size];
            serialPort.Read(b_tmp_buf, 0, i_recv_size);
            cardNumber = "";

            foreach (var temp in b_tmp_buf)
            {
                if (temp == 1)
                {
                    receiveData.Clear();
                    count = 0;

                    cardNumber = "";

                    ownerForm.Invoke(new Action(() =>
                    {
                        ownerForm.cardNumber = "";
                        MessageBox.Show(ownerForm, "카드를 인식하지 못했습니다.");
                    }));
                    break;
                }

                receiveData.Add(temp);
                count++;

                int checkCount = 15;

                if (count == checkCount)
                {
                    for (int i = 3; i < checkCount; i++)
                    {
                        cardNumber = cardNumber + (char)receiveData[i];
                    }

                    string newNumber = Regex.Replace(cardNumber, @"[^0-9a-zA-Z가-힣]", "");
                    sendRfidDelegate();
                    Console.WriteLine("카드 인식에 성공하였습니다. 카드 번호: " + newNumber);

                    count = 0;
                    cardNumber = newNumber;
                    receiveData.Clear();
                }
            }
        }

        public string getCardNumber()
        {
            return cardNumber;
        }

        public void read()
        {
            if (!serialPort.IsOpen)
            {
                throw new Exception("카드리더기가 연결되어 있지 않습니다.");
            }

            byte[] readByte = { 0x23, 0x03, 0x02, 0x00, 0x03 };
            serialPort.Write(readByte, 0, readByte.Length);
        }


        public void close()
        {
            if (serialPort.IsOpen)
            {
                serialPort.Close();
            }
        }
    }
}
