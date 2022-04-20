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
    public partial class Client : Form
    {
        Socket server;
        IPAddress serverIPAddress;

        delegate void AppendTextDelegate(Control ctrl, string s);
        AppendTextDelegate _textAppend;

        string nameID;

        public Client()
        {
            InitializeComponent();
            server = new Socket(AddressFamily.InterNetwork,
    SocketType.Stream, ProtocolType.Tcp);
            _textAppend = new AppendTextDelegate(AppendText);
        }

        void AppendText(Control ctrl, string s)
        {
            if (ctrl.InvokeRequired)
                ctrl.Invoke(_textAppend, ctrl, s);
            else
            {
                string source = ctrl.Text;
                ctrl.Text = source + Environment.NewLine + s;
            }
        }
        private void btnConnect_Click(object sender, EventArgs e)
        {
            int port;
            if (!int.TryParse(txtPort.Text, out port))
            {  // port 입력 안 함
                txtPort.Focus();
                txtPort.SelectAll();
                return;
            }

            if (string.IsNullOrEmpty(txtAddress.Text))
            {
                serverIPAddress = IPAddress.Loopback;
                txtAddress.Text = serverIPAddress.ToString();
            }
            else
            {
                serverIPAddress = IPAddress.Parse(txtAddress.Text);
            }
            IPEndPoint serverEP = new IPEndPoint(serverIPAddress, port);

            if (string.IsNullOrEmpty(txtNameID.Text))//아이디 미입력시
            {
                MsgBoxHelper.Warn("ID를 입력하세요");
                return;
            }
            nameID = txtNameID.Text;

            try
            {
                server.Connect(serverEP);
            } 
            catch
            {
                return;
            }
            SendID();
            
            AsyncObject obj = new AsyncObject(4096);
            obj.workingSocket = server;
            server.BeginReceive(obj.Buffer, 0, obj.BufferSize, 0,
                DataReceived, obj);
        }
        void SendID()
        {
            byte[] bDts = Encoding.UTF8.GetBytes("ID:" + nameID + ":");
            server.Send(bDts);

            AppendText(txtHistory, "서버와 연결되었습니다.");
            AppendText(txtHistory, "INFO:COM을 입력하여 명령어를 확인하세요.");
        }
        void DataReceived(IAsyncResult ar)
        {
            AsyncObject obj = ar.AsyncState as AsyncObject;
            try {
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
            string[] tokens = text.Split(':');
            if (tokens[0].Equals("ID"))
            {
                string id = tokens[1];
                AppendText(txtHistory, string.Format("[접속] ID:{0}", id));
            }
            else if (tokens[0].Equals("MSG"))
            {
                if(tokens[1].Equals("BR"))//전달 된 내용= MSG:BR:내용:fromID:
                {
                    string msg = tokens[2];
                    string fromID = tokens[3];
                    
                    AppendText(txtHistory, string.Format("[전체] {0}:{1}", fromID, msg));
                }
                else if(tokens[1].Equals("TO"))//전달 된 내용=MSG:TO:toID:내용:fromID
                {
                    string toID = tokens[2];
                    string msg = tokens[3];
                    string fromID = tokens[4];
                    AppendText(txtHistory, string.Format("[FROM] " + fromID + " : " + msg));
                }
                else if (tokens[1].Equals("GROUP"))//전달 된 내용= MSG:GROUP:숫자:ID~:ID:내용:fromID
                {
                    int toNum = int.Parse(tokens[2]);//메시지 받는 사람들의 수
                    string msg = tokens[toNum + 3];
                    string fromID = tokens[toNum + 4];
                    for (int i = 3; i < toNum + 3; i++)
                    {
                        if (nameID.Equals(tokens[i]))
                        {
                            AppendText(txtHistory, string.Format("[GROUP] {0} : {1}",fromID, msg));
                        }
                    }
                }
            }
            else if (tokens[0].Equals("COM"))//명령어 전달 받음
            {
                AppendText(txtHistory, string.Format(text));
            }
            else if (tokens[0].Equals("USER"))//접속자 전달 받음
            {
                AppendText(txtUser, string.Format(tokens[1]));
            }
            else if (tokens[0].Equals("Server"))
            {
                string msg = tokens[1];
                AppendText(txtHistory, string.Format("[공지]{0}", msg));
            }
          
            
            else if (tokens[0].Equals("OK"))//요청에 대한 응답
            {
                if (tokens[1].Equals("REGIST"))
                {
                    AppendText(txtHistory, string.Format("ID 등록성공"));
                }
                else if (tokens[1].Equals("BR"))
                {
                    AppendText(txtHistory, string.Format("전체 전송 성공"));
                }
                else if (tokens[1].Equals("TO"))
                {
                    AppendText(txtHistory, string.Format("TO 성공"));
                }
                else if (tokens[1].Equals("GROUP"))
                {
                    AppendText(txtHistory, string.Format("GROUP 성공"));
                }
            }
            else if (tokens[0].Equals("NO"))//요청에 대한 응답
            {
                AppendText(txtHistory, string.Format("요청 실패"));
            }

            obj.clearBuffer();
            try
            {
                obj.workingSocket.BeginReceive(obj.Buffer, 0, obj.BufferSize, 0,
                DataReceived, obj);
            }
            catch { }
            
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            if (!server.IsBound) return;
            string text = txtSend.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;


            string[] tokens = text.Split(':');
            byte[] bDts = new byte[4096];

            if (tokens[0].Equals("BR"))//실제 입력=BR:내용 / 전달 될 내용= MSG:BR:내용:fromID
            {
                bDts = Encoding.UTF8.GetBytes("MSG:" + "BR:" + tokens[1] + ':' + nameID + ':');
                AppendText(txtHistory, string.Format("[전체전송] {0}", tokens[1]));
            }
            else if(tokens[0].Equals("TO"))//실제 입력 내용=TO:toID:내용 / 전달 될 내용=MSG:TO:toID:내용:fromID
            {
                bDts = Encoding.UTF8.GetBytes("MSG:" + "TO:" + tokens[1] + ':' + tokens[2] + ':' + nameID + ':');
                AppendText(txtHistory, string.Format("[{0}에게 전송] : {1}", tokens[1], tokens[2]));
            }
            //실제 입력 내용= GROUP:숫자:ID:~:ID:내용 / 전달 될 내용= MSG:GROUP:NUM:ID~:ID:내용:fromID:
            else if (tokens[0].Equals("GROUP"))
            {
                bDts = Encoding.UTF8.GetBytes("MSG" + ':' + text + ':' + nameID + ':');
                AppendText(txtHistory, string.Format("특정 다수에게 전송"));
            }
            else if(tokens[0].Equals("INFO"))//서버에 정보 요청
            {
                bDts = Encoding.UTF8.GetBytes(text + ':' + nameID + ':');
                if (tokens[1].Equals("COM"))//전달 될 내용 = INFO:COM:fromID
                {
                    AppendText(txtHistory, string.Format("채팅 명령어 요청함"));
                }
                else if(tokens[1].Equals("USER"))//전달 될 내용 = INFO:USER:fromID
                {
                    AppendText(txtHistory, string.Format("접속자 아이디 요청함"));
                }
                    
            }
            try { server.Send(bDts); } catch { }
            txtSend.Clear();

        }

        private void Client_FormClosing(object sender, FormClosingEventArgs e)
        {
                server.Close();
        }

        private void txtPort_TextChanged(object sender, EventArgs e)
        {

        }


        private void btnUser_Click(object sender, EventArgs e)//접속자 명단 요청 버튼
        {
            if (!server.IsBound) return;
            txtUser.Clear();
            byte[] bDts = new byte[4096];
            bDts = Encoding.UTF8.GetBytes("INFO:USER:" + nameID + ':');

            try { server.Send(bDts); } catch { }
            
        }
    }
}
