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

namespace ASyncGuISimpleServer
{
    public partial class MultiChat : Form
    {
        Socket server;
        IPAddress serverIPAddress;
        delegate void AppendTextDelegate(Control ctrl, string s);
        AppendTextDelegate _textAppend;
        List<AsyncObject> connectedClient;
        int clientNum;


        public MultiChat()
        {
            InitializeComponent();
            server = new Socket(AddressFamily.InterNetwork,
                SocketType.Stream, ProtocolType.Tcp);
            _textAppend = new AppendTextDelegate(AppendText);
            connectedClient = new List<AsyncObject>();

            clientNum = 0;

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

        private void btnStart_Click(object sender, EventArgs e)
        {
            int port;
            if ( !int.TryParse(txtPort.Text, out port))
            {  // port 입력 안 함
                MsgBoxHelper.Warn("포트를 입력하세요");

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
            server.Bind(serverEP);
            server.Listen(10);
            AppendText(txtHistory, string.Format("서버시작: {0}",serverEP));
            server.BeginAccept(AcceptCallback, null);
        }

        void AcceptCallback(IAsyncResult ar)
        {
            try
            {
                Socket client = server.EndAccept(ar);

                AppendText(txtHistory, string.Format("클라이언트({0})가 연결되었습니다",
                    client.RemoteEndPoint));

                server.BeginAccept(AcceptCallback, null);

                AsyncObject obj = new AsyncObject(4096);
                obj.workingSocket = client;
                client.BeginReceive(obj.Buffer, 0, obj.BufferSize, 0,
                    DataReceived, obj);
            }
            catch { }
            

        }

        void DataReceived(IAsyncResult ar)
        {
            AsyncObject obj = ar.AsyncState as AsyncObject;
            try
            {
                int received = obj.workingSocket.EndReceive(ar);
                if (received <= 0) // 종료
                {
                    foreach (AsyncObject clients in connectedClient)
                    {
                        if (obj.workingSocket == clients.workingSocket)
                        {
                            try
                            {
                                connectedClient.Remove(clients);
                                AppendText(txtHistory, string.Format("접속해제 완료: {0}", clients.ID));
                            }
                            catch { }
                            break;
                        }
                    }
                    obj.workingSocket.Disconnect(false);
                    obj.workingSocket.Close();
                    clientNum--;
                    return;
                }
            }
            catch { }


            string text = Encoding.UTF8.GetString(obj.Buffer);
            

            string[] tokens = text.Split(':');
            string fromID = null;
            string toID = null;
            string code = tokens[0];

            byte[] COM = new byte[4096];
            COM = Encoding.UTF8.GetBytes(
                "COM:" +
                "모두에게 보내기=(BR:내용)"+ "\r\n" +
                "한명에게 보내기=(TO:ID:내용)" +"\r\n"+
                "특정인원들에게 보내기=(GROUP:사람수:ID:~~:ID:내용)" + "\r\n" +
                "접속자 목록=(INFO:USER)");
            byte[] USER = new byte[4096];

            try
            {
                if (code.Equals("ID"))
                {
                    clientNum++;
                    fromID = tokens[1].Trim();
                    obj.ID = fromID;
                    AppendText(txtHistory, string.Format("[접속{0}]ID: {1},{2}",
                        clientNum, fromID, obj.workingSocket.RemoteEndPoint.ToString()));
                    //connectedClients.Add(fromID, obj.workingSocket);
                    connectedClient.Add(obj);
                    sendAll(obj.workingSocket, obj.Buffer);
                    obj.workingSocket.Send(Encoding.UTF8.GetBytes("OK:REGIST:"));
                }
                else if (code.Equals("MSG"))//tokens[0]이 MSG일 때
                {
                    if (tokens[1].Equals("BR"))//전달 된 내용= MSG:BR:내용:fromID
                    {
                        fromID = tokens[3].Trim();
                        string msg = tokens[2];
                        AppendText(txtHistory, string.Format("[전체]{0}:{1}", fromID, msg));
                        sendAll(obj.workingSocket, obj.Buffer);
                        obj.workingSocket.Send(Encoding.UTF8.GetBytes("OK:BR:"));
                    }
                    else if (tokens[1].Equals("TO"))//전달 된 내용=MSG:TO:toID:내용:fromID
                    {
                        toID = tokens[2].Trim();
                        fromID = tokens[4].Trim();
                        string msg = tokens[3];
                        string rMsg = "[TO:" + toID + "][FROM:" + fromID + "]" + msg;
                        AppendText(txtHistory, rMsg);
                        sendTo(toID, obj.Buffer);
                        obj.workingSocket.Send(Encoding.UTF8.GetBytes("OK:TO:"));
                    }
                    else if (tokens[1].Equals("GROUP"))//전달 된 내용=msg:group:num:id:id:id:....:내용
                    {   //msg:group:num:id:id:id:....:content
                        int toNum = int.Parse(tokens[2]);//메시지 받는 사람들의 수
                        string[] toArray = new string[toNum];//받는 사람들 배열 선언, 내용은 아이디들
                        string msg = tokens[toNum+3];
                        string rMsg = "[특정 다수에게] : " + msg ;
                        for (int i = 0; i < toNum; i++)//사람 수 만큼 반복(0~받는 사람 수-1)
                        {
                            toArray[i] = tokens[3 + i];
                            sendGroup(toArray[i], obj.Buffer);
                        }
                        AppendText(txtHistory, rMsg);
                        obj.workingSocket.Send(Encoding.UTF8.GetBytes("OK:GROUP:"));
                    }

                }
                else if (code.Equals("INFO"))//서버에 정보 요청이 왔을 때
                {
                    if (tokens[1].Equals("COM"))//명령어 요청
                    {
                        fromID = tokens[2].Trim();
                        sendTo(fromID, COM);
                        AppendText(txtHistory, string.Format("[명령어] {0} 에게", fromID));
                    }
                    else if (tokens[1].Equals("USER"))//접속자 요청
                    {
                        fromID = tokens[2].Trim();
                        
                        foreach (AsyncObject user in connectedClient)
                        {
                            USER = Encoding.UTF8.GetBytes("USER:"+ user.ID + ":\r\n");
                            sendTo(fromID, USER);
                            AppendText(txtHistory, string.Format("접속자:"+user.ID));
                        }
                        
                        AppendText(txtHistory, string.Format("[접속자] {0} 에게", fromID));
                    }
                }
                else { }
            }
            catch 
            {
                obj.workingSocket.Send(Encoding.UTF8.GetBytes("NO:"));
            }
            

            obj.clearBuffer();
            try
            {
                obj.workingSocket.BeginReceive(obj.Buffer, 0, obj.BufferSize,
                    0, DataReceived, obj);
            }
            catch { }

        }

        void sendTo(string toID, byte[] buffer)
        {
            foreach (AsyncObject obj in connectedClient)
            {
                if (string.Equals(obj.ID, toID))
                {
                    try
                    {
                        obj.workingSocket.Send(buffer);
                    }
                    catch
                    {
                        obj.workingSocket.Dispose();
                    }
                }
            }
           
        }

        void sendAll(Socket except, byte[] buffer)
        {
            //foreach (Socket socket in connectedClients.Values)
            foreach (AsyncObject clients in connectedClient)
            {
                if (except != clients.workingSocket)
                {
                    try
                    {
                        clients.workingSocket.Send(buffer);
                    }
                    catch
                    {
                        try { clients.workingSocket.Dispose(); } catch { }
                    }
                }
              
            }
        }

        void sendGroup(string groupID, byte[] buffer)
        {
            foreach (AsyncObject obj in connectedClient)
            {
                if (string.Equals(obj.ID, groupID))
                {
                    try
                    {
                        obj.workingSocket.Send(buffer);
                    }
                    catch
                    {
                        obj.workingSocket.Dispose();
                    }
                }
            }
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            if (!server.IsBound)
            {
                MsgBoxHelper.Warn("서버를 실행하세요");
                return;
            }
            string text = txtSend.Text.Trim();
            if (string.IsNullOrEmpty(text))
            {
                MsgBoxHelper.Warn("텍스트를 입력하세요");
                return;
            }

            byte[] bDts = Encoding.UTF8.GetBytes("Server:" + text);
            AppendText(txtHistory, "Server:" + text);
            try
            {
                sendAll(null, bDts);
            }
            catch { }
            txtSend.Clear();
        }

        private void MultiChat_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                server.Close();
            }
            catch 
            { }
        }

        private void MultiChat_Load(object sender, EventArgs e)
        {
            IPHostEntry he = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress addr in he.AddressList)
            {
                if (addr.AddressFamily == AddressFamily.InterNetwork)
                {
                    string s = "server address:" + addr.ToString();
                    AppendText(txtHistory, s);
                }
            }

        }
    }
}
