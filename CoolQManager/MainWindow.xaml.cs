using HPSocketCS;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CoolQManager
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        //服务配置文件
        String serviceConfigFile = "ServiceConfig.cfg";
        //服务配置信息
        ServiceConfig serviceConfig;
        //连接配置是否被修改
        Boolean isConfigModified = false;
        //连接信息是否被验证成功
        Boolean isConfigValidated = true;
        //服务状态
        private AppState appState = AppState.Stoped;
        //实例化服务
        HPSocketCS.TcpServer server = new HPSocketCS.TcpServer();
        HPSocketCS.Extra<ClientInfo> extra = new HPSocketCS.Extra<ClientInfo>();

        
        private delegate void ShowMsg(string msg);
        private ShowMsg AddMsgDelegate;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (File.Exists(serviceConfigFile) == false)
            {
                serviceConfig = new ServiceConfig();
                serviceConfig.CommunicationKey = "MyKey";
                serviceConfig.IsFileSplited = true;
                serviceConfig.IsSslEnabled = true;
                serviceConfig.ServicePort = 8421;

                String configJson = (new JavaScriptSerializer()).Serialize(serviceConfig);
                File.WriteAllText(serviceConfigFile, configJson, Encoding.UTF8);
            }
            else
            {
                String configJson = File.ReadAllText(serviceConfigFile, Encoding.UTF8);
                serviceConfig = (new JavaScriptSerializer()).Deserialize<ServiceConfig>(configJson);
            }
            ViewConfig_Update();

            AddMsgDelegate = new ShowMsg(AddMsg);

            // 设置服务器事件
            server.OnPrepareListen += new ServerEvent.OnPrepareListenEventHandler(OnPrepareListen);
            server.OnAccept += new ServerEvent.OnAcceptEventHandler(OnAccept);
            server.OnSend += new ServerEvent.OnSendEventHandler(OnSend);
            // 两个数据到达事件的一种
            server.OnPointerDataReceive += new ServerEvent.OnPointerDataReceiveEventHandler(OnPointerDataReceive);
            // 两个数据到达事件的一种
            //server.OnReceive += new ServerEvent.OnReceiveEventHandler(OnReceive);
            server.OnClose += new ServerEvent.OnCloseEventHandler(OnClose);
            server.OnShutdown += new ServerEvent.OnShutdownEventHandler(OnShutdown);

            SetAppState(AppState.Stoped);
            AddMsg(string.Format("HP-Socket Version: {0}", server.Version));
            ServiceStart();
        }

        private void ViewConfig_Update()
        {
            tb_CommunicationKey.Text = serviceConfig.CommunicationKey;
            tb_ServicePort.Text = serviceConfig.ServicePort.ToString();
            cb_IsSslEnabled.IsChecked = serviceConfig.IsSslEnabled;
            cb_IsFileSplited.SelectedIndex = serviceConfig.IsFileSplited == true ? 0 : 1;
        }

        private void bt_ConnectTest_Click(object sender, RoutedEventArgs e)
        {
            ModifiedConfig_Check();
        }

        private void ModifiedConfig_Check()
        {
            isConfigValidated = true;
        }

        private void bt_ConfigSave_Click(object sender, RoutedEventArgs e)
        {
            if (isConfigModified == false)
            {
                MessageBox.Show("配置未修改，此操作取消！", "警告", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            if (isConfigValidated == false)
            {
                ModifiedConfig_Check();
                if (isConfigValidated == false)
                {
                    return;
                }
            }

            ServiceConfig serviceConfigSave = new ServiceConfig();
            serviceConfig.CommunicationKey = tb_CommunicationKey.Text;
            if (int.TryParse(tb_ServicePort.Text, out int servicePortSave) == true)
            {
                serviceConfig.ServicePort = servicePortSave;
            }
            else
            {
                serviceConfig.ServicePort = 8421;
            }
            serviceConfig.IsSslEnabled = cb_IsSslEnabled.IsChecked == true ? true : false;
            serviceConfig.IsFileSplited = cb_IsFileSplited.SelectedIndex == 0 ? true : false;

            String configJson = (new JavaScriptSerializer()).Serialize(serviceConfig);
            File.WriteAllText(serviceConfigFile, configJson, Encoding.UTF8);

            serviceConfig = serviceConfigSave;

            isConfigModified = false;
            MessageBox.Show("新配置保存成功！", "信息", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        private void tb_ServicePort_TextChanged(object sender, TextChangedEventArgs e)
        {
            isConfigModified = true;
            isConfigValidated = false;
        }

        private void tb_CommunicationKey_TextChanged(object sender, TextChangedEventArgs e)
        {
            isConfigModified = true;
            isConfigValidated = false;
        }

        private void cb_IsSslEnabled_Checked(object sender, RoutedEventArgs e)
        {
            isConfigModified = true;
            isConfigValidated = false;
        }

        private void cb_IsFileSplited_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            isConfigModified = true;
            isConfigValidated = false;
        }

        private Boolean ServiceStart()
        {
            try
            {
                SetAppState(AppState.Starting);
                server.IpAddress = "0.0.0.0";
                server.Port = (ushort)(serviceConfig.ServicePort);

                // 启动服务
                if (server.Start())
                {
                    SetAppState(AppState.Started);
                    AddMsg(string.Format("$Server Start OK -> ({0}:{1})", server.IpAddress, server.Port));
                }
                else
                {
                    SetAppState(AppState.Stoped);
                    throw new Exception(string.Format("$Server Start Error -> {0}({1})", server.ErrorMessage, server.ErrorCode));
                }

                return false;
            }
            catch(Exception ex)
            {
                AddMsg(ex.Message);
                return false;
            }
        }
        private Boolean ServiceStop()
        {
            SetAppState(AppState.Stoping);

            // 停止服务
            AddMsg("$Server Stop");
            if (server.Stop())
            {
                SetAppState(AppState.Stoped);
            }
            else
            {
                AddMsg(string.Format("$Stop Error -> {0}({1})", server.ErrorMessage, server.ErrorCode));
            }
            return true;
        }

        HandleResult OnPrepareListen(IServer sender, IntPtr soListen)
        {
            // 监听事件到达了,一般没什么用吧?

            return HandleResult.Ok;
        }

        HandleResult OnAccept(IServer sender, IntPtr connId, IntPtr pClient)
        {
            // 客户进入了
            // 获取客户端ip和端口
            string ip = string.Empty;
            ushort port = 0;
            if (server.GetRemoteAddress(connId, ref ip, ref port))
            {
                AddMsg(string.Format(" > [{0},OnAccept] -> PASS({1}:{2})", connId, ip.ToString(), port));
            }
            else
            {
                AddMsg(string.Format(" > [{0},OnAccept] -> Server_GetClientAddress() Error", connId));
            }


            // 设置附加数据
            ClientInfo clientInfo = new ClientInfo();
            clientInfo.ConnId = connId;
            clientInfo.IpAddress = ip;
            clientInfo.Port = port;
            if (extra.Set(connId, clientInfo) == false)
            {
                AddMsg(string.Format(" > [{0},OnAccept] -> SetConnectionExtra fail", connId));
            }

            return HandleResult.Ok;
        }

        HandleResult OnSend(IServer sender, IntPtr connId, byte[] bytes)
        {
            // 服务器发数据了
            AddMsg(string.Format(" > [{0},OnSend] -> ({1} bytes)", connId, bytes.Length));
            return HandleResult.Ok;
        }


        HandleResult OnPointerDataReceive(IServer sender, IntPtr connId, IntPtr pData, int length)
        {
            // 数据到达了
            try
            {
                // 可以通过下面的方法转换到byte[]
                // byte[] bytes = new byte[length];
                // Marshal.Copy(pData, bytes, 0, length);

                // 获取附加数据
                ClientInfo clientInfo = extra.Get(connId);
                if (clientInfo != null)
                {
                    // clientInfo 就是accept里传入的附加数据了
                    AddMsg(string.Format(" > [{0},OnReceive] -> {1}:{2} ({3} bytes)", clientInfo.ConnId, clientInfo.IpAddress, clientInfo.Port, length));
                }
                else
                {
                    AddMsg(string.Format(" > [{0},OnReceive] -> ({1} bytes)", connId, length));
                }

                if (server.Send(connId, pData, length))
                {
                    return HandleResult.Ok;
                }

                return HandleResult.Error;
            }
            catch (Exception)
            {

                return HandleResult.Ignore;
            }
        }
        HandleResult OnReceive(IServer sender, IntPtr connId, byte[] bytes)
        {
            // 数据到达了
            try
            {
                // 获取附加数据
                ClientInfo clientInfo = extra.Get(connId);
                if (clientInfo != null)
                {
                    // clientInfo 就是accept里传入的附加数据了
                    AddMsg(string.Format(" > [{0},OnReceive] -> {1}:{2} ({3} bytes)", clientInfo.ConnId, clientInfo.IpAddress, clientInfo.Port, bytes.Length));
                }
                else
                {
                    AddMsg(string.Format(" > [{0},OnReceive] -> ({1} bytes)", connId, bytes.Length));
                }

                if (server.Send(connId, bytes, bytes.Length))
                {
                    return HandleResult.Ok;
                }

                return HandleResult.Error;
            }
            catch (Exception)
            {

                return HandleResult.Ignore;
            }
        }

        HandleResult OnClose(IServer sender, IntPtr connId, SocketOperation enOperation, int errorCode)
        {
            if (errorCode == 0)
                AddMsg(string.Format(" > [{0},OnClose]", connId));
            else
                AddMsg(string.Format(" > [{0},OnError] -> OP:{1},CODE:{2}", connId, enOperation, errorCode));

            if (extra.Remove(connId) == false)
            {
                AddMsg(string.Format(" > [{0},OnClose] -> SetConnectionExtra({0}, null) fail", connId));
            }

            return HandleResult.Ok;
        }

        HandleResult OnShutdown(IServer sender)
        {
            // 服务关闭了
            AddMsg(" > [OnShutdown]");
            return HandleResult.Ok;
        }

        /// <summary>
        /// 设置程序状态
        /// </summary>
        /// <param name="state"></param>
        void SetAppState(AppState state)
        {
            appState = state;
        }

        /// <summary>
        /// 往listbox加一条项目
        /// </summary>
        /// <param name="msg"></param>
        void AddMsg(string msg)
        {
            try
            {
                this.Dispatcher.Invoke(
                    new Action(() =>
                    {
                        tb_ReceivedMsg.AppendText(msg + Environment.NewLine);
                        tb_ReceivedMsg.ScrollToEnd();
                    }), System.Windows.Threading.DispatcherPriority.Normal);
            }
            catch { }
        }

        private void Window_Unloaded(object sender, RoutedEventArgs e)
        {
            if (server != null)
            {
                server.Destroy();
            }
        }
    }

    public enum AppState
    {
        Starting, Started, Stoping, Stoped, Error
    }

    [StructLayout(LayoutKind.Sequential)]
    public class ClientInfo
    {
        public IntPtr ConnId { get; set; }
        public string IpAddress { get; set; }
        public ushort Port { get; set; }
    }
}
