using System;
using System.Messaging;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Cargoscan.BScaner;
using Cargoscan.DScanner;
using Cargoscan.Properties;
using Cargoscan.Cubiscan;
//using System.Collections.ObjectModel;
using Apache.NMS;
using Apache.NMS.Util;

using log4net;

namespace Cargoscan
{
        

    public partial class MainForm : Form
    {

        private static readonly ILog log = LogManager.GetLogger(typeof(Program));
        //public delegate void evScanbrcode();

        delegate void OutputText(string text);
        private BarcodeScaner BrcScanner;
        //private DimensionScaner DimScanner;
        //private WeightMeasure WeightScanner;
        private CubiscanWeightScan DoMeizure;

        //ActiveMQ activeMq = new ActiveMQ();
        //ActiveMQ.Publisher publisher = new ActiveMQ.Publisher();
        //listener = new Listener(this);
        //publisher = new publisher();
          
        
        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {

            
            BrcScanner = new BarcodeScaner();
            BrcScanner.ScanBarcodeEvnt += new BarcodeScanning(evScanbrcode);
            //DimScanner = new DimensionScaner();
            //WeightScanner = new WeightMeasure();
            DoMeizure = new CubiscanWeightScan();
            checkBox1.Checked = true;


            // ЛОГИРОВАНИЕ ПОД ApacheMQ
            
          

           // publisher.SendMessage("Hello");



            try
            {
               
                
                BrcScanner.Open(Settings.Default.PortBarcodeScanner, 9600, 8);
                DoMeizure.Open(Settings.Default.PortCubiscan, 9600, 8);
                //DimScanner.Open(Settings.Default.PortDimensionScaner, 9600, 8);
                //WeightScanner.Open(Settings.Default.PortWeighr, 9600, 8);
            }
            catch (Exception error)
            {
                log.Error("Ошибка запуска программы..." + error.Message);
                lblError.Text = error.Message;

            }


        }


        private void evScanbrcode(string ebarcode)
        {
            
            bool flOkscan = false;
            ResulthScan rscan = new ResulthScan();
            rscan.Barcode = ebarcode;
                        

            Barcode.Invoke(new OutputText((s)=>Barcode.Text=ebarcode), "newText");
            BrcScanner.SendingData = false;

            Weight.Invoke(new OutputText((s) => Weight.Text = ""), "newText");
            DimX.Invoke(new OutputText((s) => DimX.Text = ""), "newText");
            DimY.Invoke(new OutputText((s) => DimY.Text = ""), "newText");
            DimZ.Invoke(new OutputText((s) => DimZ.Text = ""), "newText");
            lblError.Invoke(new OutputText((s) => lblError.Text = ""), "newText");

            if (!checkBox1.Checked)
            {
                /*rscan.Height = 12.3;
                       rscan.Length = 10.3;
                       rscan.Weight = 9.3;
                       rscan.Width  = 7.2;

                       rscan.MeasureCreater = "65e29293-f4e2-11e5-85c0-005056b649b2";

                       JavaScriptSerializer serializer = new JavaScriptSerializer();
                       string json = serializer.Serialize(rscan);*/

                //ВЕТКА ПОД ДРУГИЕ ИЗМЕРИТЕ БОЛЕЕ НЕПОДДЕРЖИВАЕТСЯ
            }
            else
            {
                try {
                    // Откроем повторно COM порт Cubiscan для получения новых порций событий
                    DoMeizure.Open(Settings.Default.PortCubiscan, 9600, 8);

                    flOkscan = flOkscan | DoMeizure.Scan(ref rscan);
                    
                   //Бесконечный цикл зла. Пока не получим измерение не будет счастья.
                   //Сделано специально.
                    while (rscan.Weight==0)
                    {
                        flOkscan = flOkscan | DoMeizure.Scan(ref rscan);
                        //n++;
                    }
                    Double W = rscan.Weight;
                    /*Double L = rscan.Length;

                    string newErrorMsg = rscan.Weight.ToString();
                    lblError.Invoke(new OutputText((s) => newErrorMsg = ""), "newText");*/

                    if (flOkscan && W > 0)
                    {
                        
                        System.Messaging.Message ordermsg = new System.Messaging.Message();
                        ordermsg.Formatter = new XmlMessageFormatter(new Type[] { typeof(string) });

                        JavaScriptSerializer serializer = new JavaScriptSerializer();
                        string json = serializer.Serialize(rscan);

                        ordermsg.Label = "ScanDimentionCubiscan";
                        ordermsg.Priority = MessagePriority.Normal;
                        ordermsg.Body = json;

                        //*****************************************
                        //******Отправка сообщения в ActiveMQ******

                        IConnectionFactory factory = new NMSConnectionFactory("activemq:tcp://10.0.1.39:61616?wireFormat.tightEncodingEnabled=true");
                        ((Apache.NMS.ActiveMQ.ConnectionFactory)((Apache.NMS.NMSConnectionFactory)factory).ConnectionFactory).UserName = StartProgram.userNameMq;
                        ((Apache.NMS.ActiveMQ.ConnectionFactory)((Apache.NMS.NMSConnectionFactory)factory).ConnectionFactory).Password = StartProgram.passwordMq;

                        using (IConnection connection = factory.CreateConnection(StartProgram.userNameMq, StartProgram.passwordMq))

                        using (ISession session = connection.CreateSession())
                        {
                            IDestination destination = SessionUtil.GetDestination(session, StartProgram.queueName);

                            using (IMessageProducer producer = session.CreateProducer(destination))

                            {
                                connection.Start();
                                producer.DeliveryMode = MsgDeliveryMode.Persistent;


                                // Непонятные параметры взяты из примера
                                ITextMessage request = session.CreateTextMessage(json);
                                request.NMSCorrelationID = "Store";
                                request.Properties["NMSXGroupID"] = "StoreLibra";
                                request.Properties["myHeader"] = "Sending measurements";

                                producer.Send(request);
                            }
                        }


                        //*****************************************
                        //******Вывод информации от измерителя*****

                        Weight.Invoke(new OutputText((s) => Weight.Text = rscan.Weight.ToString()), "newText");

                        DimX.Invoke(new OutputText((s) => DimX.Text = rscan.Length.ToString() + " Д"), "newText");
                        DimY.Invoke(new OutputText((s) => DimY.Text = rscan.Width.ToString()  + " Ш"),  "newText");
                        DimZ.Invoke(new OutputText((s) => DimZ.Text = rscan.Height.ToString() + " В"), "newText");

                    }
                    else
                    {

                        Weight.Invoke(new OutputText((s) => Weight.Text = rscan.Weight.ToString()), "newText");

                        DimX.Invoke(new OutputText((s) => DimX.Text = rscan.Length.ToString() + " Д"), "newText");
                        DimY.Invoke(new OutputText((s) => DimY.Text = rscan.Width.ToString() + " Ш"), "newText");
                        DimZ.Invoke(new OutputText((s) => DimZ.Text = rscan.Height.ToString() + " В"), "newText");
                        /*Weight.Text = "0";
                        DimX.Text = "0";
                        DimY.Text = "0";
                        DimZ.Text = "0";*/

                    }
                }
                catch (Exception error)
                {
                    log.Error(error.Message);
                    BrcScanner.SendingData = true;

                    //this.lblError.Text = error.Message;
                    string newErrorMsg = error.Message;
                    lblError.Invoke(new OutputText((s) => newErrorMsg = ""), "newText");
                    Weight.Invoke(new OutputText((s) => Weight.Text = "0"), "newText");
                    DimX.Invoke(new OutputText((s) => DimX.Text = "0"), "newText");
                    DimY.Invoke(new OutputText((s) => DimY.Text = "0"), "newText");
                    DimZ.Invoke(new OutputText((s) => DimZ.Text = "0"), "newText");

                    BrcScanner.SendingData = true;
                }

                BrcScanner.SendingData = true;
            }
                
        }


        private void checkBox1_CheckedChanged_1(object sender, EventArgs e)
        {
            //checkBox2.Checked = false;//
            //DoMeizure.Open(Settings.Default.PortCubiscan, 9600, 8); //@#Settings.Default.PortCubiscan
        }

        private void groupBox3_Enter(object sender, EventArgs e)
        {

        }
    }
}
