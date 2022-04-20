using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;

namespace AsyncGUISImpleClient
{
    public partial class regist : Form
    {
        Socket server;
        IPAddress serverIPAddress;

        delegate void AppendTextDelegate(Control ctrl, string s);
        AppendTextDelegate _textAppend;

        string id;
        string password;

        public regist()
        {
            InitializeComponent();
            server = new Socket(AddressFamily.InterNetwork,
    SocketType.Stream, ProtocolType.Tcp);
        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void userId_TextChanged(object sender, EventArgs e)
        {

        }

        private void userPassword_TextChanged(object sender, EventArgs e)
        {

        }

        private void btnRegist_Click(object sender, EventArgs e)//여기서 서버 접속, 등록할 아이디/비밀번호 전송까지
        {
            int port=5000;//포트번호
            serverIPAddress = IPAddress.Loopback;
            string address = serverIPAddress.ToString();
            IPEndPoint serverEP = new IPEndPoint(serverIPAddress, port);
            try
            {
                server.Connect(serverEP);//서버와 연결
            }
            catch
            {
                return;//실패
            }
            //string[] tokens = text.Split(':');
     
         
            try 
            {
                if (!server.IsBound) return;
                id = userId.Text.Trim();
                password = userPassword.Text.Trim();
                //if (string.IsNullOrEmpty(text)) return;
                //byte[] bDts = new byte[4096];
                //bDts = Encoding.UTF8.GetBytes("REGIST" + ":" + Id + ":" + Password + ":");
                //server.Send(bDts);
                // beginreceive
                SendID();
                this.Close();
            } 
            catch 
            {

            }
            

            AsyncObject obj = new AsyncObject(4096);
            obj.workingSocket = server;
            server.BeginReceive(obj.Buffer, 0, obj.BufferSize, 0,
                DataReceived, obj);
        }
        void SendID()
        {
            byte[] bDts = Encoding.UTF8.GetBytes("REGIST:" + id + ":" + password );
            server.Send(bDts);
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }//취소 버튼-닫힘

        void DataReceived(IAsyncResult ar)//받은 데이터를 처리하는 곳
        {
            AsyncObject obj = ar.AsyncState as AsyncObject;
            try
            {
                int received = obj.workingSocket.EndReceive(ar);
                if (received <= 0)
                {
                    obj.workingSocket.Disconnect(false);
                    obj.workingSocket.Close();
                    return;
                }
            }
            catch { }


            string text = Encoding.UTF8.GetString(obj.Buffer);
            //AppendText(txtHistory, text);
            string[] tokens = text.Split(':');
            if (tokens[0].Equals("200"))
            {
                success showbox = new success();
                showbox.ShowDialog();//성공하면 성공창 띄우고
                this.Close();// 회원가입 창이 닫힌다.
            }
            else if (tokens[0].Equals("400"))
            {
                fail showbox = new fail();
                showbox.ShowDialog();//실패하면 실패 창을 띄운다.
            }
            

            obj.clearBuffer();
            try
            {
                obj.workingSocket.BeginReceive(obj.Buffer, 0, obj.BufferSize, 0,
                DataReceived, obj);
            }
            catch { }

        }
    }
}
